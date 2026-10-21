using System.Data;
using Npgsql;

namespace BookStore.EndToEndTests.Infrastructure;

internal sealed class AuditInsertFailure : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private bool _disposed;

    private AuditInsertFailure(string connectionString)
    {
        _connection = new NpgsqlConnection(connectionString);
    }

    internal static async Task<AuditInsertFailure> InstallAsync(string connectionString,
        CancellationToken cancellationToken)
    {
        var failure = new AuditInsertFailure(connectionString);
        try
        {
            await failure._connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("""
                                                        CREATE FUNCTION bookstore_test_audit_failure() RETURNS trigger LANGUAGE plpgsql AS $$
                                                        BEGIN
                                                            IF NEW."EntityName" = 'Loan' AND NEW."Action" = 'Created' THEN
                                                                RAISE EXCEPTION 'audit-insert-failure' USING ERRCODE = 'P0001';
                                                            END IF;
                                                            RETURN NEW;
                                                        END;
                                                        $$;
                                                        CREATE CONSTRAINT TRIGGER bookstore_test_audit_failure AFTER INSERT ON audit_events
                                                        DEFERRABLE INITIALLY DEFERRED
                                                        FOR EACH ROW EXECUTE FUNCTION bookstore_test_audit_failure();
                                                        """, failure._connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return failure;
        }
        catch
        {
            await failure.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            if (_connection.State == ConnectionState.Open)
            {
                await using var command = new NpgsqlCommand("""
                                                            DROP TRIGGER IF EXISTS bookstore_test_audit_failure ON audit_events;
                                                            DROP FUNCTION IF EXISTS bookstore_test_audit_failure();
                                                            """, _connection);
                await command.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            await _connection.DisposeAsync();
        }
    }
}