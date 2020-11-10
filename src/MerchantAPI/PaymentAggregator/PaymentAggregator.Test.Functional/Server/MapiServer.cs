// Copyright (c) 2020 Bitcoin Association

namespace MerchantAPI.PaymentAggregator.Test.Functional.Server
{
  public class MapiServer : TestServerBase
  {
    public const string ApiGatewayUrl = "/api/v1/gateway";
    public const string ApiAccountUrl = "/api/v1/account";
    public const string ApiServiceLevelUrl = "/api/v1/serviceLevel";
    public const string ApiSubscriptionUrl = "/api/v1/account/subscription";
    public const string ApiAggregatorAllFeeQuotesUrl = "api/v1/allfeequotes";
    public const string ApiAggregatorSubmitTransaction = "api/v1/tx";
    public const string ApiAggregatorSubmitTransactions = "api/v1/txs";
    public const string ApiAggregatorQueryTransactionStatusUrl = "api/v1/tx/";
  }
}
