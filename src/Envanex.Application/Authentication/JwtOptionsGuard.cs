using System.Text;

namespace Envanex.Application.Authentication;

/// <summary>
/// Fails fast on misconfigured JWT settings, in the same shape as the connection-string guard in
/// Envanex.Infrastructure: name the offending key and the command that sets it. A defaulted or
/// generated signing key would leave every test green while making tokens unverifiable across
/// restarts and forgeable if the default ever shipped.
/// </summary>
public static class JwtOptionsGuard
{
    private const string UserSecretsProject = "src/Envanex.Web";

    public static void ThrowIfInvalid(JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ThrowIfBlank(options.Issuer, "Jwt:Issuer");
        ThrowIfBlank(options.Audience, "Jwt:Audience");
        ThrowIfBlank(options.SigningKey, "Jwt:SigningKey");

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < JwtOptions.MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:SigningKey must be at least {JwtOptions.MinimumSigningKeyBytes} UTF-8 bytes long. " +
                BuildUserSecretsHint("Jwt:SigningKey"));
        }

        ThrowIfNotPositive(options.AccessTokenMinutes, "Jwt:AccessTokenMinutes");
        ThrowIfNotPositive(options.RefreshTokenIdleDays, "Jwt:RefreshTokenIdleDays");
        ThrowIfNotPositive(options.RefreshTokenAbsoluteDays, "Jwt:RefreshTokenAbsoluteDays");

        if (options.RefreshTokenIdleDays > options.RefreshTokenAbsoluteDays)
        {
            throw new InvalidOperationException(
                "Jwt:RefreshTokenIdleDays must not exceed Jwt:RefreshTokenAbsoluteDays; " +
                "an idle window longer than the absolute cap can never be reached. " +
                $"Idle: {options.RefreshTokenIdleDays}, absolute: {options.RefreshTokenAbsoluteDays}.");
        }
    }

    private static void ThrowIfBlank(string value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} is not configured. {BuildUserSecretsHint(key)}");
        }
    }

    private static void ThrowIfNotPositive(int value, string key)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException(
                $"{key} must be greater than zero. Configured value: {value}.");
        }
    }

    private static string BuildUserSecretsHint(string key) =>
        $"Set it via user-secrets: dotnet user-secrets set \"{key}\" \"<value>\" --project {UserSecretsProject}";
}
