// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MerchantAPI.Common.BitcoinRpc.Responses
{
  public class RpcGetBlockWithTxIds
  {
    [JsonPropertyName("tx")]
    public List<string> Tx { get; set; }
    [JsonPropertyName("hash")]
    public string Hash { get; set; }
    [JsonPropertyName("confirmations")]
    public long Confirmations { get; set; }
    [JsonPropertyName("size")]
    public long Size { get; set; }
    [JsonPropertyName("height")]
    public long Height { get; set; }
    [JsonPropertyName("version")]
    public long Version { get; set; }
    [JsonPropertyName("versionHex")]
    public string VersionHex { get; set; }
    [JsonPropertyName("merkleroot")]
    public string Merkleroot { get; set; }
    [JsonPropertyName("num_tx")]
    public long NumTx { get; set; }
    [JsonPropertyName("time")]
    public long Time { get; set; }
    [JsonPropertyName("mediantime")]
    public long Mediantime { get; set; }
    [JsonPropertyName("nonce")]
    public long Nonce { get; set; }
    [JsonPropertyName("bits")]
    public string Bits { get; set; }
    [JsonPropertyName("difficulty")]
    public double Difficulty { get; set; }
    [JsonPropertyName("chainwork")]
    public string Chainwork { get; set; }
    [JsonPropertyName("previousblockhash")]
    public string Previousblockhash { get; set; }

  }
  public partial class RpcGetBlock
  {
    [JsonPropertyName("tx")]
    public RpcTx[] Tx { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    [JsonPropertyName("confirmations")]
    public long Confirmations { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("height")]
    public long Height { get; set; }

    [JsonPropertyName("version")]
    public long Version { get; set; }

    [JsonPropertyName("versionHex")]
    public string VersionHex { get; set; }

    [JsonPropertyName("merkleroot")]
    public string Merkleroot { get; set; }

    [JsonPropertyName("num_tx")]
    public long NumTx { get; set; }

    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("mediantime")]
    public long Mediantime { get; set; }

    [JsonPropertyName("nonce")]
    public long Nonce { get; set; }

    [JsonPropertyName("bits")]
    public string Bits { get; set; }

    [JsonPropertyName("difficulty")]
    public double Difficulty { get; set; }

    [JsonPropertyName("chainwork")]
    public string Chainwork { get; set; }

    [JsonPropertyName("previousblockhash")]
    public string Previousblockhash { get; set; }

    [JsonPropertyName("nextblockhash")]
    public string Nextblockhash { get; set; }
  }

  [Serializable]
  public partial class RpcTx
  {
    [JsonPropertyName("txid")]
    public string Txid { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    [JsonPropertyName("version")]
    public long Version { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("locktime")]
    public long Locktime { get; set; }

    [JsonPropertyName("vin")]
    public RpcVin[] Vin { get; set; }

    [JsonPropertyName("vout")]
    public RpcVout[] Vout { get; set; }

    [JsonPropertyName("hex")]
    public string Hex { get; set; }
  }

  [Serializable]
  public partial class RpcVin
  {
    [JsonPropertyName("coinbase")]
    public string Coinbase { get; set; }

    [JsonPropertyName("sequence")]
    public long Sequence { get; set; }
  }

  [Serializable]
  public partial class RpcVout
  {
    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("n")]
    public long N { get; set; }

    [JsonPropertyName("scriptPubKey")]
    public RpcScriptPubKey ScriptPubKey { get; set; }
  }

  [Serializable]
  public partial class RpcScriptPubKey
  {
    [JsonPropertyName("asm")]
    public string Asm { get; set; }

    [JsonPropertyName("hex")]
    public string Hex { get; set; }

    [JsonPropertyName("reqSigs")]
    public long ReqSigs { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("addresses")]
    public string[] Addresses { get; set; }
  }



  [Serializable]
  public partial class RpcSendTransactionsRequestOne
  {
    [JsonPropertyName("hex")]
    public string Hex { get; set; }

    [JsonPropertyName("allowhighfees")]
    public bool AllowHighFees { get; set; }

    [JsonPropertyName("dontcheckfee")]
    public bool DontCheckFee { get; set; }

    [JsonPropertyName("listunconfirmedancestors")]
    public bool ListUnconfirmedAncestors { get; set; }

    [JsonPropertyName("config")]
    public Dictionary<string, object> Config { get; set; }
  }

  [Serializable]
  public partial class RpcSendTransactions
  {

    [JsonPropertyName("known")]
    public string[] Known { get; set; }

    [JsonPropertyName("evicted")]
    public string[] Evicted { get; set; }

    [JsonPropertyName("invalid")]
    public RpcInvalidTx[] Invalid { get; set; }

    [JsonPropertyName("unconfirmed")]
    public RpcUnconfirmedTx[] Unconfirmed { get; set; }


    [Serializable]
    public class RpcInvalidTx
    {

      [JsonPropertyName("txid")]
      public string Txid { get; set; }

      [JsonPropertyName("reject_code")]
      public int? RejectCode { get; set; }

      [JsonPropertyName("reject_reason")]
      public string RejectReason { get; set; }

      [JsonPropertyName("collidedWith")]
      public RpcCollisionTx[] CollidedWith { get; set; }
    }

    [Serializable]
    public class RpcCollisionTx
    {

      [JsonPropertyName("txid")]
      public string Txid { get; set; }

      [JsonPropertyName("size")]
      public long Size { get; set; }

      [JsonPropertyName("hex")]
      public string Hex { get; set; }
    }

    [Serializable]
    public class RpcUnconfirmedTx
    {
      [JsonPropertyName("txid")]
      public string Txid { get; set; }

      [JsonPropertyName("ancestors")]
      public RpcUnconfirmedAncestor[] Ancestors { get; set; }
    }

    [Serializable]
    public class RpcUnconfirmedAncestor
    {
      [JsonPropertyName("txid")]
      public string Txid { get; set; }

      [JsonPropertyName("vin")]
      public RpcUnconfirmedAncestorVin[] Vin { get; set; }
    }

    [Serializable]
    public class RpcUnconfirmedAncestorVin
    {
      [JsonPropertyName("txid")]
      public string Txid { get; set; }

      [JsonPropertyName("vout")]
      public int Vout { get; set; }
    }

  }
}
