namespace CodeToNeo4j.Configuration;

public record HandlerConfiguration(
	string[] FileExtensions,
	string Language,
	string KindPrefix = "");
