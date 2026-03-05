namespace CodeToNeo4j.ProgramOptions;

public interface IOptionsHandler
{
    Task Handle(Options options, HandlerContext context);
    void SetNext(IOptionsHandler next);
}