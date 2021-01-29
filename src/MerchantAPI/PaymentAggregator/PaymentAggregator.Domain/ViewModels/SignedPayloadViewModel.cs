// Copyright (c) 2020 Bitcoin Association

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MerchantAPI.Common.Json;
using Newtonsoft.Json;

namespace MerchantAPI.PaymentAggregator.Domain.ViewModels
{
  public class SignedPayloadViewModel
  {
    [Required]
    [JsonPropertyName("payload")]
    public string Payload { get; set; }

    [Required]
    [JsonPropertyName("signature")]
    public string Signature { get; set; }

    [Required]
    [JsonPropertyName("publicKey")]
    public string PublicKey { get; set; }

    [Required]
    [JsonPropertyName("encoding")]
    public string Encoding { get; set; }

    [Required]
    [JsonPropertyName("mimetype")]
    [RegularExpression("application/json")]
    public string Mimetype { get; set; }
    public SignedPayloadViewModel() { }

    public SignedPayloadViewModel(JsonEnvelope jsonEnvelope)
    {
      Payload = jsonEnvelope.Payload;
      Signature = jsonEnvelope.Signature;
      PublicKey = jsonEnvelope.PublicKey;
      Encoding = jsonEnvelope.Encoding;
      Mimetype = jsonEnvelope.Mimetype;
    }

    public JsonEnvelope ToDomainObject()
    {
      return new JsonEnvelope
      {
        Payload = Payload,
        Signature = Signature,
        PublicKey = PublicKey,
        Encoding = Encoding,
        Mimetype = Mimetype
      };
    }

    public T ExtractPayload<T>()
    {
      return System.Text.Json.JsonSerializer.Deserialize<T>(Payload);
    }

  }
}
