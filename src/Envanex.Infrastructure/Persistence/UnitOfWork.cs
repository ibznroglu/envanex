using System.Text.RegularExpressions;
using Envanex.Application.Abstractions.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Envanex.Infrastructure.Persistence;

internal sealed partial class UnitOfWork : IUnitOfWork
{
    private readonly EnvanexDbContext _context;

    public UnitOfWork(EnvanexDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(innerException: ex);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new DuplicateKeyException(ExtractConstraintName(ex), ex);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is SqlException { Number: 2601 or 2627 };
    }

    // Parses the index or constraint name from a SQL Server 2601/2627 error message.
    // This depends on SQL Server's English message text and may fail on localized servers,
    // so null is a normal result. ConstraintName is for diagnostics only, never for
    // control flow decisions.
    private static string? ExtractConstraintName(DbUpdateException ex)
    {
        if (ex.InnerException is SqlException sqlEx)
        {
            var match = ConstraintNameRegex().Match(sqlEx.Message);
            return match.Success ? match.Groups[1].Value : null;
        }

        return null;
    }

    [GeneratedRegex(@"(?:index|constraint)\s+'([^']+)'", RegexOptions.IgnoreCase)]
    private static partial Regex ConstraintNameRegex();
}
