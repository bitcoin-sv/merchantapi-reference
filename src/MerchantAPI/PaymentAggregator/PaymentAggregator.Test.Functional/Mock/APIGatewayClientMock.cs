// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common;
using MerchantAPI.Common.Clock;
using MerchantAPI.Common.Domain.Models;
using MerchantAPI.Common.Json;
using MerchantAPI.Common.Test.Mock;
using MerchantAPI.PaymentAggregator.Domain.Client;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.Common.ViewModels;
using MerchantAPI.PaymentAggregator.Rest.ViewModels;
using MerchantAPI.PaymentAggregator.Consts;

namespace MerchantAPI.PaymentAggregator.Test.Functional.Mock
{
  public class ApiGatewayClientMock : IApiGatewayClient
  {
    public string Url { get; set; }
    readonly FeeQuoteRepositoryMock feeQuoteRepositoryMock;
    readonly IClock clock;
    const string FailEndpoint = "fail/";
    object objLock = new object();

    public const string APIGATEWAY_API_VERSION = "1.2.0";

    // key = gateway's url, value = list of successful txIds
    static readonly Dictionary<string, List<string>> QueryTxsSuccessByUrl = new Dictionary<string, List<string>>();

    public static void AddUrlWithSuccessTx(string url, string tx)
    {
      lock (QueryTxsSuccessByUrl)
      {
        var txs = QueryTxsSuccessByUrl.GetValueOrDefault(url, new List<string>());
        txs.Add(tx);
        QueryTxsSuccessByUrl[url] = txs;
      }
    }

    public static string CreateUrl(string url, string filenameJson, bool reachable=true)
    {
      return url + (reachable ? "" : FailEndpoint)+ filenameJson + "/";
    }

    private string GetFilenameJson()
    {
      int startOfFilename = Url[..^1].LastIndexOf("/")+1;
      return Url[startOfFilename..(Url.Length-1)];
    }

