using System.Collections.Frozen;
using Envanex.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Envanex.Web.Extensions;

public static class ResultExtensions
{
    /// <summary>
    /// Generic Turkish fallback for ValidationFailure messages. ValidationFailure only carries
    /// PropertyName and ErrorCode (no Message field), so a generic Turkish fallback is used
    /// instead of the raw error code.
    /// </summary>
    internal const string GenericValidationFallback = "Bu alan geçersiz.";

    private static readonly FrozenDictionary<string, int> StatusCodeMap = new Dictionary<string, int>
    {
        // 400 Bad Request - ValidationError
        ["Validation.Failed"] = StatusCodes.Status400BadRequest,

        // 401 Unauthorized - Authentication
        // All four answer the same thing to the caller: try again or log in again. A locked-out
        // account is deliberately absent; it shares Auth.InvalidCredentials.
        ["Auth.InvalidCredentials"] = StatusCodes.Status401Unauthorized,
        ["Auth.InvalidRefreshToken"] = StatusCodes.Status401Unauthorized,
        ["Auth.RefreshTokenExpired"] = StatusCodes.Status401Unauthorized,
        ["Auth.RefreshTokenReused"] = StatusCodes.Status401Unauthorized,

        // 400 Bad Request - Authentication validators
        ["Auth.EmailRequired"] = StatusCodes.Status400BadRequest,
        ["Auth.EmailInvalid"] = StatusCodes.Status400BadRequest,
        ["Auth.PasswordRequired"] = StatusCodes.Status400BadRequest,
        ["Auth.RefreshTokenRequired"] = StatusCodes.Status400BadRequest,

        // 404 Not Found
        ["Product.NotFound"] = StatusCodes.Status404NotFound,
        ["UnitOfMeasure.NotFound"] = StatusCodes.Status404NotFound,

        // 409 Conflict
        ["Product.DuplicateCode"] = StatusCodes.Status409Conflict,
        ["UnitOfMeasure.DuplicateCode"] = StatusCodes.Status409Conflict,
        ["Product.ConcurrencyConflict"] = StatusCodes.Status409Conflict,

        // 422 Unprocessable Entity
        ["Product.UnitOfMeasureNotFound"] = StatusCodes.Status422UnprocessableEntity,
        ["Product.UnitOfMeasureInactive"] = StatusCodes.Status422UnprocessableEntity,
        ["UnitOfMeasure.BaseUnitNotFound"] = StatusCodes.Status422UnprocessableEntity,
        ["UnitOfMeasure.BaseUnitInactive"] = StatusCodes.Status422UnprocessableEntity,

        // 400 Bad Request - Product domain
        ["Product.CodeRequired"] = StatusCodes.Status400BadRequest,
        ["Product.NameRequired"] = StatusCodes.Status400BadRequest,
        ["Product.UnitOfMeasureRequired"] = StatusCodes.Status400BadRequest,
        ["Product.CodeTooLong"] = StatusCodes.Status400BadRequest,
        ["Product.NameTooLong"] = StatusCodes.Status400BadRequest,

        // 400 Bad Request - Product validator
        ["Product.IdRequired"] = StatusCodes.Status400BadRequest,
        ["Product.RowVersionRequired"] = StatusCodes.Status400BadRequest,
        ["Product.ListPriceAmountNegative"] = StatusCodes.Status400BadRequest,
        ["Product.ReorderPointNegative"] = StatusCodes.Status400BadRequest,
        ["Product.ListPriceCurrencyRequired"] = StatusCodes.Status400BadRequest,

        // 400 Bad Request - UnitOfMeasure
        ["UnitOfMeasure.CodeRequired"] = StatusCodes.Status400BadRequest,
        ["UnitOfMeasure.NameRequired"] = StatusCodes.Status400BadRequest,
        ["UnitOfMeasure.CodeTooLong"] = StatusCodes.Status400BadRequest,
        ["UnitOfMeasure.NameTooLong"] = StatusCodes.Status400BadRequest,
        ["UnitOfMeasure.InvalidBaseUnitId"] = StatusCodes.Status400BadRequest,
        ["UnitOfMeasure.BaseUnitFactorMustBeOne"] = StatusCodes.Status400BadRequest,
        ["UnitOfMeasure.ConversionFactorMustBePositive"] = StatusCodes.Status400BadRequest,

        // 400 Bad Request - Warehouse
        ["Warehouse.CodeRequired"] = StatusCodes.Status400BadRequest,
        ["Warehouse.NameRequired"] = StatusCodes.Status400BadRequest,

        // 400 Bad Request - Value Objects
        ["Currency.InvalidCode"] = StatusCodes.Status400BadRequest,
        ["Money.CurrencyMismatch"] = StatusCodes.Status400BadRequest,
        ["Quantity.Negative"] = StatusCodes.Status400BadRequest,
        ["Quantity.NegativeResult"] = StatusCodes.Status400BadRequest,
    }.ToFrozenDictionary();

    public static IActionResult ToActionResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return new OkObjectResult(result.Value);
        }

        return ToErrorActionResult(result.Error);
    }

    public static IActionResult ToCreatedActionResult<T>(
        this Result<T> result,
        string routeName,
        Func<T, object> routeValues)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(routeValues);

        if (result.IsSuccess)
        {
            return new CreatedAtRouteResult(routeName, routeValues(result.Value), result.Value);
        }

        return ToErrorActionResult(result.Error);
    }

    /// <summary>
    /// Maps a successful result to 204 No Content, discarding the value. Used by logout, whose
    /// success carries nothing a caller can act on; a failure still maps to the usual
    /// ProblemDetails.
    /// </summary>
    public static IActionResult ToNoContentActionResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return new NoContentResult();
        }

        return ToErrorActionResult(result.Error);
    }

    public static IReadOnlySet<string> MappedErrorCodes { get; } = StatusCodeMap.Keys.ToFrozenSet();

    private static ObjectResult ToErrorActionResult(Error error)
    {
        if (error is ValidationError validationError)
        {
            return BuildValidationProblemDetails(validationError);
        }

        return BuildProblemDetails(error);
    }

    private static ObjectResult BuildValidationProblemDetails(ValidationError validationError)
    {
        var errors = new Dictionary<string, string[]>();

        foreach (var failure in validationError.Failures)
        {
            string message = TurkishErrorMessages.GetMessage(failure.ErrorCode, GenericValidationFallback);

            if (errors.TryGetValue(failure.PropertyName, out var existing))
            {
                errors[failure.PropertyName] = [.. existing, message];
            }
            else
            {
                errors[failure.PropertyName] = [message];
            }
        }

        var problemDetails = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Failed",
            Detail = TurkishErrorMessages.GetMessage(validationError.Code, validationError.Message),
        };

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status400BadRequest,
        };
    }

    private static ObjectResult BuildProblemDetails(Error error)
    {
        int statusCode = StatusCodeMap.GetValueOrDefault(error.Code, StatusCodes.Status400BadRequest);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = GetReasonPhrase(statusCode),
            Detail = TurkishErrorMessages.GetMessage(error.Code, error.Message),
        };

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode,
        };
    }

    private static string GetReasonPhrase(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status422UnprocessableEntity => "Unprocessable Entity",
        _ => "Error",
    };
}
