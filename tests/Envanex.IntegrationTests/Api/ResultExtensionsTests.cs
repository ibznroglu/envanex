using Envanex.Domain.Common;
using Envanex.Web.Extensions;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

public sealed class ResultExtensionsTests
{
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
}
