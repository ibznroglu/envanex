using System.Reflection;
using Envanex.Domain.Common;
using Envanex.Web.Extensions;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

public sealed class ResultMappingTests
{
    private static readonly Type ErrorType = typeof(Error);
    private static readonly Type ValidationErrorType = typeof(ValidationError);

    /// <summary>
    /// The well-known error code produced by <see cref="ValidationError"/>.
    /// It is not a static Error field, so reflection cannot discover it.
    /// </summary>
    private const string ValidationFailedCode = "Validation.Failed";

    /// <summary>
    /// Scans the Domain assembly for all public static readonly Error fields,
    /// excluding the <c>Error.None</c> sentinel and <c>ValidationError</c> fields.
    /// Returns the Code value of each field.
    /// </summary>
    private static HashSet<string> GetAllDomainErrorCodes()
    {
        var domainAssembly = ErrorType.Assembly;

        var codes = domainAssembly
            .GetTypes()
            .Where(t => t.IsPublic && t != ErrorType && t != ValidationErrorType)
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(f => f.IsInitOnly && f.FieldType == ErrorType)
            .Select(f => (Error)f.GetValue(null)!)
            .Select(e => e.Code)
            .ToHashSet();

        // ValidationError's code is declared in its constructor, not as a static field.
        codes.Add(ValidationFailedCode);

        return codes;
    }

    [Fact]
    public void ResultMapping_EveryDomainErrorCode_ShouldHaveAnExplicitMapping()
    {
        var codes = GetAllDomainErrorCodes();

        codes.ShouldNotBeEmpty("No domain error codes found; the scan may be broken.");

        var unmapped = codes.Except(ResultExtensions.MappedErrorCodes).ToList();

        unmapped.ShouldBeEmpty(
            $"The following domain error codes have no explicit HTTP status mapping in ResultExtensions: " +
            $"{string.Join(", ", unmapped)}");
    }

    [Fact]
    public void ResultMapping_EveryDomainErrorCode_ShouldHaveATurkishMessage()
    {
        var codes = GetAllDomainErrorCodes();

        codes.ShouldNotBeEmpty("No domain error codes found; the scan may be broken.");

        var unmapped = codes.Except(TurkishErrorMessages.TranslatedErrorCodes).ToList();

        unmapped.ShouldBeEmpty(
            $"The following domain error codes have no Turkish message in TurkishErrorMessages: " +
            $"{string.Join(", ", unmapped)}");
    }

    [Fact]
    public void ResultMapping_NoDomainErrorCode_ShouldBeEmpty()
    {
        var codes = GetAllDomainErrorCodes();

        codes.ShouldNotBeEmpty("No domain error codes found; the scan may be broken.");

        var emptyCodes = codes.Where(code => string.IsNullOrWhiteSpace(code)).ToList();

        emptyCodes.ShouldBeEmpty(
            "Found domain error fields with empty Code values outside of Error/ValidationError. " +
            "Every error field must have a non-empty Code.");
    }

    [Fact]
    public void ResultMapping_NoMappingEntry_ShouldBeOrphaned()
    {
        var domainCodes = GetAllDomainErrorCodes();

        domainCodes.ShouldNotBeEmpty("No domain error codes found; the scan may be broken.");

        var allTableCodes = new HashSet<string>(ResultExtensions.MappedErrorCodes);
        allTableCodes.UnionWith(TurkishErrorMessages.TranslatedErrorCodes);

        var orphans = allTableCodes.Except(domainCodes).ToList();

        orphans.ShouldBeEmpty(
            $"The following codes exist in mapping/message tables but have no matching domain Error field: " +
            $"{string.Join(", ", orphans)}");
    }
}
