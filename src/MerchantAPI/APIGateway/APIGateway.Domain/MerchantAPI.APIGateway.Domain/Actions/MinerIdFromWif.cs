// Copyright (c) 2020 Bitcoin Association

using System;
using System.Threading.Tasks;
using MerchantAPI.Common.Json;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace MerchantAPI.APIGateway.Domain.Actions
{

  /// <summary>
  ///  Implementation of MinerId signing that reads private key from config file
  /// </summary>
  public class MinerIdFromWif : IMinerId
  {
    Key privateKey;
    string publicKey;

    public MinerIdFromWif(string wifPrivateKey)
    {
      if (string.IsNullOrEmpty(wifPrivateKey))
      {
        throw new ArgumentNullException(nameof(wifPrivateKey));
      }

      privateKey = JsonEnvelopeSignature.ParseWifPrivateKey(wifPrivateKey);
      publicKey = privateKey.PubKey.ToString();
    }


    public Task<string> GetCurrentMinerIdAsync()
    {
      return Task.FromResult(publicKey);
    }

    public Task<string> SignWithMinerIdAsync(string currentMinerId, string hash)
    {
      if (currentMinerId != publicKey)
      {
        throw new ArgumentException($"Unexpected public key in SignWithMinerIdAsync. Expected {currentMinerId}, but got {publicKey}");
      }
      var sigHash = new uint256(hash);
      var signature = privateKey.Sign(sigHash);
      return Task.FromResult(Encoders.Hex.EncodeData(signature.ToDER())); 
    }
  }


}
