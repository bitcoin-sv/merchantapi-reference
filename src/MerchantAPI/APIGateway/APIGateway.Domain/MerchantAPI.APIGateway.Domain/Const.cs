// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common;

namespace MerchantAPI.APIGateway.Domain
{
  public class Const: CommonConst
  {
    public const string MERCHANT_API_VERSION = "1.2.0"; 
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

}
