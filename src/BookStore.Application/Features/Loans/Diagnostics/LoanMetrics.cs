using System.Diagnostics.Metrics;

namespace BookStore.Application.Features.Loans.Diagnostics;

public sealed class LoanMetrics
{
    public const string MeterName = "BookStore.Loans";

    private readonly Counter<long> _created;
    private readonly Counter<long> _unavailableRejections;
    private readonly Counter<long> _idempotentReplays;

    public LoanMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _created = meter.CreateCounter<long>(
            "bookstore.loans.created", "{loan}", "Empréstimos com commit confirmado.");
        _unavailableRejections = meter.CreateCounter<long>(
            "bookstore.loans.rejected.unavailable", "{request}", "Rejeições por falta de quantidade disponível.");
        _idempotentReplays = meter.CreateCounter<long>(
            "bookstore.loans.idempotent.replays", "{request}", "Repetições idempotentes com dados equivalentes.");
    }

    public void RecordCreated()
    {
        _created.Add(1);
    }

    public void RecordUnavailableRejection()
    {
        _unavailableRejections.Add(1);
    }

    public void RecordIdempotentReplay()
    {
        _idempotentReplays.Add(1);
    }
}