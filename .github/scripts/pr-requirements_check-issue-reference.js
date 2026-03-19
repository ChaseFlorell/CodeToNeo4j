module.exports = async ({ github, context, core }) => {
  const { data: pr } = await github.rest.pulls.get({
    owner: context.repo.owner,
    repo: context.repo.repo,
    pull_number: context.payload.pull_request.number
  });

  const body = pr.body || '';
  if (!/^Resolves #[0-9]+/m.test(body)) {
    core.setFailed("PR body must contain 'Resolves #<issue-number>' (e.g. 'Resolves #42').");
    return;
  }
  core.info('Issue reference found.');
};