// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MerchantAPI.APIGateway.Domain
{
  public class Const
  {
    public const string MERCHANT_API_VERSION = "1.5.0";

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

    public const string DSNT_IDENTIFIER = "64736e74";

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

  public class TxOutFields
  {
    public const string ScriptPubKey = "scriptPubKey";
    public const string ScriptPubKeyLen = "scriptPubKeyLen";
    public const string Value = "value";
    public const string IsStandard = "isStandard";
    public const string Confirmations = "confirmations";

    public static readonly string[] ValidFields = { ScriptPubKey, ScriptPubKeyLen, Value, IsStandard, Confirmations };
  }

  public static class Warning
  {
    public const string MissingDSNT = "Missing DSNT output.";
  }

  public static class TxStatus
  {
    public const int NotPresentInDb = -100;

    // only for authenticated users
    public const int NodeRejected = -2;
    public const int SentToNode = -1; // if MRI fails after sendrawtxs - user should retry

    // all users
    public const int UnknownOldTx = 0; // old txs - most of them are on blockchain (they have policyQuoteId unknown)
    public const int Accepted = 1; // mempool or blockchain
    public const int MissingInputsMaxRetriesReached = 2;

    public static readonly int[] MapiSuccessTxStatuses =
      { UnknownOldTx, Accepted, MissingInputsMaxRetriesReached };
  }

  public static class NodeRejectCode
  {
    public const int Invalid = 16;
    public const int Duplicate = 18;
    public const int NonStandard = 64;
    public const int InsufficientFee = 66;
    public const int AlreadyKnown = 257;
    public const int Conflict = 258;
    public const int MempoolFull = 259;

    public const string ResultAlreadyKnown = "Already known";

    public static readonly int[] MapiSuccessCodes = { AlreadyKnown };

    public static readonly (int code, string reason) MempoolFullCodeAndReason = (InsufficientFee, "mempool full");

    public static readonly string Evicted = "evicted";

    public static readonly List<string> MapiRetryCodesAndReasons = new()
    {
      CombineRejectCodeAndReason(NonStandard, "too-long-mempool-chain"),
      CombineRejectCodeAndReason(InsufficientFee, "mempool min fee not met"),
      CombineRejectCodeAndReason(MempoolFullCodeAndReason.code, MempoolFullCodeAndReason.reason),
      CombineRejectCodeAndReason(MempoolFull, "non-final-pool-full"),
      Evicted
    };

    public const string MapiRetryMempoolError = "Mempool error, retry again later.";
    public const string UnconfirmedAncestorsError = "Transaction is already present in db, but unconfirmed ancestors are missing, retry again.";

    public static string MapiRetryMempoolErrorWithDetails(string rejectCodeAndReason)
    {
      return $"{ MapiRetryMempoolError } (details: {rejectCodeAndReason})";
    }

    public static bool IsResponseOfTypeMissingInputs(string resultDescription)
    {
      return MapiMissingInputs.Any(x => resultDescription.StartsWith(x));
    }

    static readonly HashSet<string> MapiMissingInputs = new()
    {
      CombineRejectCodeAndReason(Invalid, "missing-inputs"),
      CombineRejectCodeAndReason(Conflict, "txn-mempool-conflict"),
      CombineRejectCodeAndReason(Duplicate, "txn-double-spend-detected")
    };

    public static string CombineRejectCodeAndReason(int? rejectCode, string rejectReason)
    {
      return ($"{(rejectCode.HasValue ? rejectCode.ToString() : "") } { rejectReason ?? ""}").Trim();
    }
  }

  public static class Faults
  {
    public enum FaultType
    {
      DbBeforeSavingUncommittedState,
      DbAfterSavingUncommittedState,
      SimulateSendTxsMapi,
      SimulateSendTxsMempoolChecker
    }

    public enum DbFaultMethod
    {
      Exception,
      ProcessExit
    }

    public enum DbFaultComponent
    {
      MapiBeforeSendToNode,
      MapiAfterSendToNode,
      MapiUnconfirmedAncestors,
      MempoolCheckerUpdateTxs,
      MempoolCheckerUpdateMissingInputs,
    }

    public enum SimulateSendTxsResponse
    {
      NodeFailsWhenSendRawTxs,
      NodeReturnsNonStandard,
      NodeReturnsInsufficientFee,
      NodeReturnsMempoolFull,
      NodeReturnsMempoolFullNonFinal,
      NodeReturnsEvicted,
      NodeFailsAfterSendRawTxs
      // MapiFailsAfterSendRawTxs - is triggered with dbFaultComponent
    }
  }
}