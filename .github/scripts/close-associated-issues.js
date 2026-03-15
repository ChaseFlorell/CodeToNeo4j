module.exports = async ({github, context}) => {
  const pr = context.payload.pull_request;
  const body = pr.body;
  if (!body) {
    console.log('No PR body to parse.');
    return;
  }

  // Only close issues when the "resolves linked issue" checkbox is checked
  const resolveChecked = /- \[x\] This PR resolves the linked issue/i.test(body);
  if (!resolveChecked) {
    console.log('Resolve checkbox is unchecked. Skipping issue closure.');
    return;
  }

  // GitHub standard keywords for closing issues:
  // close, closes, closed, fix, fixes, fixed, resolve, resolves, resolved
  // regex to match "Keyword #123" or "Keyword: #123"
  const regex = /(?:close|closes|closed|fix|fixes|fixed|resolve|resolves|resolved)\s*[:]?\s*#(\d+)/gi;
  let match;
  const issueNumbers = new Set();

  while ((match = regex.exec(body)) !== null) {
    issueNumbers.add(parseInt(match[1]));
  }

  if (issueNumbers.size === 0) {
    console.log('No associated issues found in PR body.');
    return;
  }

  const isMerged = pr.merged;
  const stateReason = isMerged ? 'completed' : 'not_planned';
  const closureMessage = isMerged 
    ? `Automatically closed because PR #${pr.number} was merged.`
    : `Automatically closed because PR #${pr.number} was closed.`;

  for (const issueNumber of issueNumbers) {
    console.log(`Processing issue #${issueNumber}...`);
    try {
      // First, let's check if the issue exists and is not already closed
      const { data: issue } = await github.rest.issues.get({
        owner: context.repo.owner,
        repo: context.repo.repo,
        issue_number: issueNumber,
      });

      if (issue.state === 'closed') {
        console.log(`Issue #${issueNumber} is already closed. Skipping.`);
        continue;
      }

      // Add a comment explaining why it's being closed
      await github.rest.issues.createComment({
        owner: context.repo.owner,
        repo: context.repo.repo,
        issue_number: issueNumber,
        body: closureMessage
      });

      // Close the issue
      await github.rest.issues.update({
        owner: context.repo.owner,
        repo: context.repo.repo,
        issue_number: issueNumber,
        state: 'closed',
        state_reason: stateReason
      });
      console.log(`Successfully closed issue #${issueNumber} as ${stateReason}.`);
    } catch (error) {
      console.error(`Error processing issue #${issueNumber}: ${error.message}`);
    }
  }
};
