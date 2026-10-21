using Npgsql;

namespace BookStore.EndToEndTests.Infrastructure;

internal sealed class DatabaseLockGate : IAsyncDisposable
{
    private const long InsertLockKey = 417031;

    private const string DropInsertTrigger = """
                                             DROP TRIGGER IF EXISTS bookstore_test_insert_gate ON loans;
                                             DROP FUNCTION IF EXISTS bookstore_test_insert_gate();
                                             """;

    private readonly NpgsqlConnection _blocker;
    private readonly NpgsqlConnection _observer;
    private NpgsqlTransaction? _transaction;
    private bool _insertTriggerCreated;
    private bool _released;

    private DatabaseLockGate(string connectionString)
    {
        _blocker = new NpgsqlConnection(connectionString);
        _observer = new NpgsqlConnection(connectionString);
    }

    internal static async Task<DatabaseLockGate> HoldBookAsync(
        string connectionString, Guid bookId, CancellationToken cancellationToken)
    {
        var gate = new DatabaseLockGate(connectionString);
        try
        {
            await gate.InitializeAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                "SELECT \"Id\" FROM books WHERE \"Id\" = @bookId FOR UPDATE;",
                gate._blocker, gate._transaction);
            command.Parameters.AddWithValue("bookId", bookId);
            if (await command.ExecuteScalarAsync(cancellationToken) is not Guid)
                throw new InvalidOperationException("O livro da disputa precisa existir.");
            return gate;
        }
        catch
        {
            await gate.DisposeAsync();
            throw;
        }
    }

    internal static async Task<DatabaseLockGate> HoldLoanInsertsAsync(
        string connectionString, CancellationToken cancellationToken)
    {
        var gate = new DatabaseLockGate(connectionString);
        try
        {
            await gate.InitializeAsync(cancellationToken);
            await using var blocker = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(@key);", gate._blocker, gate._transaction);
            blocker.Parameters.AddWithValue("key", InsertLockKey);
            await blocker.ExecuteNonQueryAsync(cancellationToken);

            await using var trigger = new NpgsqlCommand("""
                                                        CREATE FUNCTION bookstore_test_insert_gate() RETURNS trigger LANGUAGE plpgsql AS $$
                                                        BEGIN
                                                            PERFORM pg_advisory_xact_lock_shared(417031);
                                                            RETURN NEW;
                                                        END;
                                                        $$;
                                                        CREATE TRIGGER bookstore_test_insert_gate BEFORE INSERT ON loans
                                                        FOR EACH ROW EXECUTE FUNCTION bookstore_test_insert_gate();
                                                        """, gate._observer);
            gate._insertTriggerCreated = true;
            await trigger.ExecuteNonQueryAsync(cancellationToken);
            return gate;
        }
        catch
        {
            await gate.DisposeAsync();
            throw;
        }
    }

    internal async Task<ConcurrentResponses> ExecuteAsync(
        Func<Task<HttpResponseMessage>> firstRequest,
        Func<Task<HttpResponseMessage>> secondRequest,
        CancellationToken cancellationToken)
    {
        List<Task<HttpResponseMessage>> requests = [];
        try
        {
            requests.Add(firstRequest());
            requests.Add(secondRequest());
            await WaitForBlockedApisAsync(cancellationToken);
            await ReleaseAsync(cancellationToken);
            await Task.WhenAll(requests);
            return new ConcurrentResponses(await requests[0], await requests[1]);
        }
        catch
        {
            await ReleaseAsync(CancellationToken.None);
            try
            {
                await Task.WhenAll(requests);
            }
            catch
            {
            }

            foreach (var request in requests.Where(request => request.IsCompletedSuccessfully))
                (await request).Dispose();
            throw;
        }
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _blocker.OpenAsync(cancellationToken);
        await _observer.OpenAsync(cancellationToken);
        _transaction = await _blocker.BeginTransactionAsync(cancellationToken);
    }

    private async Task WaitForBlockedApisAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var polling = new PeriodicTimer(TimeSpan.FromMilliseconds(25));
        await using var command = new NpgsqlCommand("""
                                                    WITH RECURSIVE blocked(pid) AS (
                                                        SELECT pid FROM pg_stat_activity WHERE @blocker = ANY(pg_blocking_pids(pid))
                                                        UNION
                                                        SELECT activity.pid FROM pg_stat_activity activity
                                                        JOIN blocked ON blocked.pid = ANY(pg_blocking_pids(activity.pid))
                                                    )
                                                    SELECT COUNT(DISTINCT activity.application_name)
                                                    FROM pg_stat_activity activity JOIN blocked USING (pid)
                                                    WHERE activity.datname = current_database()
                                                      AND activity.application_name = ANY(@applications);
                                                    """, _observer);
        command.Parameters.AddWithValue("blocker", _blocker.ProcessID);
        command.Parameters.AddWithValue("applications", new[]
        {
            BookStoreFixture.PrimaryApplicationName, BookStoreFixture.SecondaryApplicationName
        });

        try
        {
            do
            {
                if (await command.ExecuteScalarAsync(timeout.Token) is 2L)
                    return;
            } while (await polling.WaitForNextTickAsync(timeout.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("As duas APIs não foram observadas aguardando a transação de controle em 10s.");
        }
    }

    private async Task ReleaseAsync(CancellationToken cancellationToken)
    {
        if (_released || _transaction is null)
            return;
        await _transaction.CommitAsync(cancellationToken);
        _released = true;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_transaction is not null)
                await _transaction.DisposeAsync();
            if (_insertTriggerCreated)
            {
                await using var command = new NpgsqlCommand(DropInsertTrigger, _observer);
                await command.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            await Task.WhenAll(_observer.DisposeAsync().AsTask(), _blocker.DisposeAsync().AsTask());
        }
    }
}