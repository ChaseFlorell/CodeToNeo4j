namespace CodeToNeo4j.Configuration;

public record HandlerConfiguration(
	string FileExtension,
	string Language,
	string KindPrefix = "");
