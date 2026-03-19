namespace SqlTxt.Engine;

/// <summary>
/// Chains multiple disposables; releases in reverse order.
/// </summary>
internal sealed class CompositeSchemaAndTableLocks : IAsyncDisposable
{
    private readonly IAsyncDisposable _inner;
    private readonly IAsyncDisposable _schemaRead;

    internal CompositeSchemaAndTableLocks(IAsyncDisposable schemaRead, IAsyncDisposable inner)
    {
        _schemaRead = schemaRead;
        _inner = inner;
    }

    public async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync().ConfigureAwait(false);
        await _schemaRead.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// No-op release for NOLOCK paths.
/// </summary>
internal sealed class NoopAsyncDisposable : IAsyncDisposable
{
    internal static readonly NoopAsyncDisposable Instance = new();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
