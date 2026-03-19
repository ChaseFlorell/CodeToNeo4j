namespace CodeToNeo4j.ProgramOptions.Handlers;

public interface IOptionsHandler
{
	Task Handle(Options options);
	void SetNext(IOptionsHandler next);
}
