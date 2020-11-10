// Copyright (c) 2020 Bitcoin Association

namespace MerchantAPI.Common.ViewModels
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
