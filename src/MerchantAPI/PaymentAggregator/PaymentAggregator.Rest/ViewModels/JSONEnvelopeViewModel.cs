// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain.ViewModels;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class JSONEnvelopeViewModel : SignedPayloadViewModel
  {

    public JSONEnvelopeViewModel() { }

    public JSONEnvelopeViewModel(string payload)
    {
      Payload = payload;
      Signature = null;
      PublicKey = null;
      Encoding = "json";
      Mimetype = "application/json";
    }
  }
}
