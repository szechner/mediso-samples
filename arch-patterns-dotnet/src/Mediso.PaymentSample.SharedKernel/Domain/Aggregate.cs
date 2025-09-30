namespace Mediso.PaymentSample.SharedKernel.Domain;

public interface IAggregateRoot 
{
    Guid Id { get; }
    long Version { get; }
    IEnumerable<IDomainEvent> GetUncommittedEvents();
    void MarkEventsAsCommitted();
}


public abstract class Aggregate<TId> : IAggregateRoot
{
    public TId Id { get; protected set; } = default!;
    public long Version { get; protected set; }

    // IAggregateRoot implementation
    Guid IAggregateRoot.Id => Id switch
    {
        Guid guid => guid,
        string str when Guid.TryParse(str, out var parsed) => parsed,
        _ => TryConvertToGuid(Id) ?? throw new InvalidOperationException($"Cannot convert {typeof(TId).Name} to Guid for aggregate ID")
    };
    long IAggregateRoot.Version => Version;

    private readonly List<IDomainEvent> uncommitted = new();
    public IReadOnlyCollection<IDomainEvent> UncommittedEvents => uncommitted.AsReadOnly();
    
    // IAggregateRoot implementation
    public IEnumerable<IDomainEvent> GetUncommittedEvents() => uncommitted.AsReadOnly();
    public void MarkEventsAsCommitted() => uncommitted.Clear();

    protected void Raise(IDomainEvent @event)
    {
        When(@event);
        uncommitted.Add(@event);
        Version++;
    }

    protected abstract void When(IDomainEvent @event);
    
    /// <summary>
    /// Converts the strongly-typed ID to a Guid for compatibility
    /// </summary>
    private static Guid? TryConvertToGuid(TId id)
    {
        // Try implicit conversion using dynamic (handles implicit operators)
        try
        {
            dynamic dynamicId = id!;
            if (dynamicId is Guid guid)
                return guid;
                
            // Try to call implicit conversion to Guid if it exists
            Guid convertedGuid = (Guid)dynamicId;
            return convertedGuid;
        }
        catch
        {
            // If conversion fails, try to get a Value property or similar
            var type = typeof(TId);
            var valueProperty = type.GetProperty("Value");
            if (valueProperty?.PropertyType == typeof(Guid))
            {
                return (Guid?)valueProperty.GetValue(id);
            }
        }
        
        return null;
    }
    
    [Obsolete("Use MarkEventsAsCommitted() instead")]
    public void ClearUncommittedEvents() => uncommitted.Clear();
}
