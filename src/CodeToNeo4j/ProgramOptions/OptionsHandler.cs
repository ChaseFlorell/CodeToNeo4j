namespace CodeToNeo4j.ProgramOptions;

public record HandlerContext(IServiceProvider? ServiceProvider = null);

public abstract class OptionsHandler : IOptionsHandler
{
    public virtual async Task Handle(Options options, HandlerContext context)
    {
        if (Next != null)
        {
            await Next.Handle(options, context);
        }
    }

    public void SetNext(IOptionsHandler next) => Next = next;

    protected IOptionsHandler? Next { get; private set; }
}
