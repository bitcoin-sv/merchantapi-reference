// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.APIGateway.Rest.Swagger;
using MerchantAPI.Common.Clock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MerchantAPI.APIGateway.Rest.Controllers
{

  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  [ApiExplorerSettings(GroupName = SwaggerGroup.Admin)]
  [ServiceFilter(typeof(HttpsRequiredAttribute))]
  public class PolicyQuoteController : FeeQuoteController
  {
    public PolicyQuoteController(
      ILogger<FeeQuoteController> logger,
      IFeeQuoteRepository feeQuoteRepository,
      ITxRepository txRepository,
      IClock clock,
      IOptions<AppSettings> options
      ) : base(logger, feeQuoteRepository, txRepository, clock, options)
    {

    }

  }
}
