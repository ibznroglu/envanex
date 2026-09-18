using Shouldly;

namespace Envanex.IntegrationTests.Identity;

/// <summary>
/// Keeps the exception to "test users are created through <c>UserManager</c>" narrow. The rule
/// only holds while the bare-row seeder stays where it was justified; widening its use must fail a
/// named test rather than pass quietly.
/// </summary>
public sealed class IdentitySeedingScopeTests
{
    private static readonly string[] AllowedFiles =
    [
        "IdentityRowSeeder.cs",
        "IdentitySeedingScopeTests.cs",
        "RefreshTokenSchemaTests.cs",
    ];

    [Fact]
    public void IdentityRowSeeder_ShouldBeReferencedOnlyBySchemaTests()
    {
        var testsDirectory = Path.Combine(FindSolutionDirectory(), "tests");

        var referencingFiles = Directory
            .EnumerateFiles(testsDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path, testsDirectory))
            .Where(path => File.ReadAllText(path).Contains("IdentityRowSeeder", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        referencingFiles.ShouldBe(
            AllowedFiles,
            $"Files naming IdentityRowSeeder: {string.Join(", ", referencingFiles)}. The bare-row " +
            "seeder writes an auth.AspNetUsers row with no password hash and bypasses UserValidator. " +
            "It is justified for schema tests that only need a foreign key target. Anything that " +
            "authenticates must use UserManager instead.");
    }

    private static bool IsBuildOutput(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);

        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment =>
                segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindSolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (directory.GetFiles("Envanex.slnx").Length > 0
                || directory.GetFiles("Envanex.sln").Length > 0)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Envanex.sln or Envanex.slnx not found. The test must run from within the solution directory.");
    }
}
