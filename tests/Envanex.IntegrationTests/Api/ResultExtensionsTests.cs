using System.Reflection;
using Envanex.Application.Authentication;
using Envanex.Domain.Common;
using Envanex.Web.Extensions;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

public sealed class ResultExtensionsTests
{
    /// <summary>
    /// Every error code declared by <see cref="AuthErrors"/>, discovered by reflection so a new
    /// code is covered the moment it is added.
    /// </summary>
    public static TheoryData<string> AuthErrorCodes
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (var code in typeof(AuthErrors)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsInitOnly && f.FieldType == typeof(Error))
                .Select(f => ((Error)f.GetValue(null)!).Code))
            {
                data.Add(code);
            }

            return data;
        }
    }

    [Fact]
    public void UnmappedErrorCode_ShouldFallBackTo400NotServerError()
    {
        // An error code that does not exist in the StatusCodeMap
        var unmappedError = new Error("SomeAggregate.CompletelyUnmapped", "This code has no mapping.");
        var failedResult = Result.Failure<Guid>(unmappedError);

        var actionResult = failedResult.ToActionResult();

        var objectResult = actionResult.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(400);

        var problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Status.ShouldBe(400);
        problemDetails.Title.ShouldBe("Bad Request");

        // The fallback for a regular Error uses the English Message field,
        // because Error carries a Message property (unlike ValidationFailure).
        problemDetails.Detail.ShouldBe("This code has no mapping.");
    }

    [Fact]
    public void ValidationFailure_WithUnmappedErrorCode_ShouldUseGenericTurkishFallback()
    {
        // A ValidationFailure whose ErrorCode has no Turkish translation
        var failures = new List<ValidationFailure>
        {
            new("SomeField", "SomeAggregate.UnknownRule"),
        };
        var validationError = new ValidationError(failures);
        var failedResult = Result.Failure<Guid>(validationError);

        var actionResult = failedResult.ToActionResult();

        var objectResult = actionResult.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(400);

        var problemDetails = objectResult.Value.ShouldBeOfType<ValidationProblemDetails>();
        problemDetails.Errors.ShouldContainKey("SomeField");

        // Should use the generic Turkish fallback, NOT the raw error code
        problemDetails.Errors["SomeField"].ShouldContain("Bu alan ge\u00e7ersiz.");
    }

    [Fact]
    public void ToActionResult_AuthInvalidCredentials_ShouldReturn401WithTurkishDetail()
    {
        var failedResult = Result.Failure<Guid>(AuthErrors.InvalidCredentials);

        var actionResult = failedResult.ToActionResult();

        var objectResult = actionResult.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(401);

        var problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Status.ShouldBe(401);
        problemDetails.Detail.ShouldBe(
            TurkishErrorMessages.GetMessage(AuthErrors.InvalidCredentials.Code, "fallback"));
        problemDetails.Detail.ShouldNotBe(
            AuthErrors.InvalidCredentials.Message,
            "The English Error.Message leaked instead of the Turkish translation.");
    }

    [Fact]
    public void ToActionResult_AuthRefreshTokenReused_ShouldReturn401WithTurkishDetail()
    {
        var failedResult = Result.Failure<Guid>(AuthErrors.RefreshTokenReused);

        var actionResult = failedResult.ToActionResult();

        var objectResult = actionResult.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(401);

        var problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Status.ShouldBe(401);
        problemDetails.Detail.ShouldBe(
            TurkishErrorMessages.GetMessage(AuthErrors.RefreshTokenReused.Code, "fallback"));
        problemDetails.Detail.ShouldNotBe(
            AuthErrors.RefreshTokenReused.Message,
            "The English Error.Message leaked instead of the Turkish translation.");
    }

    [Fact]
    public void ToActionResult_Status401_ShouldHaveTitleUnauthorized()
    {
        var failedResult = Result.Failure<Guid>(AuthErrors.InvalidRefreshToken);

        var actionResult = failedResult.ToActionResult();

        var objectResult = actionResult.ShouldBeOfType<ObjectResult>();
        var problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();

        // Without the 401 arm in GetReasonPhrase the title would read "Error".
        problemDetails.Title.ShouldBe("Unauthorized");
    }

    [Fact]
    public void ToNoContentActionResult_Success_ShouldReturn204()
    {
        var result = Result.Success(true);

        var actionResult = result.ToNoContentActionResult();

        actionResult.ShouldBeOfType<NoContentResult>().StatusCode.ShouldBe(204);
    }

    [Fact]
    public void ToNoContentActionResult_Failure_ShouldReturnTheMappedProblemDetails()
    {
        var result = Result.Failure<bool>(AuthErrors.InvalidRefreshToken);

        var actionResult = result.ToNoContentActionResult();

        var objectResult = actionResult.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(401);

        var problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Status.ShouldBe(401);
        problemDetails.Detail.ShouldBe(
            TurkishErrorMessages.GetMessage(AuthErrors.InvalidRefreshToken.Code, "fallback"));
    }

    [Fact]
    public void ToNoContentActionResult_NullResult_ShouldThrowArgumentNullException()
    {
        Result<bool> result = null!;

        Should.Throw<ArgumentNullException>(() => result.ToNoContentActionResult());
    }

    [Theory]
    [MemberData(nameof(AuthErrorCodes))]
    public void ToActionResult_EveryAuthErrorCode_ShouldMapTo400Or401(string errorCode)
    {
        var failedResult = Result.Failure<Guid>(new Error(errorCode, "English fallback message."));

        var actionResult = failedResult.ToActionResult();

        var objectResult = actionResult.ShouldBeOfType<ObjectResult>();

        // 403 would tell the caller the credentials were understood but refused, which is exactly
        // the distinction login must never make. No auth code maps to 403 in this PR.
        objectResult.StatusCode.ShouldNotBeNull().ShouldBeOneOf(400, 401);
    }
}
