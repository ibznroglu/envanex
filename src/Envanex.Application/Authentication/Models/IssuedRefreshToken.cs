namespace Envanex.Application.Authentication.Models;

public sealed record IssuedRefreshToken(string Token, DateTimeOffset ExpiresAt);
