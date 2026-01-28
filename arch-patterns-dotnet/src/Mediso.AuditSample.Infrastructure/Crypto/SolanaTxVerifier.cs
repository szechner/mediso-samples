using System.Text;
using Mediso.AuditSample.Infrastructure.Anchoring;
using Microsoft.Extensions.Logging;
using Solnet.Rpc;
using Solnet.Rpc.Models;
using Solnet.Rpc.Types;
using Solnet.Wallet.Utilities;

namespace Mediso.AuditSample.Infrastructure.Solana;

public sealed class SolanaTxVerifier
{
    private readonly IRpcClient _rpc;
    private readonly ILogger<SolanaTxVerifier> _log;

    public SolanaTxVerifier(IRpcClient rpc, ILogger<SolanaTxVerifier> log)
    {
        _rpc = rpc;
        _log = log;
    }

    public async Task<SolanaVerifyResult> VerifyMemoAsync(string signature, string expectedMemo, CancellationToken ct)
    {
        var res = await _rpc.GetTransactionAsync(signature, commitment: Commitment.Finalized);

        if (!res.WasSuccessful)
        {
            var reason = res.Reason ?? "rpc call failed";
            // Solana public RPC často vrací "Too many requests from your IP"
            if (reason.Contains("Too many requests", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("429", StringComparison.OrdinalIgnoreCase))
            {
                return SolanaVerifyResult.RateLimited(reason);
            }

            return SolanaVerifyResult.Inconclusive($"rpc error: {reason}");
        }

        if (res.Result == null)
            return SolanaVerifyResult.Inconclusive("tx not found (yet).");

        // Solnet: res.Result má Transaction + Meta + Slot + BlockTime
        var slot = checked((long)(res.Result!.Slot));
        DateTimeOffset? blockTimeUtc = null;
        if (res.Result.BlockTime != null)
        {
            blockTimeUtc = DateTimeOffset.FromUnixTimeSeconds(res.Result.BlockTime.Value).ToUniversalTime();
        }

        // fee payer = první account key (typicky)
        var payer = res.Result.Transaction?.Message?.AccountKeys?.FirstOrDefault() ?? "";

        // memo: hledej instrukci Memo Programu
        var memo = TryExtractMemo(res.Result?.Transaction);

        if (memo == null)
            return SolanaVerifyResult.NotVerified("memo not found in transaction.");

        if (!string.Equals(memo, expectedMemo, StringComparison.Ordinal))
            return SolanaVerifyResult.NotVerified("memo mismatch.");
        
        return SolanaVerifyResult.Verified(
            commitment: "finalized",
            slot: slot,
            blockTimeUtc: blockTimeUtc,
            anchorerPubkey: payer
        );
    }

    private static string? TryExtractMemo(TransactionInfo? tx)
    {
        // V Solaně je memo buď jako instrukce s programId Memo, nebo parsed instruction.
        // V Solnet response se to liší podle encodingu.
        // Tady zkusíme několik cest – upravíš podle toho, co ti vrací konkrétní RPC.

        var message = tx?.Message;
        if (message == null) return null;

        // 1) Parsed instructions (pokud RPC vrací jsonParsed)
        // 2) Raw data instructions (base64)
        // Zjednodušeně: zkus projít instrukce a najít programId obsahující "Memo"
        // (v praxi: Memo programId = MemoSq4gqABAXKb96qnH8TysNcWxMyWCqXgDLGmfcHr)
        var memoProgramId = "MemoSq4gqABAXKb96qnH8TysNcWxMyWCqXgDLGmfcHr";
        var memoProgramId2 = "Memo1UhkJRfHyvLMcVucJwxXeuD728EqVDDwQDxFMNo";

        foreach (var ix in message.Instructions)
        {
            // Solnet: ix.ProgramIdIndex → účet v AccountKeys
            var programId = message.AccountKeys[ix.ProgramIdIndex];
            if (!string.Equals(programId, memoProgramId, StringComparison.Ordinal) && !string.Equals(programId, memoProgramId2, StringComparison.Ordinal))
                continue;

            // ix.Data bývá base58/base64 dle response; Solnet drží string.
            // Memo program používá "data" jako raw bytes textu.
            // Pokud je to base58, je potřeba dekódovat. Pokud je to plain, tak rovnou.
            // Pro demo: pokud to vypadá jako ASCII, vraťme přímo.
            var data = ix.Data;
            if (string.IsNullOrWhiteSpace(data)) continue;

            // Heuristika: pokud obsahuje 'mediso.audit.v1|' tak je to plain
            if (data.Contains("mediso.audit.v1|", StringComparison.Ordinal))
                return data;

            // Jinak: v reálu by tu šla dekódovací větev (base58) – doplníme, pokud to bude potřeba.
            return TryDecodeMemo(data) ?? data;
        }

        return null;
    }
    
    static string? TryDecodeMemo(string data)
    {
        try
        {
            var bytes = Encoders.Base58.DecodeData(data);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }
}

public sealed record SolanaVerifyResult(
    string Verdict, // VERIFIED / NOT_VERIFIED / INCONCLUSIVE
    string? Commitment,
    long? Slot,
    DateTimeOffset? BlockTimeUtc,
    string? AnchorrerPubkey,
    string? Problem
)
{
    public static SolanaVerifyResult Verified(string commitment, long slot, DateTimeOffset? blockTimeUtc, string anchorerPubkey)
        => new("VERIFIED", commitment, slot, blockTimeUtc, anchorerPubkey, null);

    public static SolanaVerifyResult NotVerified(string problem)
        => new("NOT_VERIFIED", null, null, null, null, problem);

    public static SolanaVerifyResult Inconclusive(string problem)
        => new("INCONCLUSIVE", null, null, null, null, problem);
    
    public static SolanaVerifyResult RateLimited(string problem)
        => new("RATE_LIMITED", null, null, null, null, problem);
}
