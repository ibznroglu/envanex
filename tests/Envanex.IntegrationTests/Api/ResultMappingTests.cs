using System.Reflection;
using Envanex.Application.Authentication;
using Envanex.Domain.Common;
using Envanex.Web.Extensions;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

public sealed class ResultMappingTests
{
    private static readonly Type ErrorType = typeof(Error);
    private static readonly Type ValidationErrorType = typeof(ValidationError);

    /// <summary>
    /// The two assemblies that declare error codes: Envanex.Domain (aggregates and value objects)
    /// and Envanex.Application (authentication). Domain has no auth aggregate and will not get
    /// one, so scanning Domain alone would leave the mapping tables unguarded for exactly the
    /// errors a user is most likely to see.
    /// </summary>
    private static readonly Assembly[] ScannedAssemblies =
    [
        ErrorType.Assembly,
        typeof(AuthErrors).Assembly,
    ];

    /// <summary>
    /// The well-known error code produced by <see cref="ValidationError"/>.
    /// It is not a static Error field, so reflection cannot discover it.
    /// </summary>
    private const string ValidationFailedCode = "Validation.Failed";

    /// <summary>
    /// Scans the Domain and Application assemblies for all public static readonly Error fields,
    /// excluding the <c>Error.None</c> sentinel and <c>ValidationError</c> fields.
    /// Returns the Code value of each field.
    /// </summary>
    private static HashSet<string> GetAllErrorCodes()
    {
        var codes = ScannedAssemblies
            .SelectMany(assembly => assembly.GetTypes())
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
    public void ResultMapping_Scan_ShouldReachTheApplicationAssembly()
    {
        var codes = GetAllErrorCodes();

        // Without this, a two-assembly scan that silently degrades to Domain-only would leave
        // every Auth.* code unguarded while all four facts below stayed green.
        codes.ShouldContain(
            AuthErrors.InvalidCredentials.Code,
            "The scan no longer reaches Envanex.Application; the auth error codes are unguarded.");
    }

    [Fact]
    public void ResultMapping_EveryErrorCode_ShouldHaveAnExplicitMapping()
    {
        var codes = GetAllErrorCodes();

        codes.ShouldNotBeEmpty("No error codes found; the scan may be broken.");

        var unmapped = codes.Except(ResultExtensions.MappedErrorCodes).ToList();

        unmapped.ShouldBeEmpty(
            $"The following error codes have no explicit HTTP status mapping in ResultExtensions: " +
            $"{string.Join(", ", unmapped)}");
    }

    [Fact]
    public void ResultMapping_EveryErrorCode_ShouldHaveATurkishMessage()
    {
        var codes = GetAllErrorCodes();

        codes.ShouldNotBeEmpty("No error codes found; the scan may be broken.");

        var unmapped = codes.Except(TurkishErrorMessages.TranslatedErrorCodes).ToList();

        unmapped.ShouldBeEmpty(
            $"The following error codes have no Turkish message in TurkishErrorMessages: " +
            $"{string.Join(", ", unmapped)}");
    }

    [Fact]
    public void ResultMapping_NoErrorCode_ShouldBeEmpty()
    {
        var codes = GetAllErrorCodes();

        codes.ShouldNotBeEmpty("No error codes found; the scan may be broken.");

        var emptyCodes = codes.Where(code => string.IsNullOrWhiteSpace(code)).ToList();

        emptyCodes.ShouldBeEmpty(
            "Found error fields with empty Code values outside of Error/ValidationError. " +
            "Every error field must have a non-empty Code.");
    }

    [Fact]
    public void ResultMapping_NoMappingEntry_ShouldBeOrphaned()
    {
        var declaredCodes = GetAllErrorCodes();

        declaredCodes.ShouldNotBeEmpty("No error codes found; the scan may be broken.");

        var allTableCodes = new HashSet<string>(ResultExtensions.MappedErrorCodes);
        allTableCodes.UnionWith(TurkishErrorMessages.TranslatedErrorCodes);

        var orphans = allTableCodes.Except(declaredCodes).ToList();

        orphans.ShouldBeEmpty(
            $"The following codes exist in mapping/message tables but have no matching Error field: " +
            $"{string.Join(", ", orphans)}");
    }
}
