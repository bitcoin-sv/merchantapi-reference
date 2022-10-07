// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.APIGateway.Rest.Swagger;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.Common.Authentication;
using MerchantAPI.Common.Clock;
using MerchantAPI.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Rest.Controllers
{

  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  [ApiExplorerSettings(GroupName = SwaggerGroup.Admin)]
  [ServiceFilter(typeof(HttpsRequiredAttribute))]
  public class FeeQuoteController: ControllerBase
  {

    private readonly ILogger<FeeQuoteController> logger;
    private readonly IFeeQuoteRepository feeQuoteRepository;
    private readonly IClock clock;

    private readonly string[] callbackIPAddressesArray;

    public FeeQuoteController(
      ILogger<FeeQuoteController> logger,
      IFeeQuoteRepository feeQuoteRepository,
      IClock clock,
      IOptions<AppSettings> options
      )
    {
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.feeQuoteRepository = feeQuoteRepository ?? throw new ArgumentNullException(nameof(feeQuoteRepository));
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
      callbackIPAddressesArray = options.Value.CallbackIPAddressesArray;
    }

    // GET /api/v1/feeQuote
    /// <summary>
    /// Get selected policy quote.
    /// </summary>
    /// <param name="id">Id of the selected policy quote.</param>
    /// <returns></returns>
    [HttpGet("{id}")]
    public ActionResult<FeeQuoteConfigViewModelGet> GetFeeQuoteById(long id)
    {
      logger.LogInformation($"Get FeeQuote {id}");

      FeeQuote feeQuote = feeQuoteRepository.GetFeeQuoteById(id);

      if (feeQuote == null)
      {
        return NotFound();
      }

      var feeQuoteViewModelGet = new FeeQuoteConfigViewModelGet(feeQuote);

      return Ok(feeQuoteViewModelGet);
    }

    /// <summary>
    /// Get general list of policy quotes or policy quotes for given identity.
    /// </summary>
    /// <param name="identity">Identity identifier.</param>
    /// <param name="identityProvider">Identity provider.</param>
    /// <param name="anonymous">Return only policy quotes for anonymous user.</param>
    /// <param name="current">Return only valid policy quotes.</param>   
    /// <param name="valid">Return valid policy quotes in interval with QuoteExpiryMinutes.</param>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<IEnumerable<FeeQuoteConfigViewModelGet>> Get(
      [FromQuery]
      string identity,
      [FromQuery]
      string identityProvider,
      [FromQuery]
      bool anonymous,
      [FromQuery]
      bool current,
      [FromQuery]
      bool valid)
    {
      UserAndIssuer userAndIssuer = null;
      if (identity != null || identityProvider != null)
      {
        userAndIssuer = new UserAndIssuer() { Identity = identity, IdentityProvider = identityProvider };
      }

      IEnumerable<FeeQuote> result = new List<FeeQuote>();
      if (valid)
      {
        logger.LogInformation($"GetValidFeeQuotes for user { ((userAndIssuer == null) ? "/" : userAndIssuer.ToString())} ...");
        if (userAndIssuer != null)
        {
          result = feeQuoteRepository.GetValidFeeQuotesByIdentity(userAndIssuer);
        }
        if (anonymous) 
        {
          result = result.Concat(feeQuoteRepository.GetValidFeeQuotesByIdentity(null));
        }
        if (!anonymous && userAndIssuer == null)
        {
          result = feeQuoteRepository.GetValidFeeQuotes();
        }
      }
      else if (current)
      {
        logger.LogInformation($"GetCurrentFeeQuotes for user { ((userAndIssuer == null) ? "/" : userAndIssuer.ToString())} ...");
        if (userAndIssuer != null)
        {
          var feeQuote = feeQuoteRepository.GetCurrentFeeQuoteByIdentity(userAndIssuer);
          if (feeQuote != null)
          {
            result = result.Append(feeQuote);
          }
        }
        if (anonymous)
        {
          var feeQuote = feeQuoteRepository.GetCurrentFeeQuoteByIdentity(null);
          if (feeQuote != null)
          {
            result = result.Append(feeQuote);
          }
        }
        if (!anonymous && userAndIssuer == null)
        {
          result = feeQuoteRepository.GetCurrentFeeQuotes();
        }
      }
      else
      {
        logger.LogInformation($"GetFeeQuotes for user { ((userAndIssuer == null) ? "/" : userAndIssuer.ToString())} ...");
        if (userAndIssuer != null)
        {
          result = feeQuoteRepository.GetFeeQuotesByIdentity(userAndIssuer);
        }
        if (anonymous)
        {
          result = result.Concat(feeQuoteRepository.GetFeeQuotesByIdentity(null));
        }
        if (!anonymous && userAndIssuer == null)
        {
          result = feeQuoteRepository.GetFeeQuotes();
        }
      }
      return Ok(result.Select(x => new FeeQuoteConfigViewModelGet(x)));
    }

    /// <summary>
    /// Create new policy quote.
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<FeeQuoteConfigViewModelGet>> Post([FromBody] FeeQuoteViewModelCreate data)
    {
      logger.LogInformation($"Create new FeeQuote form data: {data} .");

      data.CreatedAt = clock.UtcNow();
      var domainModel = data.ToDomainObject(clock.UtcNow());
      
      var br = this.ReturnBadRequestIfInvalid(domainModel);
      if (br != null)
      {
        return br;
      }
      var newFeeQuote = await feeQuoteRepository.InsertFeeQuoteAsync(domainModel);
      if (newFeeQuote == null)
      {
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        problemDetail.Status = (int)HttpStatusCode.BadRequest;
        problemDetail.Title = $"FeeQuote not created. Check errors.";
        return BadRequest(problemDetail);
      }

      var returnResult = new FeeQuoteConfigViewModelGet(newFeeQuote);

      logger.LogInformation($"FeeQuote(id) {returnResult.Id} was generated.");

      return CreatedAtAction(nameof(GetFeeQuoteById), new { id = returnResult.Id }, returnResult);
    }
  }
}
