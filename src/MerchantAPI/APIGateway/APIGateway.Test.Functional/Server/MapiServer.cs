// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

namespace MerchantAPI.APIGateway.Test.Functional.Server
{
  public class MapiServer : TestServerBase
  {
    public const string ApiNodeUrl = "/api/v1/node";    
    public const string ApiMapiFeeQuoteConfigUrl = "/api/v1/feequote";
    public const string ApiMapiQueryFeeQuote = "mapi/feequote/";
    public const string ApiMapiSubmitTransaction= "mapi/tx/";
    public const string ApiMapiQueryTransactionStatus = "mapi/tx/";
    public const string ApiMapiSubmitTransactions = "mapi/txs";
    public const string ApiZmqStatusUrl = "/api/v1/status/zmq";
    public const string ApiDSQuery = "dsnt/1/query";
    public const string ApiDSSubmit = "dsnt/1/submit";
  }
}
