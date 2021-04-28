// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common;
using System.Reflection;

namespace MerchantAPI.APIGateway.Domain
{
  public class Const
  {
    public const string MERCHANT_API_VERSION = "1.3.0";

    public readonly static string MERCHANT_API_BUILD_VERSION = GetBuildVersion();
    private static string GetBuildVersion()
    {
      return Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
    }

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

}
