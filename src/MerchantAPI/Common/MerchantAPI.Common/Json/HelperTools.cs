// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.Common.Exceptions;
using NBitcoin;
using NBitcoin.Crypto;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MerchantAPI.Common.Json
{
  public static class HelperTools
  {
    const int BufferChunkSize = 1024 * 1024;

    // we reuse the same options instance and avoid performance penalty
    // see: https://www.meziantou.net/avoid-performance-issue-with-jsonserializer-by-reusing-the-same-instance-of-json.htm
    static readonly System.Text.Json.JsonSerializerOptions SerializerOptions = new()
    {
      IgnoreNullValues = true,
      // \u0022 -> \"
      Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    static readonly System.Text.Json.JsonSerializerOptions SerializerOptionsIndented = new()
    {
      IgnoreNullValues = true,
      WriteIndented = true,
      // \u0022 -> \"
      Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static async Task<byte[]> HexStringToByteArrayAsync(Stream stream)
    {
      IList<byte> outputBuffer = new List<byte>();
      using var strReader = new StreamReader(stream);
      do
      {
        var chBuffer = new char[BufferChunkSize];
        var readSize = await strReader.ReadBlockAsync(chBuffer, 0, BufferChunkSize);
        for (int i = 0; i < (readSize / 2); i++)
        {
          var hexChar = new char[] { chBuffer[i * 2], chBuffer[i * 2 + 1] };
          var byteVal = int.Parse(hexChar, NumberStyles.AllowHexSpecifier);
          if (byteVal < Byte.MinValue || byteVal > Byte.MaxValue)
          {
            throw new OverflowException($"Byte value exceeds limits 0-255");
          }
          outputBuffer.Add((byte)byteVal);
        }
      }
      while (!strReader.EndOfStream);

      return outputBuffer.ToArray();
    }

    public static bool AreByteArraysEqual(byte[] a1, byte[] a2)
    {
      var a1Length = a1.Length;
      if (a1Length != a2.Length)
        return false;

      for (int i = 0; i < a1Length; i++)
      {
        if (a1[i] != a2[i])
        {
          return false;
        }
      }

      return true;
    }

    public static DateTime GetEpochTime(long dateValue)
    {
      var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
      return epoch.Add(TimeSpan.FromSeconds(dateValue));
    }

    /// <summary>
    /// Serializes an object using System.Text.Json serializer.
    /// Before using this method make sure that class has JSon serialization attributes from
    /// the right  namespaces. Mixing Newtonsoft and System.Text.Json serializer will not produce desired results.
    /// </summary>
    public static string JSONSerialize(object value, bool writeIndented)
    {
      if (writeIndented)
      {
        return System.Text.Json.JsonSerializer.Serialize(value, SerializerOptionsIndented);
      }
      else
      {
        return System.Text.Json.JsonSerializer.Serialize(value, SerializerOptions);
      }
    }

    /// <summary>
    /// Deserializes an object using System.Text.Json serializer.
    /// Before using this method make sure that class has JSon serialization attributes from
    /// the right  namespaces. Mixing Newtonsoft and System.Text.Json serializer will not produce desired results.
    /// </summary>
    public static T JSONDeserialize<T>(string value)
    {
      return System.Text.Json.JsonSerializer.Deserialize<T>(value);
    }

    /// <summary>
    /// Serializes an object using Newtonsoft serializer.
    /// Before using this method make sure that class has JSon serialization attributes from
    /// the right  namespaces. Mixing Newtonsoft and System.Text.Json serializer will not produce desired results.
    /// </summary>
    public static string JSONSerializeNewtonsoft(object value, bool writeIndented)
    {
      DefaultContractResolver contractResolver = new()
      {
        NamingStrategy = new CamelCaseNamingStrategy()
      };

      JsonSerializerSettings serializeSettings =
        new()
        {
          ContractResolver = contractResolver,
          Formatting = writeIndented ? Formatting.Indented : Formatting.None,
          NullValueHandling = NullValueHandling.Ignore,
        };

      return Newtonsoft.Json.JsonConvert.SerializeObject(value, serializeSettings);
    }

    /// <summary>
    /// Deserializes an object using Newtonsoft serializer.
    /// Before using this method make sure that class has JSon serialization attributes from
    /// the right  namespaces. Mixing Newtonsoft and System.Text.Json serializer will not produce desired results.
    /// </summary>
    public static T JSONDeserializeNewtonsoft<T>(string value)
    {
      DefaultContractResolver contractResolver = new()
      {
        NamingStrategy = new CamelCaseNamingStrategy()
      };

      JsonSerializerSettings serializeSettings =
        new()
        {
          ContractResolver = contractResolver,
          NullValueHandling = NullValueHandling.Ignore,
        };

      return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(value, serializeSettings);
    }

    // https://stackoverflow.com/questions/311165/how-do-you-convert-a-byte-array-to-a-hexadecimal-string-and-vice-versa/26304129#26304129
    public static byte[] HexStringToByteArray(string input)
    {
      if (string.IsNullOrEmpty(input))
      {
        return Array.Empty<byte>();
      }
      if (input.Length % 2 > 0)
      {
        throw new InvalidOperationException("Input data is of incorrect length");
      }

      try
      {
        var outputLength = input.Length / 2;
        var output = new byte[outputLength];
        for (var i = 0; i < outputLength; i++)
          output[i] = Convert.ToByte(input.Substring(i * 2, 2), 16);
        return output;
      }
      catch(Exception ex)
      {
        throw new BadRequestException("Unable to convert string value to byte[]", ex);
      }
    }

    // https://stackoverflow.com/questions/311165/how-do-you-convert-a-byte-array-to-a-hexadecimal-string-and-vice-versa/14333437#14333437
    // changed the formula to (87 + b + (((b - 10) >> 31) & -39) to get lowercase characters
    public static string ByteToHexString(byte[] bytes)
    {
      char[] c = new char[bytes.Length * 2];
      int b;
      for (int i = 0; i < bytes.Length; i++)
      {
        b = bytes[i] >> 4;
        c[i * 2] = (char)(87 + b + (((b - 10) >> 31) & -39));
        b = bytes[i] & 0xF;
        c[i * 2 + 1] = (char)(87 + b + (((b - 10) >> 31) & -39));
      }
      return new string(c);
    }

    public static Transaction ParseBytesToTransaction(byte[] objectBytes)
    {
      // Create or own MemoryStream, so that we support bigger blocks
      BitcoinStream s = new(new MemoryStream(objectBytes, false), false)
      {
        MaxArraySize = unchecked((int)uint.MaxValue) // NBitcoin internally casts to uint when comparing
      };

      var tx = Transaction.Create(Network.Main);
      tx.ReadWrite(s);
      return tx;

    }

    public static Block ParseBytesToBlock(byte[] objectBytes)
    {
      // Create or own MemoryStream, so that we support bigger blocks
      BitcoinStream s = new(new MemoryStream(objectBytes, false), false);
      s.MaxArraySize = unchecked((int)uint.MaxValue); // NBitcoin internally casts to uint when comparing

      var block = s.ConsensusFactory.CreateBlock();
      block.ReadWrite(s);
      return block;
    }

    public static Block ParseByteStreamToBlock(RpcBitcoinStreamReader streamReader)
    {
      // Create or own MemoryStream, so that we support bigger blocks
      BitcoinStream s = new(streamReader, false);
      s.MaxArraySize = unchecked((int)uint.MaxValue); // NBitcoin internally casts to uint when comparing

      var block = s.ConsensusFactory.CreateBlock();
      block.ReadWrite(s);
      streamReader.Close();
      return block;
    }

    public static byte[][] ParseTransactionsIntoBytes(byte[] multipleTransactions) 
    {
      var transactions = new List<byte[]>();
      // Create or own MemoryStream, so that we support bigger blocks
      BitcoinStream s = new(new MemoryStream(multipleTransactions, false), false);
      s.MaxArraySize = unchecked((int)uint.MaxValue); // NBitcoin internally casts to uint when comparing
      while (s.Inner.Position < s.Inner.Length)
      {
        var tx = Transaction.Create(Network.Main);
        tx.ReadWrite(s);
        transactions.Add(tx.ToBytes());
      }

      return transactions.ToArray();

    }

    /// <summary>
    /// This method should be removed once PR https://github.com/MetacoSA/NBitcoin/pull/1005
    /// will be merged to master and new package will be released.
    /// Original code stores the calculated hash and returns it once it has been calculated so it's more optimal
    /// </summary>
    public static uint256 GetHash(this Transaction tx, int maxArraySize)
    {
      // try to use original implementation because it's more efficient
      try
      {
        return tx.GetHash();
      }
      catch (ArgumentOutOfRangeException) 
      {
        // catch ArgumentOutOfRangeException in case Tx is bigger than 1MB
        // and parse it again by increasing the maxArraySize
      }

      using var hs = new HashStream();
      var stream = new BitcoinStream(hs, true)
      {
        TransactionOptions = TransactionOptions.None,
        ConsensusFactory = tx.GetConsensusFactory(),
        MaxArraySize = maxArraySize
      };
      stream.SerializationTypeScope(SerializationType.Hash);
      tx.ReadWrite(stream);
      return hs.GetHash();
    }
  }
}
