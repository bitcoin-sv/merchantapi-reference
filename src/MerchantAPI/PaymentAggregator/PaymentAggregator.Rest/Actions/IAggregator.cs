// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.ViewModels;
using MerchantAPI.PaymentAggregator.Rest.ViewModels;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Domain.Actions
{
  public interface IAggregator
  {
    Task<AllFeeQuotesViewModelGet> GetAllFeeQuotesAsync();

    Task<SignedPayloadViewModel[]> QueryTransactionStatusesAsync(string txId);

    Task<SignedPayloadViewModel[]> SubmitTransactionAsync(SubmitTransactionViewModel request);

    Task<SignedPayloadViewModel[]> SubmitTransactionsAsync(SubmitTransactionViewModel[] request);

    Task<SignedPayloadViewModel[]> SubmitRawTransactionAsync(
      byte[] data, 
      string callbackUrl, 
      string callbackToken, 
      string callbackEncryption, 
      bool merkleProof, 
      bool dsCheck);

    Task<SignedPayloadViewModel[]> SubmitRawTransactionsAsync(
      byte[] data,
      string callbackUrl,
      string callbackToken,
      string callbackEncryption,
      bool merkleProof,
      bool dsCheck);
  }
}
