namespace CodeToNeo4j.ProgramOptions;

public abstract class OptionsHandler : IOptionsHandler
{
    public virtual async Task Handle(Options options)
    {
        if (Next != null)
        {
            await Next.Handle(options);
        }
    }

    public void SetNext(IOptionsHandler next) => Next = next;

    protected IOptionsHandler? Next { get; private set; }
}