using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace PaymentSimulator.Data;

public static class PaymentDatabaseMigration
{
    private const string InitialMigration = "20261022080921_InitialCreate";

    public static async Task MigrateAsync(PaymentDbContext db, CancellationToken cancellationToken = default)
    {
        var creator = db.GetService<IRelationalDatabaseCreator>();
        if (!await creator.ExistsAsync(cancellationToken))
        {
            try { await creator.CreateAsync(cancellationToken); }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.DuplicateDatabase) { }
        }
        // Older versions used EnsureCreated. Adopt only the exact original schema;
        // retain all payment rows and let normal EF migrations perform the upgrade.
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(724091631)", cancellationToken);
            // Npgsql may probe a missing history table by catching undefined_table. Inside
            // this transaction that would leave PostgreSQL aborted, so probe safely first.
            var hasHistory = await db.Database.SqlQueryRaw<bool>("""
                SELECT to_regclass('public."__EFMigrationsHistory"') IS NOT NULL AS "Value"
                """).SingleAsync(cancellationToken);
            var applied = hasHistory
                ? await db.Database.GetAppliedMigrationsAsync(cancellationToken)
                : Enumerable.Empty<string>();
            if (!applied.Any())
            {
                var exists = await db.Database.SqlQueryRaw<bool>(
                    """
                    SELECT to_regclass('public."Payments"') IS NOT NULL AS "Value"
                    """
                ).SingleAsync(cancellationToken);
                if (exists)
                {
                    var matches = await db.Database.SqlQueryRaw<bool>("""
                        WITH expected(name, type, required) AS (VALUES
                            ('Id', 'uuid', true), ('BookingId', 'uuid', true),
                            ('Amount', 'numeric(18,2)', true), ('Currency', 'character varying(3)', true),
                            ('Status', 'character varying(20)', true),
                            ('CreatedAt', 'timestamp with time zone', true),
                            ('CompletedAt', 'timestamp with time zone', false)),
                        actual AS (SELECT a.attname::text AS name,
                            format_type(a.atttypid, a.atttypmod) AS type, a.attnotnull AS required
                            FROM pg_attribute a WHERE a.attrelid = 'public."Payments"'::regclass
                                AND a.attnum > 0 AND NOT a.attisdropped)
                        SELECT NOT EXISTS ((SELECT * FROM expected EXCEPT SELECT * FROM actual)
                            UNION ALL (SELECT * FROM actual EXCEPT SELECT * FROM expected))
                            AND EXISTS (SELECT 1 FROM pg_constraint c
                                WHERE c.conrelid = 'public."Payments"'::regclass AND c.contype = 'p'
                                AND c.conkey = ARRAY[(SELECT attnum FROM pg_attribute
                                    WHERE attrelid = c.conrelid AND attname = 'Id')]::smallint[])
                            AND EXISTS (SELECT 1 FROM pg_index i
                                WHERE i.indrelid = 'public."Payments"'::regclass
                                AND i.indisunique AND i.indisvalid AND i.indpred IS NULL
                                AND i.indexprs IS NULL AND i.indnkeyatts = 1
                                AND i.indkey[0] = (SELECT attnum FROM pg_attribute
                                    WHERE attrelid = i.indrelid AND attname = 'BookingId')) AS "Value"
                        """).SingleAsync(cancellationToken);
                    if (!matches)
                        throw new InvalidOperationException("Payments exists without migration history, but its schema "
                            + "does not match InitialCreate. Verify the database schema before baselining migrations.");
                    var history = db.GetService<IHistoryRepository>();
                    await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), cancellationToken);
                    await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(
                        new HistoryRow(InitialMigration, "10.0.12")), cancellationToken);
                }
            }
            await transaction.CommitAsync(cancellationToken);
        }
        await db.Database.MigrateAsync(cancellationToken);
    }
}
