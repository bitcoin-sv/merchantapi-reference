// Copyright (c) 2020 Bitcoin Association

using NBitcoin;

namespace MerchantAPI.APIGateway.Domain.DSAccessChecks
{
  public interface ITransactionRequestsCheck
  {
    /// <summary>
    /// Increases the request counter for IP/TxId combination...if the counter exceeds set number, the IP will be banned
    /// </summary>
    void LogKnownTransactionId(string requestIP, uint256 transactionId);
    /// <summary>
    /// Increases the request counter for unknown TxId requests for each IP...if the counter exceeds set number, the IP will be banned
    /// </summary>
    /// <param name="host"></param>
    void LogUnknownTransactionRequest(string host);
    /// <summary>
    /// Stores the successful TxIds queries (transactions that we are interested in) for each IP before call to 'submit' can be made.
    /// </summary>
    void LogQueriedTransactionId(string host, uint256 txId);
    /// <summary>
    /// Check if the IP/TxId query was successful
    /// </summary>
    bool WasTransactionIdQueried(string host, uint256 txId);
    /// <summary>
    /// Remove IP/TxId combination from the list of successful queries
    /// </summary>
    void RemoveQueriedTransactionId(string host, uint256 txId);
  }
}
