using System.Diagnostics;
using OpenTelemetry;

namespace BookStore.Api.Diagnostics;

internal sealed class TracePrivacyProcessor : BaseProcessor<Activity>
{
    private readonly ActivityListener _exceptionListener = new()
    {
        ShouldListenTo = source => source.Name == "Npgsql",
        ExceptionRecorder = (Activity activity, Exception exception, ref TagList tags) =>
        {
            tags = new TagList
            {
                { "exception.type", exception.GetType().FullName },
                { "exception.message", "[redacted]" },
                { "exception.stacktrace", "[redacted]" }
            };
        }
    };

    public TracePrivacyProcessor()
    {
        ActivitySource.AddActivityListener(_exceptionListener);
    }

    public override void OnEnd(Activity activity)
    {
        activity.SetTag("url.query", null);
        activity.SetTag("url.full", null);
        activity.SetTag("http.url", null);
        activity.SetTag("http.target", null);

        if (activity.Source.Name == "Npgsql")
        {
            activity.SetTag("db.npgsql.data_source", null);
            activity.SetStatus(activity.Status);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _exceptionListener.Dispose();

        base.Dispose(disposing);
    }
}