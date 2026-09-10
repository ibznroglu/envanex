using System.Collections.Frozen;
using DevExtreme.AspNet.Data;
using Envanex.Domain.Common;

namespace Envanex.Web.DataSource;

public sealed class DataSourceGuard
{
    private readonly FrozenSet<string> _allowedSortFields;
    private readonly FrozenSet<string> _allowedGroupFields;
    private readonly int _defaultTake;
    private readonly int _maxTake;
    private readonly int _maxSkip;

    public DataSourceGuard(
        IReadOnlySet<string> allowedSortFields,
        IReadOnlySet<string> allowedGroupFields,
        int defaultTake,
        int maxTake,
        int maxSkip = 10_000)
    {
        ArgumentNullException.ThrowIfNull(allowedSortFields);
        ArgumentNullException.ThrowIfNull(allowedGroupFields);

        _allowedSortFields = allowedSortFields.ToFrozenSet();
        _allowedGroupFields = allowedGroupFields.ToFrozenSet();
        _defaultTake = defaultTake;
        _maxTake = maxTake;
        _maxSkip = maxSkip;
    }

    public Result<DataSourceLoadOptionsBase> ValidateAndApply(DataSourceLoadOptionsBase options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Take < 0)
        {
            return Result.Failure<DataSourceLoadOptionsBase>(
                new Error("DataSource.TakeNegative", "Take cannot be negative."));
        }

        if (options.Take > _maxTake)
        {
            return Result.Failure<DataSourceLoadOptionsBase>(
                new Error("DataSource.TakeExceedsMax", $"Take cannot exceed {_maxTake}."));
        }

        if (options.Skip < 0)
        {
            return Result.Failure<DataSourceLoadOptionsBase>(
                new Error("DataSource.SkipNegative", "Skip cannot be negative."));
        }

        if (options.Skip > _maxSkip)
        {
            return Result.Failure<DataSourceLoadOptionsBase>(
                new Error("DataSource.SkipExceedsMax", $"Skip cannot exceed {_maxSkip}."));
        }

        if (options.Sort is { Length: > 0 })
        {
            foreach (var sort in options.Sort)
            {
                if (!_allowedSortFields.Contains(sort.Selector))
                {
                    return Result.Failure<DataSourceLoadOptionsBase>(
                        new Error("DataSource.DisallowedSortField", $"Sorting by '{sort.Selector}' is not allowed."));
                }
            }
        }

        if (options.Group is { Length: > 0 })
        {
            foreach (var group in options.Group)
            {
                if (!_allowedGroupFields.Contains(group.Selector))
                {
                    return Result.Failure<DataSourceLoadOptionsBase>(
                        new Error("DataSource.DisallowedGroupField", $"Grouping by '{group.Selector}' is not allowed."));
                }
            }
        }

        if (options.RequireGroupCount)
        {
            return Result.Failure<DataSourceLoadOptionsBase>(
                new Error("DataSource.RequireGroupCountNotAllowed", "RequireGroupCount is not allowed."));
        }

        if (options.GroupSummary is { Length: > 0 })
        {
            return Result.Failure<DataSourceLoadOptionsBase>(
                new Error("DataSource.GroupSummaryNotAllowed", "GroupSummary is not allowed."));
        }

        // Apply default take when not specified.
        // Take == 0 means the client did not provide a value; apply the default
        // to prevent unbounded queries.
        if (options.Take == 0)
        {
            options.Take = _defaultTake;
        }

        return Result.Success(options);
    }
}
