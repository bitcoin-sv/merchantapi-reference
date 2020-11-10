// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common;
using System.Linq;

namespace MerchantAPI.PaymentAggregator.Consts
{
  public class Const : CommonConst
  {
    public const string PAYMENT_AGGREGATOR_API_VERSION = "1.0.0";

    public const string EXCEPTION_DETAILS_EXECUTION_TIME = "ExecutionTime";
    public const string EXCEPTION_DETAILS_SUBSCRIPTION_ID = "SubscriptionId";
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
