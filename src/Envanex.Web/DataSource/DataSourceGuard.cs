using System.Collections;
using System.Collections.Frozen;
using DevExtreme.AspNet.Data;
using Envanex.Domain.Common;

namespace Envanex.Web.DataSource;

public sealed class DataSourceGuard
{
    private readonly FrozenSet<string> _allowedFields;
    private readonly FrozenSet<string> _allowedGroupFields;
    private readonly string _defaultSortSelector;
    private readonly int _defaultTake;
    private readonly int _maxTake;
    private readonly int _maxSkip;

    public DataSourceGuard(
        IReadOnlySet<string> allowedFields,
        IReadOnlySet<string> allowedGroupFields,
        string defaultSortSelector,
        int defaultTake,
        int maxTake,
        int maxSkip = 10_000)
    {
        ArgumentNullException.ThrowIfNull(allowedFields);
        ArgumentNullException.ThrowIfNull(allowedGroupFields);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultSortSelector);

        if (!allowedFields.Contains(defaultSortSelector))
        {
            throw new ArgumentException(
                $"The default sort selector '{defaultSortSelector}' must be one of the allowed fields.",
                nameof(defaultSortSelector));
        }

        _allowedFields = allowedFields.ToFrozenSet();
        _allowedGroupFields = allowedGroupFields.ToFrozenSet();
        _defaultSortSelector = defaultSortSelector;
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
                if (!_allowedFields.Contains(sort.Selector))
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

        if (options.Filter is { Count: > 0 })
        {
            var disallowedField = FindDisallowedFilterField(options.Filter);
            if (disallowedField is not null)
            {
                return Result.Failure<DataSourceLoadOptionsBase>(
                    new Error("DataSource.DisallowedFilterField", $"Filtering by '{disallowedField}' is not allowed."));
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

        // Give every request a deterministic total ordering.
        // The risk is not the absence of an ordering but its non-uniqueness: OFFSET/FETCH over
        // an ORDER BY whose key repeats leaves rows with equal keys in an undefined relative
        // order, so SQL Server may repeat a row on one page and skip it on the next. Most
        // allowlisted fields are non-unique, so a sort the client did supply is not enough on
        // its own. The default selector maps to a uniquely indexed column, so appending it
        // makes the ordering total. The client's own sort keys stay primary and are never
        // overridden or reordered; when the client supplied none, the tiebreaker is the whole
        // ordering.
        options.Sort = AppendTiebreaker(options.Sort);

        return Result.Success(options);
    }

    /// <summary>
    /// Returns the client's sort keys with the default selector appended as a final tiebreaker,
    /// or just the tiebreaker when the client supplied no sort at all. A client list that already
    /// sorts on the default selector is returned unchanged: it is already total.
    /// </summary>
    /// <remarks>
    /// Appending the default selector cannot bypass the sort allowlist — the constructor rejects
    /// a default selector that is not in <see cref="_allowedFields"/>.
    /// </remarks>
    private SortingInfo[] AppendTiebreaker(SortingInfo[]? sort)
    {
        var tiebreaker = new SortingInfo { Selector = _defaultSortSelector, Desc = false };

        if (sort is null or { Length: 0 })
        {
            return [tiebreaker];
        }

        foreach (var entry in sort)
        {
            if (string.Equals(entry.Selector, _defaultSortSelector, StringComparison.Ordinal))
            {
                return sort;
            }
        }

        return [.. sort, tiebreaker];
    }

    /// <summary>
    /// Recursively walks a DevExtreme filter structure and returns the first
    /// field name that is not in the allowlist, or null if all fields are allowed.
    /// </summary>
    /// <remarks>
    /// DevExtreme filter formats:
    /// - Simple condition: ["fieldName", "=", "value"] — IList with 3 elements, first is string
    /// - Unary operator: ["!", [...]] — IList with 2 elements, first is "!"
    /// - Compound: [cond1, "and"/"or", cond2, ...] — IList of alternating conditions and operators
    /// - Nested: conditions can be IList themselves, recursively
    /// </remarks>
    private string? FindDisallowedFilterField(IList filter)
    {
        // A simple condition: [fieldName, operator, value]
        // Detected by: first element is a string that is NOT a logical operator,
        // and the list has at least 2 elements (unary like ["!", cond] is handled below).
        if (filter.Count >= 2 && filter[0] is string fieldOrOp)
        {
            // "!" is a unary NOT operator; the second element is a sub-condition
            if (string.Equals(fieldOrOp, "!", StringComparison.Ordinal))
            {
                if (filter[1] is IList subFilter)
                {
                    return FindDisallowedFilterField(subFilter);
                }

                return null;
            }

            // Logical operators ("and", "or") at position 0 are not valid DevExtreme format,
            // but we treat any known operator as non-field-name to be safe.
            if (!IsLogicalOperator(fieldOrOp))
            {
                // This is a field name — validate it
                if (!_allowedFields.Contains(fieldOrOp))
                {
                    return fieldOrOp;
                }

                return null;
            }
        }

        // Compound filter: [cond1, "and", cond2, "or", cond3, ...]
        // Walk each element; skip string operators, recurse into IList sub-conditions.
        foreach (var element in filter)
        {
            if (element is IList subList)
            {
                var result = FindDisallowedFilterField(subList);
                if (result is not null)
                {
                    return result;
                }
            }
            // String elements at this level are logical operators ("and", "or") — skip them.
        }

        return null;
    }

    private static bool IsLogicalOperator(string value)
    {
        return string.Equals(value, "and", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "or", StringComparison.OrdinalIgnoreCase);
    }
}
