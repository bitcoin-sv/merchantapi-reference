// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.ViewModels;

namespace MerchantAPI.APIGateway.Rest.ViewModels
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
