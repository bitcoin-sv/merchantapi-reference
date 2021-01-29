// Copyright (c) 2020 Bitcoin Association


namespace MerchantAPI.Common.Json
{

    /// <summary>
    /// Json envelope as defined by "BRFC 6a7a2dec8b17 jsonEnvelope"
    /// C# property names are written in capital names. Use To correctly serialize/deserialize
    /// Use PropertyNamingPolicy = JsonNamingPolicy.CamelCase to correctly serilize/deserialize
    /// </summary>
    public class JsonEnvelope
    {

      /// <summary>
      ///  payload of data being sent
      /// </summary>
      public string Payload { get; set; }

      /// <summary>
      /// signature signature on payload(string)
      /// </summary>
      public string Signature { get; set; }

      /// <summary>
      ///  public key to verify signature
      /// </summary>
      public string PublicKey { get; set; }


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
