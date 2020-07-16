// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.APIGateway.Rest.Swagger;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.Common;
using MerchantAPI.Common.Clock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace MerchantAPI.APIGateway.Rest.Controllers
{

  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  [ApiExplorerSettings(GroupName = SwaggerGroup.Admin)]
  public class FeeQuoteController: ControllerBase
  {

    private readonly ILogger<FeeQuoteController> logger;
    private readonly IFeeQuoteRepository feeQuoteRepository;
    private readonly IClock clock;

    public FeeQuoteController(
      ILogger<FeeQuoteController> logger,
      IFeeQuoteRepository feeQuoteRepository,
      IClock clock
      )
    {
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.feeQuoteRepository = feeQuoteRepository ?? throw new ArgumentNullException(nameof(feeQuoteRepository));
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    // GET /api/v1/feeQuote
    /// <summary>
    /// Get selected fee quote.
    /// </summary>
    /// <param name="id">Id of the selected fee quote.</param>
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
    /// Get general list of fee quotes or fee quotes for given identity.
    /// </summary>
    /// <param name="identity">Identity identifier.</param>
    /// <param name="identityProvider">Identity provider.</param>
    /// <param name="anonymous">Return only fee quotes for anonymous user.</param>
    /// <param name="current">Return only valid fee quotes.</param>   
    /// <param name="valid">Return valid fee quotes in interval with QuoteExpiryMinutes.</param>
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
        userAndIssuer = new UserAndIssuer() { Identity = HttpUtility.UrlDecode(identity), IdentityProvider = HttpUtility.UrlDecode(identityProvider) };
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
    /// Create new fee quote.
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<FeeQuoteConfigViewModelGet>> Post([FromBody] FeeQuoteViewModelCreate data)
    {
      logger.LogInformation($"Create new FeeQuote form data: {data} .");

      data.CreatedAt = clock.UtcNow();
      var domainModel = data.ToDomainObject();
      
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
