namespace CodeToNeo4j.ProgramOptions;

public interface IOptionsHandler
{
    Task Handle(Options options);
    void SetNext(IOptionsHandler next);
}