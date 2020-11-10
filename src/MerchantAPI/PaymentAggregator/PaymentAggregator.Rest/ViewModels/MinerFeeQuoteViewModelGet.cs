// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.ViewModels;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class MinerFeeQuoteViewModelGet: SignedPayloadViewModel
  {
    [JsonPropertyName("sla")]
    public SLAViewModelGet[] SLA { get; set; }

    public MinerFeeQuoteViewModelGet()
    {
    }

    public MinerFeeQuoteViewModelGet(SignedPayloadViewModel signedPayloadViewModel)
    {
      Payload = signedPayloadViewModel.Payload;
      Signature = signedPayloadViewModel.Signature;
      PublicKey = signedPayloadViewModel.PublicKey;
      Encoding = signedPayloadViewModel.Encoding;
      Mimetype = signedPayloadViewModel.Mimetype;
    }
  }
}
