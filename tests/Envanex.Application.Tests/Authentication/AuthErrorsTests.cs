using System.Reflection;
using Envanex.Application.Authentication;
using Envanex.Domain.Common;
using Shouldly;

namespace Envanex.Application.Tests.Authentication;

public class AuthErrorsTests
{
    private static Error[] AllErrors => typeof(AuthErrors)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsInitOnly && f.FieldType == typeof(Error))
        .Select(f => (Error)f.GetValue(null)!)
        .ToArray();

    [Fact]
    public void AuthErrors_EveryCode_ShouldStartWithTheAuthPrefix()
    {
        var errors = AllErrors;

        errors.ShouldNotBeEmpty("No AuthErrors fields found; the reflection scan may be broken.");

        var offenders = errors
            .Where(e => !e.Code.StartsWith("Auth.", StringComparison.Ordinal))
            .Select(e => e.Code)
            .ToArray();

        offenders.ShouldBeEmpty(
            $"Every authentication error code must be prefixed 'Auth.': {string.Join(", ", offenders)}");
    }

    [Fact]
    public void AuthErrors_Codes_ShouldBeUnique()
    {
        var codes = AllErrors.Select(e => e.Code).ToArray();

        codes.ShouldNotBeEmpty("No AuthErrors fields found; the reflection scan may be broken.");

        // A duplicated code would silently collapse two failures into one mapping-table entry.
        codes.Distinct(StringComparer.Ordinal).Count().ShouldBe(codes.Length);
    }

    [Fact]
    public void AuthErrors_EveryMessage_ShouldBeNonEmpty()
    {
        var errors = AllErrors;

        errors.ShouldNotBeEmpty("No AuthErrors fields found; the reflection scan may be broken.");

        var offenders = errors
            .Where(e => string.IsNullOrWhiteSpace(e.Message))
            .Select(e => e.Code)
            .ToArray();

        // The English message is the fallback detail when no Turkish message is registered.
        offenders.ShouldBeEmpty(
            $"The following authentication errors carry an empty message: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void AuthErrors_ShouldNotDeclareALockoutSpecificCode()
    {
        string[] lockoutTerms = ["lockout", "locked", "lock", "kilit"];

        var offenders = AllErrors
            .Where(e => lockoutTerms.Any(term =>
                e.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                || e.Message.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Select(e => e.Code)
            .ToArray();

        // A locked-out account gets the same InvalidCredentials instance as a wrong password and
        // an unknown e-mail address, so the three cannot drift apart into three distinguishable
        // responses later. Re-adding a lockout code must mean deleting this test on purpose.
        offenders.ShouldBeEmpty(
            $"Authentication errors naming a lockout: {string.Join(", ", offenders)}. " +
            "Login answers wrong password, unknown user and locked-out account identically.");
    }
}
