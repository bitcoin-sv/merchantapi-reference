// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Text.Json.Serialization;

namespace MerchantAPI.Common.BitcoinRpc.Responses
{
  [Serializable]
  public partial class RpcGetTxOuts : IComparable
  {
    [JsonPropertyName("txouts")]
    public PrevOut[] TxOuts { get; set; }

    public int CompareTo(object obj)
    {
      if (obj == null)
      {
        return 1;
      }
      if (obj is not RpcGetTxOuts otherPrevOut)
      {
        throw new ArgumentException("Object is not a RpcGetTxOuts");
      }
      if (otherPrevOut.TxOuts.Length != TxOuts.Length)
      {
        return 1;
      }
      for (int i=0; i < TxOuts.Length; i++)
      {        
        if (otherPrevOut.TxOuts[i].Error != TxOuts[i].Error)
        {
          return 1;
        }
      }
      return 0;
    }
  }


  [Serializable]
  public partial class CollidedWith
  {

    [JsonPropertyName("txid")]
    public string TxId { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("hex")]
    public string Hex{ get; set; }

  };
 

  /// <summary>
  /// Information about unspent coin.
  /// IMPORTANT: Some of the fields might not be returned from bitcoind
  ///            Set of fields depends on "returnFields" parameter passed to gettxouts RPC function
  /// </summary>
  [Serializable]
  public partial class PrevOut
  {
    [JsonPropertyName("error")]
    public string Error { get; set; }

    [JsonPropertyName("collidedWith")]
    public CollidedWith CollidedWith { get; set; }

    // scriptPubKey in hex string
    [JsonPropertyName("scriptPubKey")]
    public string ScriptPubKey { get; set; }

    [JsonPropertyName("scriptPubKeyLen")]
    public long? ScriptPubKeyLength { get; set; }

    [JsonPropertyName("value")]
    public decimal? Value { get; set; }

    [JsonPropertyName("isStandard")]
    public bool? IsStandard { get; set; }

    [JsonPropertyName("confirmations")]
    public long? Confirmations { get; set; }
  }

  [Serializable]
  public partial class GetTxOutsInput
  {
    [JsonPropertyName("txid")]
    public string TxId { get; set; }

    [JsonPropertyName("n")]
    public long N { get; set; }


  }
}
