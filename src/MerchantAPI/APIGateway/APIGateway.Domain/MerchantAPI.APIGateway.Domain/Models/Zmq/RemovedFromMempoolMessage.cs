using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Domain.Models.Zmq
{
  public class RemovedFromMempoolMessage
  {
    public static class Reasons
    {
      public const string CollisionInBlockTx = "collision-in-block-tx";
    }
    [JsonPropertyName("txid")]
    public string TxId { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; }

    [JsonPropertyName("collidedWith")]
    public CollidedWith CollidedWith { get; set; }

    [JsonPropertyName("blockhash")]
    public string BlockHash { get; set; }
  }

  // same as MerchantAPI.Common.BitcoinRpc.Responses.CollidedWith
  public class CollidedWith
  {

    [JsonPropertyName("txid")]
    public string TxId { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("hex")]
    public string Hex { get; set; }

  };
}
