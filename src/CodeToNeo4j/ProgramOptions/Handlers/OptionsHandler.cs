namespace CodeToNeo4j.ProgramOptions.Handlers;

public abstract class OptionsHandler : IOptionsHandler
{
    public async Task Handle(Options options)
    {
        var handleNext = await HandleOptions(options);
        if (handleNext)
        {
            await HandleInternal(options);
        }
    }

    public void SetNext(IOptionsHandler next) => _next = next;

    protected abstract Task<bool> HandleOptions(Options options);

    private IOptionsHandler? _next;

    private async Task HandleInternal(Options options)
    {
        if (_next != null)
        {
            await _next.Handle(options);
        }
    }
}