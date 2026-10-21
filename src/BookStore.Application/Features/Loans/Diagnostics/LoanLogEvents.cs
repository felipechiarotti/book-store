using Microsoft.Extensions.Logging;

namespace BookStore.Application.Features.Loans.Diagnostics;

internal static class LoanLogEvents
{
    internal static readonly EventId Created = new(2000, "LoanCreated");
    internal static readonly EventId Replayed = new(2001, "LoanReplayed");
    internal static readonly EventId Unavailable = new(2002, "LoanUnavailable");
    internal static readonly EventId Rejected = new(2003, "LoanRejected");
    internal static readonly EventId Closed = new(2004, "LoanClosed");
    internal static readonly EventId Unchanged = new(2005, "LoanUnchanged");
    internal static readonly EventId ClosureRejected = new(2006, "LoanClosureRejected");
}
