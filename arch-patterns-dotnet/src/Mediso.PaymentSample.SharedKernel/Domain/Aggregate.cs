namespace Mediso.PaymentSample.SharedKernel.Domain;

public interface IAggregateRoot { }


public abstract class Aggregate<TId> : IAggregateRoot
{
    public TId Id { get; protected set; } = default!;
    public long Version { get; protected set; }


    private readonly List<IDomainEvent> uncommitted = new();
    public IReadOnlyCollection<IDomainEvent> UncommittedEvents => uncommitted.AsReadOnly();


    protected void Raise(IDomainEvent @event)
    {
        When(@event);
        uncommitted.Add(@event);
        Version++;
    }


    protected abstract void When(IDomainEvent @event);
    public void ClearUncommittedEvents() => uncommitted.Clear();
}