module.exports = {
    platform: 'github',
    token: process.env.TOKEN,
    hostRules: [
        {
            "rebaseWhen": "auto",
            "platformAutomerge": true,
            "assignees": ["chase@cannect.app"],
        },
    ],
    repositories: ['ChaseFlorell/CodeToNeo4j'],
}
