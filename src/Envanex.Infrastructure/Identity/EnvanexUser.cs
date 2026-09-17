using Microsoft.AspNetCore.Identity;

namespace Envanex.Infrastructure.Identity;

/// <summary>
/// The application user. It adds no members to <see cref="IdentityUser{TKey}"/>; it exists so the
/// key type is <see cref="Guid"/> and so a later phase can add profile data without a migration
/// of the base type. Public because <see cref="EnvanexIdentityDbContext"/> is public and CS0060
/// forbids a public class deriving from a base constructed with a less accessible type argument.
/// </summary>
public sealed class EnvanexUser : IdentityUser<Guid>;
