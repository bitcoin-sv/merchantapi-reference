// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Actions;

namespace MerchantAPI.APIGateway.Domain.Models.APIStatus
{
  public class SubmitTxStatus
  {
    public double Request { get; private set; }
    public double TxAuthenticatedUser { get; private set; }
    public double TxAnonymousUser { get; private set; }
    public double Tx => TxAuthenticatedUser + TxAnonymousUser;
    public double AvgBatch => Request > 0 ? Tx / (double)Request : 0;
    public double TxSentToNode { get; private set; }
    public double TxAcceptedByNode { get; private set; }
    public double TxRejectedByNode { get; private set; }
    public double TxSubmitException { get; private set; }
    public double TxResponseSuccess { get; private set; }
    public double TxResponseFailure { get; private set; }
    public double TxResponseFailureRetryable { get; private set; }
    public double TxWithoutResponse => Tx - TxResponseFailure - TxResponseSuccess;
    public double TxMissingInputs { get; private set; }
    public double TxReSentMissingInputs { get; private set; }
    public double TxWasMinedMissingInputs { get; private set; }
    public double TxInvalidBlockMissingInputs { get; private set; }

    public SubmitTxStatus(double request, double txAuthenticatedUser, double txAnonymousUser,
      double txSentToNode, double txAcceptedByNode, double txRejectedByNode, double txSubmitException, 
      double txResponseSuccess, double txResponseFailure, double txResponseFailureRetryable,
      double txMissingInputs, double txReSentMissingInputs, double txWasMinedMissingInputs, double txInvalidBlockMissingInputs)
    {
      Request = request;
      TxAuthenticatedUser = txAuthenticatedUser;
      TxAnonymousUser = txAnonymousUser;
      TxSentToNode = txSentToNode;
      TxAcceptedByNode = txAcceptedByNode;
      TxRejectedByNode = txRejectedByNode;
      TxSubmitException = txSubmitException;
      TxResponseSuccess = txResponseSuccess;
      TxResponseFailure = txResponseFailure;
      TxResponseFailureRetryable = txResponseFailureRetryable;
      TxMissingInputs = txMissingInputs;
      TxReSentMissingInputs = txReSentMissingInputs;
      TxWasMinedMissingInputs = txWasMinedMissingInputs;
      TxInvalidBlockMissingInputs = txInvalidBlockMissingInputs;
    }

    public string SubmitTxDescription
    {
      get
      {
        return $@"Number of requests: {Request}, all transactions processed: {Tx} (authenticated: {TxAuthenticatedUser}, anonymous: {TxAnonymousUser}). Average batch: {AvgBatch}. 
Transactions sent to node: {TxSentToNode}. Accepted by node: {TxAcceptedByNode}, rejected by node: {TxRejectedByNode}, submit exceptions: {TxSubmitException}.
Transaction responses with success: {TxResponseSuccess}, failure: {TxResponseFailure} (retryable: {TxResponseFailureRetryable}), processing/exceptions: {TxWithoutResponse}. 
All missing inputs: {TxMissingInputs} (resent: {TxReSentMissingInputs}, was mined: {TxWasMinedMissingInputs}, invalid block: {TxInvalidBlockMissingInputs}).";
      }
    }

    public string PrepareForLogging()
    {
      return SubmitTxDescription;
    }
  }
}
