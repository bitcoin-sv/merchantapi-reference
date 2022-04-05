// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MerchantAPI.APIGateway.Domain
{
  public class Const
  {
    public const string MERCHANT_API_VERSION = "1.4.0";

    public readonly static string MERCHANT_API_BUILD_VERSION = GetBuildVersion();
    private static string GetBuildVersion()
    {
      return Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
    }

    public static string MinBitcoindRequired()
    {
      return MinBitcoindRequired(MERCHANT_API_VERSION);
    }

    public static string MinBitcoindRequired(string mAPIVersion)
    {
      List<(string mapiVersion, string nodeVersion)> mapiNodeCompatibleVersions = new()
      {
        ( "1.4.0", "1.0.10" ) // mAPI v1.4.0 and up require node 1.0.10
      };
      string version = mapiNodeCompatibleVersions.LastOrDefault(x => mAPIVersion.CompareTo(x.mapiVersion) >= 0).nodeVersion;
      return version;
    }

    public const int NBitcoinMaxArraySize = unchecked((int)uint.MaxValue); // NBitcoin internally casts to uint when comparing

    public const long Megabyte = 1024 * 1024;

    public class FeeType
    {
      public const string Standard = "standard";
      public const string Data = "data";

      public static readonly string[] RequiredFeeTypes = { Standard, Data };
    }

    public class AmountType
    {
      public const string MiningFee = "MiningFee";
      public const string RelayFee = "RelayFee";
    }
  }

  public class CallbackReason
  {
    public const string MerkleProof = "merkleProof";
    public const string DoubleSpend = "doubleSpend";
    public const string DoubleSpendAttempt = "doubleSpendAttempt";
  }

  public class ZMQTopic
  {
    public const string HashBlock = "hashblock";
    public const string InvalidTx = "invalidtx";
    public const string DiscardedFromMempool = "discardedfrommempool";

    public static readonly string[] RequiredZmqTopics = { "pubhashblock", "pubdiscardedfrommempool", "pubinvalidtx" };
  }

  public class MerkleFormat
  {
    public const string TSC = "TSC";

    public static readonly string[] ValidFormats = { TSC };
  }

  public static class TxStatus
  {
    public const int NotPresentInDb = -100;

    // only for authenticated users
    public const int NodeRejected = -2;
    public const int SentToNode = -1; // if MRI fails after sendrawtxs - user should retry

    // all users
    public const int UnknownOldTx = 0; // old txs - most of them are on blockchain (they have policyQuoteId unknown)
    public const int Mempool = 1;
    public const int MissingInputsMaxRetriesReached = 2;
    public const int Blockchain = 3;

    public static readonly int[] MapiSuccessTxStatuses =
      { UnknownOldTx ,Mempool, MissingInputsMaxRetriesReached, Blockchain };
  }
}
