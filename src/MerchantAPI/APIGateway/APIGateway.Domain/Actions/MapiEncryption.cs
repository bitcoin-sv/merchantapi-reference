// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Sodium;
using System;
using System.Linq;
using System.Text;

namespace MerchantAPI.APIGateway.Domain.Actions
{
  public static class MapiEncryption
  {
    static bool TryExtractEncryptionKey(string callbackEncryption, out byte[] key)
    {
      key = null;
      // libsodium sealed_box <blob>"
      var parts = callbackEncryption.Split(' ')
        .Select(x => x.Trim())
        .Where(x => !string.IsNullOrEmpty(x))
        .ToArray();

      if (parts.Length != 3)
      {
        return false;
      }

      if (parts[0] != "libsodium" || parts[1] != "sealed_box")
      {
        return false;
      }

      try
      {
        key = Convert.FromBase64String(parts[2]);
        return true;
      }
      catch (FormatException)
      {
        return false;
      }
    }

    public static string GetEncryptionKey(KeyPair keypair)
    {
      return "libsodium sealed_box " + Convert.ToBase64String(keypair.PublicKey);
    }

    public static bool IsEncryptionSupported(string callbackEncryption)
    {
      return TryExtractEncryptionKey(callbackEncryption, out _);
    }

    public static byte[] Encrypt(string json, string callbackEncryption)
    {
      if (!TryExtractEncryptionKey(callbackEncryption, out var key))
      {
        throw new ArgumentException("Can not extract encryption key");
      }

      return SealedPublicKeyBox.Create(Encoding.UTF8.GetBytes(json), key);
    }

    public static string Decrypt(byte[] encrypted, KeyPair keyPair)
    {
      return Encoding.UTF8.GetString(SealedPublicKeyBox.Open(encrypted, keyPair));
    }

  }
}
