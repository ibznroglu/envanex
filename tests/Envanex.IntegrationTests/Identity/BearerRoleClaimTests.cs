using System.Security.Claims;
using Envanex.Application.Abstractions.Authentication;
using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Models;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Shouldly;

namespace Envanex.IntegrationTests.Identity;

/// <summary>
/// The signing half and the validating half, read against each other. The issuer writes the short
/// <c>role</c> claim; these cases prove the host's bearer options are configured to read that same
/// string back, rather than a WS-Federation URI nothing writes.
/// </summary>
/// <remarks>
/// Both cases read the host's own options out of <c>WebApplicationFactory.Services</c> rather than
/// rebuilding a copy of them. A copy would stay green while the host was misconfigured, which is
/// the whole failure these tests exist to catch.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class BearerRoleClaimTests
{
    private readonly SqlServerFixture _fixture;

    public BearerRoleClaimTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public void HostBearerOptions_ShouldSetRoleClaimTypeToTheEnvanexRoleClaim()
    {
        var options = BearerOptions();

        options.TokenValidationParameters.RoleClaimType.ShouldBe(EnvanexClaimTypes.Role);
    }

    [Fact]
    public async Task TokenCarryingTheAdministratorRole_ValidatedWithHostOptions_ShouldProduceAPrincipalInThatRole()
    {
        var user = new AuthenticatedUser(
            Guid.CreateVersion7(),
            "bearer-role-claim@envanex.test",
            "bearer-role-claim@envanex.test",
            [EnvanexRoles.Administrator]);

        var issued = _fixture.WebApplicationFactory.Services
            .GetRequiredService<IAccessTokenIssuer>()
            .Issue(user);

        var options = BearerOptions();

        // The host's own handler, not a fresh one: JwtBearerOptions.MapInboundClaims is applied to
        // the handlers in this collection, so validating through them is what makes this case able
        // to fail when that line is removed.
        var handler = options.TokenHandlers.OfType<JsonWebTokenHandler>().Single();

        var result = await handler.ValidateTokenAsync(issued.Token, options.TokenValidationParameters);

        result.IsValid.ShouldBeTrue($"Validation failed: {result.Exception?.Message}");

        var principal = new ClaimsPrincipal(result.ClaimsIdentity);

        principal.IsInRole(EnvanexRoles.Administrator).ShouldBeTrue(
            "The token carries the role but the principal is not in it. Claim types present: "
            + string.Join(", ", principal.Claims.Select(claim => claim.Type)));
    }

    private JwtBearerOptions BearerOptions()
        => _fixture.WebApplicationFactory.Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
}
