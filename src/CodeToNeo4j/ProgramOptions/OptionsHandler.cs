namespace CodeToNeo4j.ProgramOptions;

public abstract class OptionsHandler : IOptionsHandler
{
    public virtual async Task Handle(Options options)
    {
        if (_next != null)
        {
            await _next.Handle(options);
        }
    }

    public void SetNext(IOptionsHandler next) => _next = next;

    private IOptionsHandler? _next;
}