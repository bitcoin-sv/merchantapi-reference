// Copyright (c) 2020 Bitcoin Association

namespace MerchantAPI.APIGateway.Domain
{
  public static class Const
  {
    public const string MERCHANT_API_VERSION = "1.2.3"; 
    public static readonly string[] RequiredZmqNotifications = { "pubhashblock", "pubremovedfrommempool", "pubinvalidtx" };
  }

  public static class CallbackReason
  {
    public const string MerkleProof = "merkleProof";
    public const string DoubleSpend = "doubleSpend";
    public const string DoubleSpendAttempt = "doubleSpendAttempt";
  }
}
