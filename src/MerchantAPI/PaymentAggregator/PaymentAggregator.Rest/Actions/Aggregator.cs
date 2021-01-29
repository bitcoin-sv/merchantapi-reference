// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Consts;
using MerchantAPI.PaymentAggregator.Domain.Client;
using MerchantAPI.PaymentAggregator.Domain.Models;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using MerchantAPI.PaymentAggregator.Domain.ViewModels;
using MerchantAPI.PaymentAggregator.Rest.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Rest.Actions
{
  public class Aggregator : IAggregator
  {
    readonly IServiceLevelRepository serviceLevelRepository;
    readonly IApiGatewayMultiClient apiGatewayMultiClient;

    public Aggregator(
      IServiceLevelRepository serviceLevelRepository,
      IApiGatewayMultiClient apiGatewayMultiClient
    )
    {
      this.serviceLevelRepository = serviceLevelRepository ?? throw new ArgumentNullException(nameof(serviceLevelRepository));
      this.apiGatewayMultiClient = apiGatewayMultiClient ?? throw new ArgumentNullException(nameof(apiGatewayMultiClient));
    }

    private int GetFeeAmountLevelFromFeeAmounts(FeeAmount feeAmount, FeeAmount[] feeAmounts)
    {
      var feesSpB = feeAmounts.Select(x => x.GetSatoshiPerByte()).ToList();
      feesSpB.Add(int.MaxValue);
      var level = feesSpB.FindIndex(0, x => x > feeAmount.GetSatoshiPerByte());
      return level;
    }

    public async Task<AllFeeQuotesViewModelGet> GetAllFeeQuotesAsync()
    {
      List<MinerFeeQuoteViewModelGet> miners = new List<MinerFeeQuoteViewModelGet>();
      var serviceLevels = serviceLevelRepository.GetServiceLevels().ToArray();

      using CancellationTokenSource cts = new CancellationTokenSource(2000);
      var responses = await apiGatewayMultiClient.GetFeeQuoteAsync(cts.Token);

      foreach (var response in responses.Where(x => x != null))
      {
        List<SLAViewModelGet> sla = new List<SLAViewModelGet>();
        var payload = response.ExtractPayload<FeeQuoteViewModelGet>();
        foreach (var feeType in Consts.Const.FeeType.RequiredFeeTypes)
        {
          var fee = payload.Fees.FirstOrDefault(x => x.FeeType == feeType);
          if (fee != null) // only add sla if fee with this feeType is present
          {
            var fees = serviceLevels.Where(x => x.Fees != null)  // last serviceLevel has Fees null
              .OrderBy(x => x.Level)
              .SelectMany(x => x.Fees)
              .Where(x => x?.FeeType == feeType).ToArray();
            var miningLevel = GetFeeAmountLevelFromFeeAmounts(fee.MiningFee.ToDomainObject(Const.AmountType.MiningFee), fees.Select(x => x.MiningFee).ToArray());
            var relayLevel = GetFeeAmountLevelFromFeeAmounts(fee.RelayFee.ToDomainObject(Const.AmountType.RelayFee), fees.Select(x => x.RelayFee).ToArray());
            var level = Math.Min(miningLevel, relayLevel);
            sla.Add(new SLAViewModelGet(serviceLevels[level], feeType));
          }
        }
        var miner = new MinerFeeQuoteViewModelGet(response)
        {
          SLA = sla.ToArray()
        };
        miners.Add(miner);
      }

      if (miners.Any())
      {
        AllFeeQuotesViewModelGet all = new AllFeeQuotesViewModelGet
        {
          Miner = miners.ToArray()
        };
        return all;
      }
      return null;
    }

    public async Task<SignedPayloadViewModel[]> QueryTransactionStatusesAsync(string txId)
    {
      using CancellationTokenSource cts = new CancellationTokenSource(5000);
      var responses = await apiGatewayMultiClient.QueryTransactionStatusAsync(txId, cts.Token);
      return responses;
    }

    public async Task<SignedPayloadViewModel[]> SubmitTransactionAsync(SubmitTransactionViewModel request)
    {
      using CancellationTokenSource cts = new CancellationTokenSource(5000);
      var payload = JsonSerializer.Serialize(request);
      var responses = await apiGatewayMultiClient.SubmitTransactionAsync(payload, cts.Token);
      return responses;
    }

    public async Task<SignedPayloadViewModel[]> SubmitTransactionsAsync(SubmitTransactionViewModel[] request)
    {
      using CancellationTokenSource cts = new CancellationTokenSource(5000);
      var payload = JsonSerializer.Serialize(request);
      var responses = await apiGatewayMultiClient.SubmitTransactionsAsync(payload, cts.Token);
      return responses;
    }

    public async Task<SignedPayloadViewModel[]> SubmitRawTransactionAsync(byte[] data, string callbackUrl, string callbackToken, string callbackEncryption, bool merkleProof, bool dsCheck)
    {
      using CancellationTokenSource cts = new CancellationTokenSource(5000);
      var responses = await apiGatewayMultiClient.SubmitRawTransactionAsync(data, callbackUrl, callbackToken, callbackEncryption, merkleProof, dsCheck, cts.Token);
      return responses;
    }

    public async Task<SignedPayloadViewModel[]> SubmitRawTransactionsAsync(byte[] data, string callbackUrl, string callbackToken, string callbackEncryption, bool merkleProof, bool dsCheck)
    {
      using CancellationTokenSource cts = new CancellationTokenSource(5000);
      var responses = await apiGatewayMultiClient.SubmitRawTransactionsAsync(data, callbackUrl, callbackToken, callbackEncryption, merkleProof, dsCheck, cts.Token);
      return responses;
    }
  }
}
