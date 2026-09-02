using Envanex.Domain.Common;
using Shouldly;

namespace Envanex.Domain.Tests.Common;

public class EntityTests
{
    private sealed class TestEntity : Entity<Guid>
    {
        public TestEntity(Guid id) : base(id)
        {
        }
    }

    private sealed class OtherTestEntity : Entity<Guid>
    {
        public OtherTestEntity(Guid id) : base(id)
        {
        }
    }

    [Fact]
    public void Entities_WithSameId_ShouldBeEqual()
    {
        Guid id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);

        entity1.Equals(entity2).ShouldBeTrue();
    }

    [Fact]
    public void Entities_WithDifferentIds_ShouldNotBeEqual()
    {
        var entity1 = new TestEntity(Guid.NewGuid());
        var entity2 = new TestEntity(Guid.NewGuid());

        entity1.Equals(entity2).ShouldBeFalse();
    }

    [Fact]
    public void Entity_ComparedToNull_ShouldNotBeEqual()
    {
        var entity = new TestEntity(Guid.NewGuid());

        entity.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void Entities_WithSameId_ShouldHaveSameHashCode()
    {
        Guid id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);

        entity1.GetHashCode().ShouldBe(entity2.GetHashCode());
    }

    [Fact]
    public void EqualityOperator_WithSameId_ShouldReturnTrue()
    {
        Guid id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);

        (entity1 == entity2).ShouldBeTrue();
    }

    [Fact]
    public void InequalityOperator_WithDifferentId_ShouldReturnTrue()
    {
        var entity1 = new TestEntity(Guid.NewGuid());
        var entity2 = new TestEntity(Guid.NewGuid());

        (entity1 != entity2).ShouldBeTrue();
    }

    [Fact]
    public void Entities_OfDifferentTypes_WithSameId_ShouldNotBeEqual()
    {
        Guid id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new OtherTestEntity(id);

        entity1.Equals(entity2).ShouldBeFalse();
    }
}
