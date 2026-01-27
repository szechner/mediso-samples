using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Types;
using Solnet.Wallet;

namespace Mediso.AuditSample.Infrastructure.Anchoring;

public sealed class SolanaMemoAnchorProvider : IAnchorProvider
{
    private readonly IRpcClient _rpc;
    private readonly Account _payer;
    private readonly ILogger<SolanaMemoAnchorProvider> _logger;

    public SolanaMemoAnchorProvider(IConfiguration cfg, ILogger<SolanaMemoAnchorProvider> logger)
    {
        _logger = logger;
        var rpcUrl = cfg["Solana:RpcUrl"] ?? "https://api.mainnet-beta.solana.com";
        var privateKey = cfg["Solana:PrivateKey"] ?? throw new InvalidOperationException("Solana:PrivateKey missing");
        var publicKey  = cfg["Solana:PublicKey"]  ?? throw new InvalidOperationException("Solana:PublicKey missing");

        _rpc = ClientFactory.GetClient(rpcUrl, _logger);

        _payer = new Account(privateKey, publicKey);
    }

    public async Task<string> AnchorMemoAsync(string memoText, CancellationToken ct)
    {
        // blockhash
        var bh = await _rpc.GetLatestBlockHashAsync(Commitment.Confirmed);
        if (!bh.WasSuccessful) throw new Exception(bh.Reason);

        // memo tx
        var tx = new TransactionBuilder()
            .SetFeePayer(_payer)
            .SetRecentBlockHash(bh.Result.Value.Blockhash)
            .AddInstruction(MemoProgram.NewMemo(_payer, memoText))
            .Build(_payer);

        var send = await _rpc.SendTransactionAsync(tx, skipPreflight: false, commitment: Commitment.Confirmed);
        if (!send.WasSuccessful)
        {
            _logger.LogError("Anchoring failed. Reason {FailedReason}", send.Reason);
            throw new Exception(send.Reason);
        }

        return send.Result; // signature
    }
}