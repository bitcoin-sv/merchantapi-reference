// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MerchantAPI.Common.Exceptions;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;

namespace MerchantAPI.Common.Json
{

  public static class JsonEnvelopeSignature
  {
    public static JsonSerializerOptions SerializeOptionsNoPrettyPrint
    {
      get
      {
        var options = new JsonSerializerOptions
        {
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
          // Force \u0022 -> \"
          Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        return options;
      }
    }

    public static JsonSerializerOptions SerializeOptions
    {
      get
      {
        var options = new JsonSerializerOptions
        {
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
          WriteIndented = true,
          // Force: \u0022 -> \"
          Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        return options;
      }
    }

    /// <summary>
    ///  Calculates double SHA256 hash of payload and returns it as hex string
    /// </summary>
    public static string GetSigHashHash(string payload, string encodingName)
    {
      var sigHash = GetSigHash(payload, encodingName);
      byte[] doubleHash = Hashes.SHA256(sigHash);

      Array.Reverse(doubleHash);

      return Encoders.Hex.EncodeData(doubleHash);
    }

    public static byte[] GetSigHash(string payload, string encodingName) // throws an exception with friendly message if encoding is not found
    {
      byte[] bytes; 
      if (encodingName == "base64")
      {
        // treat as binary
        bytes = Convert.FromBase64String(payload);
      }
      else
      {
        Encoding encoding;
        try
        {
          encoding = Encoding.GetEncoding(encodingName);
        }
        catch (ArgumentException ex)
        {
          throw new BadRequestException($"Unsupported JSonEnvelope encoding :{encodingName} ", ex);
        }

        // treat as string
        bytes = encoding.GetBytes(payload);
      }
      return Hashes.SHA256(bytes);
    }

    public static bool VerifySignature(string jsonString)
    {
      var envelope = JsonSerializer.Deserialize<JsonEnvelope>(jsonString, SerializeOptions);
      return VerifySignature(envelope);
    }

    public static bool VerifySignature(JsonEnvelope envelope)
    {
      if (string.IsNullOrEmpty(envelope.Payload))
      {
        throw new BadRequestException("JsonEnvelope must contain non-empty 'payload'");
      }

      if (string.IsNullOrEmpty(envelope.PublicKey))
      {
        throw new BadRequestException("JsonEnvelope must contain non-empty publicKey");
      }

      if (string.IsNullOrEmpty(envelope.Signature))
      {
        throw new BadRequestException("JsonEnvelope must contain non-empty signature");
      }

      var signature = ECDSASignature.FromDER(Encoders.Hex.DecodeData(envelope.Signature));

      var pubKey = new PubKey(envelope.PublicKey);


      return pubKey.Verify(new uint256(GetSigHash(envelope.Payload, envelope.Encoding)), signature);
    }

    // Maps base68 private key prefixes to netowrk https://en.bitcoin.it/wiki/List_of_address_prefixes
    private static readonly Dictionary<string, Network> prefixToNetwork = new()
    {
      {"5", Network.Main}, // Private key (WIF, uncompressed pubkey)
      {"K", Network.Main}, // Private key (WIF, uncompressed pubkey)
      {"L", Network.Main}, // Private key (WIF, uncompressed pubkey)
      {"xprv", Network.Main}, // Private key (WIF, uncompressed pubkey)

      {"9", Network.TestNet}, // Testnet Private key (WIF, uncompressed pubkey)
      {"c", Network.TestNet},  // Testnet Private key (WIF, compressed pubkey)
      {"tprv", Network.TestNet}, // Testnet Private key (WIF, compressed pubkey)
    };

    public static Key ParseWifPrivateKey(string privateKeyWif)
    {
      if (!prefixToNetwork.TryGetValue(privateKeyWif.Substring(0, 1), out var network))
      {
        throw new BadRequestException("Unknown private key format");
      }
      return  Key.Parse(privateKeyWif, network);
      
    }

    /// <summary>
    /// Create a signature
    /// </summary>
    /// <param name="signHashAsync">A function takes takes sigHashHex as parameter and returns the signature and public key used</param>
    /// <returns></returns>
    public static async Task<JsonEnvelope> CreateSignatureAsync(string payload, string encoding, string mimetype, Func<string, Task<(string signature, string publicKey)>> signHashAsync) 
    {
      var sigHash = new uint256(GetSigHash(payload, encoding));

      var signature = await signHashAsync(sigHash.ToString());
      var envelope = new JsonEnvelope
      {
        Payload = payload,
        Encoding = encoding,
        Mimetype = mimetype,
        PublicKey = signature.publicKey,
        Signature = signature.signature
      };

      return envelope;
    }

    public static JsonEnvelope CreateJSonSignature(string json, string privateKeyWif)
    {
      Task<(string signature, string publicKey)> signWithWif(string sigHashHex)
      {
        var key = ParseWifPrivateKey(privateKeyWif);
        var signature = key.Sign(new uint256(sigHashHex));
        return Task.FromResult((Encoders.Hex.EncodeData(signature.ToDER()), key.PubKey.ToHex()));

      }
      return CreateSignatureAsync(json, Encoding.UTF8.BodyName, MediaTypeNames.Application.Json, signWithWif)
        .Result; // We do not await, since this is a sync call
    }

    /// <summary>
    /// Create a signature
    /// </summary>
    /// <param name="signHash">A function takes takes sigHashHex as parameter and returns the signature</param>
    /// <returns></returns>
    public static Task<JsonEnvelope> CreateJSonSignatureAsync(string json, Func<string, Task<(string signature, string publicKey)>> signHash)
    {
      return CreateSignatureAsync(json, Encoding.UTF8.BodyName.ToUpper(), MediaTypeNames.Application.Json, signHash);
    }

    public static string CreateJSONWithBitcoinSignature(string json, string privateKeyWif, NBitcoin.Network network)
    {
      var key = Key.Parse(privateKeyWif, network);
      string messageSignature = key.SignMessage(json);

      var envelope = new BitcoinSignatureEnvelope
      {
        Payload = json,
        Encoding = Encoding.UTF8.BodyName.ToUpper(),
        Mimetype = MediaTypeNames.Application.Json,
        SignatureBitcoin = messageSignature
      };

      return JsonSerializer.Serialize(envelope, SerializeOptions);
    }

    public static bool VerifyBitcoinSignature(string jsonPayload, string signature, string publicKey, string address, Network network, out string error)
    {
      error = null;
      if (string.IsNullOrEmpty(publicKey) && string.IsNullOrEmpty(address))
      {
        error = "'delegatingPublicKey' and 'delegatingPublicKeyAddress' parameters are not set.";
        return false;
      }

      PubKey pubKey;
      try
      {
        pubKey = PubKey.RecoverFromMessage(jsonPayload, signature);
      }
      catch
      {
        error = "Signature is not valid.";
        return false;
      }

      if (!string.IsNullOrEmpty(publicKey))
      {
        if (pubKey.ToHex() != publicKey)
        {
          error = "Public key that was used to sign the message does not match the one in payload and is invalid.";
          return false;
        }
      }
      if (!string.IsNullOrEmpty(address))
      {
        if (pubKey.GetAddress(ScriptPubKeyType.Legacy, network).ToString() != address)
        {
          error = "Public key address that was used to sign the message does not match the one in payload and is invalid.";
          return false;
        }
      }
      return true;
    }
  }



}
