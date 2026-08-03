module.exports = async () => {
	const fs = require('fs');
	const path = require('path');

	// ── .NET results ────────────────────────────────────────────────────────────

	const resultsDir = path.join(process.cwd(), 'TestResults');
	let dotnetTotal = 0, dotnetPassed = 0, dotnetFailed = 0, dotnetSkipped = 0;
	let dotnetStartTime, dotnetFinishTime;

	const getAttr = (line, attr) => {
		const match = line.match(new RegExp(`${attr}="(\\d+)"`));
		return match ? parseInt(match[1]) : 0;
	};

	if (fs.existsSync(resultsDir)) {
		const trxFiles = fs.readdirSync(resultsDir).filter(f => f.endsWith('.trx'));
		for (const file of trxFiles) {
			const content = fs.readFileSync(path.join(resultsDir, file), 'utf8');
			const countersMatch = content.match(/<Counters [^>]*\/>/);
			if (countersMatch) {
				const cl = countersMatch[0];
				dotnetTotal += getAttr(cl, 'total');
				dotnetPassed += getAttr(cl, 'passed');
				dotnetFailed += getAttr(cl, 'failed') + getAttr(cl, 'error') + getAttr(cl, 'timeout') + getAttr(cl, 'aborted');
				dotnetSkipped += getAttr(cl, 'notExecuted');
			}
			const timesMatch = content.match(/<Times [^>]*start="([^"]+)" [^>]*finish="([^"]+)"/);
			if (timesMatch) {
				if (!dotnetStartTime || new Date(timesMatch[1]) < new Date(dotnetStartTime)) dotnetStartTime = timesMatch[1];
				if (!dotnetFinishTime || new Date(timesMatch[2]) > new Date(dotnetFinishTime)) dotnetFinishTime = timesMatch[2];
			}
		}
	}

	let dotnetDurationStr = 'N/A';
	if (dotnetStartTime && dotnetFinishTime) {
		const ms = new Date(dotnetFinishTime) - new Date(dotnetStartTime);
		dotnetDurationStr = `${(ms / 1000).toFixed(2)}s`;
	}

	// ── Dart results ─────────────────────────────────────────────────────────────

	const dartResultsFile = path.join(process.cwd(), 'tools', 'dart-analyzer', 'dart-test-results.json');
	let dartTotal = 0, dartPassed = 0, dartFailed = 0, dartSkipped = 0, dartDurationMs = 0;
	let dartHasResults = false;

	if (fs.existsSync(dartResultsFile)) {
		dartHasResults = true;
		const lines = fs.readFileSync(dartResultsFile, 'utf8').split('\n').filter(l => l.trim());
		for (const line of lines) {
			try {
				const event = JSON.parse(line);
				if (event.type === 'testDone' && !event.hidden) {
					dartTotal++;
					if (event.skipped) {
						dartSkipped++;
					} else if (event.result === 'success') {
						dartPassed++;
					} else {
						dartFailed++;
					}
				} else if (event.type === 'done') {
					dartDurationMs = event.time || 0;
				}
			} catch { /* skip malformed lines */
			}
		}
	}

	const dartDurationStr = dartHasResults ? `${(dartDurationMs / 1000).toFixed(2)}s` : 'N/A';

	// ── TypeScript (ts-analyzer) results ──────────────────────────────────────────

	const tsResultsFile = path.join(process.cwd(), 'tools', 'ts-analyzer', 'ts-test-results.xml');
	let tsTotal = 0, tsPassed = 0, tsFailed = 0, tsSkipped = 0, tsDurationMs = 0;
	let tsHasResults = false;

	const getComment = (content, name) => {
		const match = content.match(new RegExp(`<!-- ${name} ([\\d.]+) -->`));
		return match ? parseFloat(match[1]) : 0;
	};

	if (fs.existsSync(tsResultsFile)) {
		tsHasResults = true;
		const content = fs.readFileSync(tsResultsFile, 'utf8');
		tsTotal = getComment(content, 'tests');
		tsPassed = getComment(content, 'pass');
		tsFailed = getComment(content, 'fail') + getComment(content, 'cancelled');
		tsSkipped = getComment(content, 'skipped');
		tsDurationMs = getComment(content, 'duration_ms');
	}

	const tsDurationStr = tsHasResults ? `${(tsDurationMs / 1000).toFixed(2)}s` : 'N/A';

	// ── Combined table ────────────────────────────────────────────────────────────

	const suiteRow = (label, passed, failed, skipped, total, duration) => {
		const status = failed > 0 ? '❌ Failed' : '✅ Passed';
		return `| ${label} | ${status} | ${total} | ${passed} | ${failed} | ${skipped} | ${duration} |`;
	};

	const totalPassed = dotnetPassed + dartPassed + tsPassed;
	const totalFailed = dotnetFailed + dartFailed + tsFailed;
	const totalSkipped = dotnetSkipped + dartSkipped + tsSkipped;
	const totalTests = dotnetTotal + dartTotal + tsTotal;
	const overallStatus = totalFailed > 0 ? '❌ Failed' : '✅ Passed';

	const tableRows = [
		'| Suite | Status | Total | Passed | Failed | Skipped | Duration |',
		'| --- | --- | --- | --- | --- | --- | --- |',
		suiteRow('.NET', dotnetPassed, dotnetFailed, dotnetSkipped, dotnetTotal, dotnetDurationStr),
	];
	if (dartHasResults) {
		tableRows.push(suiteRow('Dart', dartPassed, dartFailed, dartSkipped, dartTotal, dartDurationStr));
	}
	if (tsHasResults) {
		tableRows.push(suiteRow('TypeScript', tsPassed, tsFailed, tsSkipped, tsTotal, tsDurationStr));
	}
	tableRows.push(`| **Total** | **${overallStatus}** | **${totalTests}** | **${totalPassed}** | **${totalFailed}** | **${totalSkipped}** | — |`);

	// ── .NET code coverage ────────────────────────────────────────────────────────

	const findCoverageFiles = (dir) => {
		const results = [];
		if (!fs.existsSync(dir)) return results;
		for (const entry of fs.readdirSync(dir, {withFileTypes: true})) {
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
			const packageRegex = /<package[^>]*name="([^"]*)"[^>]*line-rate="([^"]*)"[^>]*branch-rate="([^"]*)"[^>]*>/g;
			let pkgMatch;
			while ((pkgMatch = packageRegex.exec(content)) !== null) {
				packageStats.push({name: pkgMatch[1], lineRate: parseFloat(pkgMatch[2]), branchRate: parseFloat(pkgMatch[3])});
			}
		}

		const lineRate = totalLines > 0 ? (coveredLines / totalLines * 100).toFixed(1) : 'N/A';
		const branchRate = totalBranches > 0 ? (coveredBranches / totalBranches * 100).toFixed(1) : 'N/A';

		const coverageRows = [
			'',
			'### .NET Code Coverage',
			'',
			'| Metric | Value |',
			'| --- | --- |',
			`| **Line Coverage** | ${lineRate}% (${coveredLines}/${totalLines}) |`,
			`| **Branch Coverage** | ${branchRate}% (${coveredBranches}/${totalBranches}) |`
		];

		if (packageStats.length > 0) {
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

	// ── TypeScript code coverage ────────────────────────────────────────────────

	const tsLcovFile = path.join(process.cwd(), 'tools', 'ts-analyzer', 'coverage', 'lcov.info');
	let tsCoverageSection = '';

	if (fs.existsSync(tsLcovFile)) {
		const content = fs.readFileSync(tsLcovFile, 'utf8');
		let totalLines = 0, coveredLines = 0;
		let totalBranches = 0, coveredBranches = 0;
		const fileStats = [];

		let currentFile = null, fileLinesFound = 0, fileLinesHit = 0, fileBranchesFound = 0, fileBranchesHit = 0;
		const flushFile = () => {
			if (currentFile) {
				fileStats.push({
					name: currentFile,
					lineRate: fileLinesFound > 0 ? fileLinesHit / fileLinesFound : 0,
					branchRate: fileBranchesFound > 0 ? fileBranchesHit / fileBranchesFound : 0
				});
			}
		};

		for (const line of content.split('\n')) {
			if (line.startsWith('SF:')) {
				flushFile();
				currentFile = line.slice(3).trim();
				fileLinesFound = 0; fileLinesHit = 0; fileBranchesFound = 0; fileBranchesHit = 0;
			} else if (line.startsWith('LF:')) {
				fileLinesFound = parseInt(line.slice(3)) || 0;
				totalLines += fileLinesFound;
			} else if (line.startsWith('LH:')) {
				fileLinesHit = parseInt(line.slice(3)) || 0;
				coveredLines += fileLinesHit;
			} else if (line.startsWith('BRF:')) {
				fileBranchesFound = parseInt(line.slice(4)) || 0;
				totalBranches += fileBranchesFound;
			} else if (line.startsWith('BRH:')) {
				fileBranchesHit = parseInt(line.slice(4)) || 0;
				coveredBranches += fileBranchesHit;
			}
		}
		flushFile();

		const lineRate = totalLines > 0 ? (coveredLines / totalLines * 100).toFixed(1) : 'N/A';
		const branchRate = totalBranches > 0 ? (coveredBranches / totalBranches * 100).toFixed(1) : 'N/A';

		const tsCoverageRows = [
			'',
			'### TypeScript Code Coverage',
			'',
			'| Metric | Value |',
			'| --- | --- |',
			`| **Line Coverage** | ${lineRate}% (${coveredLines}/${totalLines}) |`,
			`| **Branch Coverage** | ${branchRate}% (${coveredBranches}/${totalBranches}) |`
		];

		if (fileStats.length > 0) {
			tsCoverageRows.push(
				'',
				'<details>',
				'<summary>Coverage by file</summary>',
				'',
				'| File | Line % | Branch % |',
				'| --- | --- | --- |'
			);
			for (const file of fileStats.sort((a, b) => a.name.localeCompare(b.name))) {
				tsCoverageRows.push(`| ${file.name} | ${(file.lineRate * 100).toFixed(1)}% | ${(file.branchRate * 100).toFixed(1)}% |`);
			}
			tsCoverageRows.push('', '</details>');
		}

		tsCoverageSection = tsCoverageRows.join('\n');
	}

	coverageSection = [coverageSection, tsCoverageSection].filter(Boolean).join('\n');

	// ── Write summary artifact ──────────────────────────────────────────────────

	fs.writeFileSync(
		path.join(process.cwd(), 'test-summary.json'),
		JSON.stringify({tableRows, coverageSection}, null, 2)
	);
}
