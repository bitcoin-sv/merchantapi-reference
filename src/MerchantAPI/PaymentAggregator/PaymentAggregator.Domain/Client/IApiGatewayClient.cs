// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Domain.Client
{
  public interface IApiGatewayClient
  {
    public string Url { get; set; }
    Task TestMapiFeeQuoteAsync(CancellationToken token);
    Task<SignedPayloadViewModel> GetFeeQuoteAsync(CancellationToken token);
    Task<SignedPayloadViewModel> QueryTransactionStatusAsync(string txId, CancellationToken token);
    Task<SignedPayloadViewModel> SubmitTransactionAsync(string payload, CancellationToken token);
    Task<SignedPayloadViewModel> SubmitTransactionsAsync(string payload, CancellationToken token);

    Task<SignedPayloadViewModel> SubmitRawTransactionAsync(
      byte[] payload, 
      string callbackUrl, 
      string callbackToken, 
      string callbackEncryption, 
      bool merkleProof, 
      bool dsCheck,
      CancellationToken token);

    Task<SignedPayloadViewModel> SubmitRawTransactionsAsync(
      byte[] payload,
      string callbackUrl,
      string callbackToken,
      string callbackEncryption,
      bool merkleProof,
      bool dsCheck,
      CancellationToken token);
  }
}
