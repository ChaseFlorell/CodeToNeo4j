namespace CodeToNeo4j.Configuration;

public record HandlerConfiguration(
	string[] FileExtensions,
	string Language,
	string Technology = "unknown",
	string KindPrefix = "");
