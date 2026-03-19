module.exports = {
	platform: 'github',
	token: process.env.TOKEN,
	gitAuthor: 'Chase Florell <chase.florell@gmail.com>',
	hostRules: [
		{
			"rebaseWhen": "auto",
			"platformAutomerge": true,
			"assignees": ["chase@cannect.app"],
		},
	],
	repositories: ['ChaseFlorell/CodeToNeo4j'],
}
