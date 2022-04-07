using MerchantAPI.APIGateway.Test.Functional.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestCategory("TestCategoryNo1")]
  [TestClass]
  public class PolicyQuoteRest : FeeQuoteRest
  {
    // test synonymous endpoint
    public override string GetBaseUrl() => MapiServer.ApiPolicyQuoteConfigUrl;

    protected override string PrepareTxsUrl(IList<(string, string)> queryParams)
    {
      return PrepareQueryParams(MapiServer.ApiPolicyQuoteTxsUrl, queryParams);
    }
  }
}
