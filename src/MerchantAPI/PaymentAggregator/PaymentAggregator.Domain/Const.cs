// Copyright (c) 2020 Bitcoin Association

using System.Linq;

namespace MerchantAPI.PaymentAggregator.Consts
{
  public class Const 
  {
    public const string PAYMENT_AGGREGATOR_API_VERSION = "1.0.0";

    public const string EXCEPTION_DETAILS_EXECUTION_TIME = "ExecutionTime";
    public const string EXCEPTION_DETAILS_SUBSCRIPTION_ID = "SubscriptionId";

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

  public class ServiceType
  {
    public const string allFeeQuotes = "allfeequotes";
    public const string submitTx = "submittx";
    public const string queryTx = "querytx";

    public static readonly string[] validServiceTypes = new string[] { allFeeQuotes, submitTx, queryTx };

    public static bool IsValid(string serviceType) => validServiceTypes.Any(x => x == serviceType);
  }

}
