module.exports = async ({ github, context }) => {
  const fs = require('fs');
  const path = require('path');
  const resultsDir = path.join(process.cwd(), 'TestResults');

  if (!fs.existsSync(resultsDir)) {
    console.log('No TestResults directory found.');
    return;
  }

  const files = fs.readdirSync(resultsDir).filter(f => f.endsWith('.trx'));
  if (files.length === 0) {
    console.log('No .trx files found in TestResults.');
    return;
  }

  let total = 0, passed = 0, failed = 0, skipped = 0;
  let startTime, finishTime;

  const getAttr = (line, attr) => {
    const match = line.match(new RegExp(`${attr}="(\\d+)"`));
    return match ? parseInt(match[1]) : 0;
  };

  for (const file of files) {
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
