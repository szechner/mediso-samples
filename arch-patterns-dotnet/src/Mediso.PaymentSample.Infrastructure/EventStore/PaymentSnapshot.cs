namespace Mediso.PaymentSample.Infrastructure.EventStore;

/// <summary>
/// Document to store Payment aggregate snapshots
/// </summary>
public class PaymentSnapshot
{
    /// <summary>
    /// The payment aggregate ID (same as the original Payment ID)
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// The version of the aggregate when the snapshot was taken
    /// </summary>
    public long AggregateVersion { get; set; }
    
    /// <summary>
    /// JSON serialized data of the Payment aggregate
    /// </summary>
    public string SnapshotData { get; set; } = string.Empty;
    
    /// <summary>
    /// When the snapshot was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
}