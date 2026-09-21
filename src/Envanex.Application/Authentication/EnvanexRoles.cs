namespace Envanex.Application.Authentication;

/// <summary>
/// The two application roles. They live in the Application layer because both halves of the
/// system need them: Infrastructure seeds them and reads them off a user, and Web names them in
/// its authorization policies.
/// </summary>
/// <remarks>
/// Names that need explaining were rejected. A role name is read by people who will never open the
/// policy table, and a name whose meaning has to be looked up is a name that gets guessed at.
/// </remarks>
public static class EnvanexRoles
{
    public const string Administrator = "Administrator";

    public const string Viewer = "Viewer";

    /// <summary>
    /// Every role the application seeds, in the order it seeds them.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Administrator, Viewer];
}
