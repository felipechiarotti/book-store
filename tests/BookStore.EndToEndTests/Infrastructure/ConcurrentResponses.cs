namespace BookStore.EndToEndTests.Infrastructure;

internal sealed record ConcurrentResponses(HttpResponseMessage First, HttpResponseMessage Second) : IDisposable
{
    public void Dispose()
    {
        First.Dispose();
        Second.Dispose();
    }
}