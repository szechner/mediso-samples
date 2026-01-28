namespace Mediso.AuditSample.Application.Models;

public sealed record AuditCaseBatchLink(
    Guid BatchId,
    int LeafIndex,
    string LeafSha256,
    string MerkleRootSha256,
    string? TxSignature,
    string? Chain,
    string? Network,
    DateTimeOffset? VerifiedAtUtc,
    string? Commitment,
    long? Slot,
    DateTimeOffset? BlockTimeUtc,
    string? AnchorerPubkey
);
