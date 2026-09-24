namespace Envanex.Web.Authorization;

/// <summary>
/// The two authorization policies. Endpoints and pages name a policy, never a role, so that adding
/// a role later means editing the policy registration in <c>Program.cs</c> and nothing else.
/// </summary>
public static class EnvanexPolicies
{
    /// <summary>Satisfied by <c>Administrator</c> and <c>Viewer</c>.</summary>
    public const string CanRead = "CanRead";

    /// <summary>Satisfied by <c>Administrator</c> only.</summary>
    public const string CanWrite = "CanWrite";
}
