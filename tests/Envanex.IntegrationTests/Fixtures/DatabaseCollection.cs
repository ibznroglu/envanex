using System.Diagnostics.CodeAnalysis;

namespace Envanex.IntegrationTests.Fixtures;

[CollectionDefinition(Name)]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Required by xUnit ICollectionFixture pattern")]
public sealed class DatabaseCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "Database";
}
