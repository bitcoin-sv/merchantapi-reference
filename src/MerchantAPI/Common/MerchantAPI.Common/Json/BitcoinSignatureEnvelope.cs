// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

namespace MerchantAPI.Common.Json
{
  public class BitcoinSignatureEnvelope
  {
    /// <summary>
    ///  payload of data being sent
    /// </summary>
    public string Payload { get; set; }

    /// <summary>
    /// signature of payload done with bitcoinds signmessage RPC method
    /// </summary>
    public string SignatureBitcoin { get; set; }

    /// <summary>
    /// 	encoding of the payload data
    /// </summary>
    public string Encoding { get; set; }

    /// <summary>
    /// 	mimetype of the payload data
    /// </summary>
    public string Mimetype { get; set; }

  }
}
