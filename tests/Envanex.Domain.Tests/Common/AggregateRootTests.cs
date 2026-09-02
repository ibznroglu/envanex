using Envanex.Domain.Common;
using Shouldly;

namespace Envanex.Domain.Tests.Common;

public class AggregateRootTests
{
    private sealed record TestEvent(DateTime OccurredOnUtc) : IDomainEvent;

    private sealed class TestAggregate : AggregateRoot<Guid>
    {
        public TestAggregate(Guid id) : base(id)
        {
        }

        public void AddEvent(IDomainEvent domainEvent)
        {
            RaiseDomainEvent(domainEvent);
        }
    }

    [Fact]
    public void RaiseDomainEvent_ShouldAddEventToCollection()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        var @event = new TestEvent(DateTime.UtcNow);

        aggregate.AddEvent(@event);

        aggregate.DomainEvents.Count.ShouldBe(1);
        aggregate.DomainEvents.ShouldContain(@event);
    }

    [Fact]
    public void ClearDomainEvents_ShouldEmptyCollection()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.AddEvent(new TestEvent(DateTime.UtcNow));

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void DomainEvents_ShouldReturnReadOnlyCollection()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());

        aggregate.DomainEvents.ShouldBeAssignableTo<IReadOnlyCollection<IDomainEvent>>();
        (aggregate.DomainEvents as List<IDomainEvent>).ShouldBeNull();
    }

    [Fact]
    public void NewAggregate_ShouldHaveEmptyDomainEvents()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());

        aggregate.DomainEvents.ShouldBeEmpty();
    }
}
