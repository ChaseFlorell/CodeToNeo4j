module.exports = async ({github, context, prNumber}) => {
	const fs = require('fs');
	const path = require('path');

	const {tableRows, coverageSection} = JSON.parse(
		fs.readFileSync(path.join(process.cwd(), 'test-summary.json'), 'utf8')
	);

	// ── Compose comment ───────────────────────────────────────────────────────────

	const marker = '<!-- test-results-summary -->';
	const body = [
		'### Test Results Summary',
		'',
		...tableRows,
		coverageSection,
		'',
		`*Workflow run: [${context.runId}](https://github.com/${context.repo.owner}/${context.repo.repo}/actions/runs/${context.runId})*`,
		'',
		marker
	].join('\n');

	const {data: comments} = await github.rest.issues.listComments({
		owner: context.repo.owner,
		repo: context.repo.repo,
		issue_number: prNumber,
		per_page: 100
	});

	const botComment = comments.slice().reverse().find(c => c.body.includes(marker));

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
			issue_number: prNumber,
			body: body
		});
	}
}