    public ApiGatewayClientMock(string url, IFeeQuoteRepository feeQuoteRepository, IClock clock)
    {
      this.Url = url;
      this.feeQuoteRepositoryMock = feeQuoteRepository as FeeQuoteRepositoryMock ?? throw new ArgumentNullException(nameof(feeQuoteRepository)); 
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    private void CheckIfUrlIsReachable()
    {
      if (Url.Contains(FailEndpoint, StringComparison.OrdinalIgnoreCase))
      {
        throw new Exception("Url is not reachable.");
      }
    }

    public Task TestMapiFeeQuoteAsync(CancellationToken token)
    {
      CheckIfUrlIsReachable();
      // successful
      return Task.CompletedTask;
    }

    public async Task<SignedPayloadViewModel> GetFeeQuoteAsync(CancellationToken token)
    {
      CheckIfUrlIsReachable();
      FeeQuote feeQuote;
      lock (objLock)
      {
        feeQuoteRepositoryMock.FeeFileName = GetFilenameJson();
        feeQuote = Task.FromResult(feeQuoteRepositoryMock.GetCurrentFeeQuoteByIdentity(null)).Result;
      }

      if (feeQuote == null)
      {
        return null;
      }

      var feeQuoteViewModelGet = new FeeQuoteViewModelGet(feeQuote)
      {
        Timestamp = clock.UtcNow(),
      };
      feeQuoteViewModelGet.ExpiryTime = feeQuoteViewModelGet.Timestamp.Add(TimeSpan.FromMinutes(15));// quoteExpiryMinutes
      
      string payload = HelperTools.JSONSerialize(feeQuoteViewModelGet, false);
     
      return await Task.FromResult(new JSONEnvelopeViewModel(payload));
    }

    public async Task<SignedPayloadViewModel> QueryTransactionStatusAsync(string txId, CancellationToken token)
    {
      CheckIfUrlIsReachable();
      var txStatus = new QueryTransactionStatusResponseViewModel()
      {
        ApiVersion = APIGATEWAY_API_VERSION,
        Timestamp = clock.UtcNow(),
        Txid = txId,
        ReturnResult = QueryTxsSuccessByUrl.GetValueOrDefault(Url, new List<string>()).Contains(txId) ? "success" : "failure",
        ResultDescription = null,
        BlockHash = "blockHash",
        BlockHeight = 100,
        MinerId = ""
      };
      string payload = HelperTools.JSONSerialize(txStatus, false);

      return await Task.FromResult(new JSONEnvelopeViewModel(payload));
    }

    public async Task<SignedPayloadViewModel> SubmitTransactionAsync(string payload, CancellationToken token)
    {
      CheckIfUrlIsReachable();
      var request = JsonSerializer.Deserialize<SubmitTransactionViewModel>(payload);
      var submitTx = GenerateSubmitTxResponse(request.RawTx);
      string responsePayload = HelperTools.JSONSerialize(submitTx, false);
      return await Task.FromResult(new JSONEnvelopeViewModel(responsePayload));      
    }

    public async Task<SignedPayloadViewModel> SubmitTransactionsAsync(string payload, CancellationToken token)
    {
      CheckIfUrlIsReachable();
      var request = JsonSerializer.Deserialize<SubmitTransactionViewModel[]>(payload);
      var txs = new List<SubmitTransactionOneResponseViewModel>();
      foreach (var tx in request)
      {
        var txResp = GenerateSubmitTxResponse(tx.RawTx);
        txs.Add(new SubmitTransactionOneResponseViewModel()
        {
          Txid = txResp.Txid,
          ReturnResult = txResp.ReturnResult,
          ResultDescription = txResp.ResultDescription,
          ConflictedWith = txResp.ConflictedWith
        });
      }
      var response = new SubmitTransactionsResponseViewModel()
      {
        ApiVersion = APIGATEWAY_API_VERSION,
        Timestamp = clock.UtcNow(),
        CurrentHighestBlockHash = "blockHash",
        CurrentHighestBlockHeight = 100,
        FailureCount = txs.Count(t => t.ReturnResult != "success"),
        Txs = txs.ToArray()
      };
      string responsePayload = HelperTools.JSONSerialize(response, false);
      return await Task.FromResult(new JSONEnvelopeViewModel(responsePayload));
    }

    private SubmitTransactionResponseViewModel GenerateSubmitTxResponse(string rawTx)
    {
      var result = new SubmitTransactionResponseViewModel()
      {
        ApiVersion = APIGATEWAY_API_VERSION,
        Timestamp = clock.UtcNow(),
        CurrentHighestBlockHash = "blockHash",
        CurrentHighestBlockHeight = 100,
        MinerId = "",
        ReturnResult = "success",
        ResultDescription = ""
    };
      switch(rawTx)
      {
        case AggregatorTestBase.txC0Hex:
          result.Txid = AggregatorTestBase.txC0Hash;
          break;
        case AggregatorTestBase.txC1Hex:
          result.Txid = AggregatorTestBase.txC1Hash;
          break;
        case AggregatorTestBase.txC2Hex:
          result.Txid = AggregatorTestBase.txC2Hash;
          result.ReturnResult = "failure";
          result.ResultDescription = "Transaction already known";
          break;
        case AggregatorTestBase.txC3Hex:
          result.Txid = AggregatorTestBase.txC3Hash;
          result.ReturnResult = "failure";
          result.ResultDescription = "Missing inputs";
          break;
        default:
          result.ReturnResult = "failure";
          break;
      }
      return result;
    }

    public async Task<SignedPayloadViewModel> SubmitRawTransactionAsync(byte[] payload, string callbackUrl, string callbackToken, string callbackEncryption, bool merkleProof, bool dsCheck, CancellationToken token)
    {
      CheckIfUrlIsReachable();
      var rawTx = HelperTools.ByteToHexString(payload);
      var submitTx = GenerateSubmitTxResponse(rawTx);
      string responsePayload = HelperTools.JSONSerialize(submitTx, false);
      return await Task.FromResult(new JSONEnvelopeViewModel(responsePayload));
    }

    public async Task<SignedPayloadViewModel> SubmitRawTransactionsAsync(byte[] payload, string callbackUrl, string callbackToken, string callbackEncryption, bool merkleProof, bool dsCheck, CancellationToken token)
    {      
      CheckIfUrlIsReachable();
      var rawTxs = HelperTools.ParseTransactionsIntoBytes(payload);
      var txs = new List<SubmitTransactionOneResponseViewModel>();
      foreach (var tx in rawTxs)
      {
        var rawTx = HelperTools.ByteToHexString(tx);
        var txResp = GenerateSubmitTxResponse(rawTx);
        txs.Add(new SubmitTransactionOneResponseViewModel()
        {
          Txid = txResp.Txid,
          ReturnResult = txResp.ReturnResult,
          ResultDescription = txResp.ResultDescription,
          ConflictedWith = txResp.ConflictedWith
        });
      }
      var response = new SubmitTransactionsResponseViewModel()
      {
        ApiVersion = APIGATEWAY_API_VERSION,
        Timestamp = clock.UtcNow(),
        CurrentHighestBlockHash = "blockHash",
        CurrentHighestBlockHeight = 100,
        FailureCount = txs.Count(t => t.ReturnResult != "success"),
        Txs = txs.ToArray()
      };
      string responsePayload = HelperTools.JSONSerialize(response, false);
      return await Task.FromResult(new JSONEnvelopeViewModel(responsePayload));
    }
  }
}
