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

    protected abstract Task<bool> HandleOptions(Options options);

    private async Task HandleInternal(Options options)
    {
        if (_next != null)
        {
            await _next.Handle(options);
        }
    }

    public void SetNext(IOptionsHandler next) => _next = next;

    private IOptionsHandler? _next;
}