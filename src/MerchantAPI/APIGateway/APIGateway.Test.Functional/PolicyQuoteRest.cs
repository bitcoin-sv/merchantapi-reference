﻿using MerchantAPI.APIGateway.Test.Functional.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestCategory("TestCategoryNo1")]
  [TestClass]
  public class PolicyQuoteRest : FeeQuoteRest
  {
    // test synonymous endpoint
    public override string GetBaseUrl() => MapiServer.ApiPolicyQuoteConfigUrl;
  }
}
