module.exports = async ({ github, context }) => {
  const fs = require('fs');
  const path = require('path');
  const resultsDir = path.join(process.cwd(), 'TestResults');

  if (!fs.existsSync(resultsDir)) {
    console.log('No TestResults directory found.');
    return;
  }

  // Parse TRX test results
  const trxFiles = fs.readdirSync(resultsDir).filter(f => f.endsWith('.trx'));
  let total = 0, passed = 0, failed = 0, skipped = 0;
  let startTime, finishTime;

  const getAttr = (line, attr) => {
    const match = line.match(new RegExp(`${attr}="(\\d+)"`));
    return match ? parseInt(match[1]) : 0;
  };

  for (const file of trxFiles) {
    const content = fs.readFileSync(path.join(resultsDir, file), 'utf8');

    const countersMatch = content.match(/<Counters [^>]*\/>/);
    if (countersMatch) {
      const countersLine = countersMatch[0];
      total += getAttr(countersLine, 'total');
      passed += getAttr(countersLine, 'passed');
      failed += getAttr(countersLine, 'failed') + getAttr(countersLine, 'error') + getAttr(countersLine, 'timeout') + getAttr(countersLine, 'aborted');
      skipped += getAttr(countersLine, 'notExecuted');
    }

    const timesMatch = content.match(/<Times [^>]*start="([^"]+)" [^>]*finish="([^"]+)"/);
    if (timesMatch) {
      if (!startTime || new Date(timesMatch[1]) < new Date(startTime)) startTime = timesMatch[1];
      if (!finishTime || new Date(timesMatch[2]) > new Date(finishTime)) finishTime = timesMatch[2];
    }
  }

  let durationStr = 'N/A';
  if (startTime && finishTime) {
    const durationMs = new Date(finishTime) - new Date(startTime);
    durationStr = `${(durationMs / 1000).toFixed(2)}s`;
  }

  // Parse Cobertura coverage reports
  // Coverage files are in subdirectories: TestResults/<guid>/coverage.cobertura.xml
  const findCoverageFiles = (dir) => {
    const results = [];
    if (!fs.existsSync(dir)) return results;
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      if (entry.isDirectory()) {
        results.push(...findCoverageFiles(path.join(dir, entry.name)));
      } else if (entry.name === 'coverage.cobertura.xml') {
        results.push(path.join(dir, entry.name));
      }
    }
    return results;
  };

  const coverageFiles = findCoverageFiles(resultsDir);
  let coverageSection = '';

  if (coverageFiles.length > 0) {
    let totalLines = 0, coveredLines = 0;
    let totalBranches = 0, coveredBranches = 0;
    const packageStats = [];

    for (const file of coverageFiles) {
      const content = fs.readFileSync(file, 'utf8');

      // Parse overall coverage from <coverage> element
      const coverageMatch = content.match(/<coverage[^>]*>/);
      if (coverageMatch) {
        const el = coverageMatch[0];
        const lv = el.match(/lines-valid="(\d+)"/);
        const lc = el.match(/lines-covered="(\d+)"/);
        const bv = el.match(/branches-valid="(\d+)"/);
        const bc = el.match(/branches-covered="(\d+)"/);
        if (lv) totalLines += parseInt(lv[1]);
        if (lc) coveredLines += parseInt(lc[1]);
        if (bv) totalBranches += parseInt(bv[1]);
        if (bc) coveredBranches += parseInt(bc[1]);
      }

      // Parse per-package (namespace) coverage
      const packageRegex = /<package[^>]*name="([^"]*)"[^>]*line-rate="([^"]*)"[^>]*branch-rate="([^"]*)"[^>]*>/g;
      let pkgMatch;
      while ((pkgMatch = packageRegex.exec(content)) !== null) {
        packageStats.push({
          name: pkgMatch[1],
          lineRate: parseFloat(pkgMatch[2]),
          branchRate: parseFloat(pkgMatch[3])
        });
      }
    }

    const lineRate = totalLines > 0 ? (coveredLines / totalLines * 100).toFixed(1) : 'N/A';
    const branchRate = totalBranches > 0 ? (coveredBranches / totalBranches * 100).toFixed(1) : 'N/A';

    const coverageRows = [
      '',
      '### Code Coverage',
      '',
      '| Metric | Value |',
      '| --- | --- |',
      `| **Line Coverage** | ${lineRate}% (${coveredLines}/${totalLines}) |`,
      `| **Branch Coverage** | ${branchRate}% (${coveredBranches}/${totalBranches}) |`
    ];

    if (packageStats.length > 0) {
      // Sort by name and deduplicate
      const seen = new Set();
      const uniquePackages = packageStats.filter(p => {
        if (seen.has(p.name)) return false;
        seen.add(p.name);
        return true;
      }).sort((a, b) => a.name.localeCompare(b.name));

      coverageRows.push(
        '',
        '<details>',
        '<summary>Coverage by namespace</summary>',
        '',
        '| Namespace | Line % | Branch % |',
        '| --- | --- | --- |'
      );
      for (const pkg of uniquePackages) {
        coverageRows.push(`| ${pkg.name} | ${(pkg.lineRate * 100).toFixed(1)}% | ${(pkg.branchRate * 100).toFixed(1)}% |`);
      }
      coverageRows.push('', '</details>');
    }

    coverageSection = coverageRows.join('\n');
  }

  const status = failed > 0 ? '❌ Failed' : '✅ Passed';
  const header = '### Test Results Summary';
  const marker = '<!-- test-results-summary -->';
  const body = [
    header,
    '',
    '| Metric | Count |',
    '| --- | --- |',
    `| **Status** | ${status} |`,
    `| **Total** | ${total} |`,
    `| **Passed** | ${passed} |`,
    `| **Failed** | ${failed} |`,
    `| **Skipped** | ${skipped} |`,
    `| **Duration** | ${durationStr} |`,
    coverageSection,
    '',
    `*Workflow run: [${context.runId}](https://github.com/${context.repo.owner}/${context.repo.repo}/actions/runs/${context.runId})*`,
    '',
    marker
  ].join('\n');

  const { data: comments } = await github.rest.issues.listComments({
    owner: context.repo.owner,
    repo: context.repo.repo,
    issue_number: context.payload.pull_request.number,
    per_page: 100
  });

  const botComment = comments.slice().reverse().find(comment => comment.body.includes(marker));

  if (botComment) {
    await github.rest.issues.updateComment({
      owner: context.repo.owner,
      repo: context.repo.repo,
      comment_id: botComment.id,
      body: body
    });
  } else {
    await github.rest.issues.createComment({
      owner: context.repo.owner,
      repo: context.repo.repo,
      issue_number: context.payload.pull_request.number,
      body: body
    });
  }
}
