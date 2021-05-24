// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

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
