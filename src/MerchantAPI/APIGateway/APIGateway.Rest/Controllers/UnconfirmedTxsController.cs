// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.APIGateway.Rest.Swagger;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.Common.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  [ApiExplorerSettings(GroupName = SwaggerGroup.Admin)]
  [ServiceFilter(typeof(HttpsRequiredAttribute))]
  public class UnconfirmedTxsController : ControllerBase
  {
    private readonly ILogger<UnconfirmedTxsController> logger;
    private readonly IFeeQuoteRepository feeQuoteRepository;
    private readonly ITxRepository txRepository;

    public UnconfirmedTxsController(
      ILogger<UnconfirmedTxsController> logger,
      IFeeQuoteRepository feeQuoteRepository,
      ITxRepository txRepository
      )
    {
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.feeQuoteRepository = feeQuoteRepository ?? throw new ArgumentNullException(nameof(feeQuoteRepository));
      this.txRepository = txRepository ?? throw new ArgumentNullException(nameof(txRepository));
    }

    private (FeeQuote[] feeQuotes, string error) GetFeeQuotesForDeleteTxs(string identity, string identityProvider, long? policyQuoteId)
    {
      UserAndIssuer userAndIssuer = null;
      if (identity != null || identityProvider != null)
      {
        userAndIssuer = new UserAndIssuer() { Identity = identity, IdentityProvider = identityProvider };
      }

      if (policyQuoteId.HasValue)
      {
        var feequote = feeQuoteRepository.GetFeeQuoteById(policyQuoteId.Value);
        if (feequote == null)
        {
          return (null, "Invalid policyQuoteId.");
        }
        if (feequote.Identity == null && feequote.IdentityProvider == null)
        {
          return (null, "PolicyQuoteId refers to anonymous user.");
        }
        if (identity != null && feequote.Identity != identity)
        {
          return (null, $"Identity of policyQuote with policyQuoteId {policyQuoteId} is different from {identity}.");
        }
        if (identityProvider != null && feequote.IdentityProvider != identityProvider)
        {
          return (null, $"IdentityProvider of policyQuote with policyQuoteId {policyQuoteId} is different from {identityProvider}.");
        }
        return (new FeeQuote[] { feequote }, null);
      }
      else if (userAndIssuer != null)
      {
        var feeQuotes = feeQuoteRepository.GetFeeQuotesByIdentity(userAndIssuer).ToArray();
        if (feeQuotes.Length == 0)
        {
          return (null, "No policyQuotes available.");
        }
        return (feeQuotes, null);
      }
      else
      {
        return (null, "Missing query parameters.");
      }
    }

    /// <summary>
    /// Get a list of transactions that were sent to node but are not marked as accepted with defined policy quote or given identity.
    /// Parameters must refer to authenticated user.
    /// </summary>
    /// <param name="identity">Identity identifier.</param>
    /// <param name="identityProvider">Identity provider.</param>
    /// <param name="policyQuoteId">Policy quote id.</param>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<DeleteTxsViewModelGet>> GetDeleteTxs(
      [FromQuery]
      string identity,
      [FromQuery]
      string identityProvider,
      [FromQuery]
      long? policyQuoteId)
    {
      var (feeQuotes, error) = GetFeeQuotesForDeleteTxs(identity, identityProvider, policyQuoteId);
      if (error != null)
      {
        return BadRequest(error);
      }
      var txs = await txRepository.GetTxsWithFeeQuotesAsync(feeQuotes);

      return Ok(new DeleteTxsViewModelGet(txs));
    }

    /// <summary>
    /// Delete transactions that were sent to node but are not marked as accepted with defined policy quote or given identity.
    /// Parameters must refer to authenticated user.
    /// </summary>
    /// <param name="identity">Identity identifier.</param>
    /// <param name="identityProvider">Identity provider.</param>
    /// <param name="policyQuoteId">Policy quote id.</param>
    /// <returns></returns>
    [HttpDelete]
    public async Task<ActionResult> DeleteTxs(
      [FromQuery]
      string identity,
      [FromQuery]
      string identityProvider,
      [FromQuery]
      long? policyQuoteId)
    {
      var (feeQuotes, error) = GetFeeQuotesForDeleteTxs(identity, identityProvider, policyQuoteId);
      if (error != null)
      {
        return BadRequest(error);
      }

      var count = await txRepository.DeleteTxsWithFeeQuotesAsync(feeQuotes);
      if (count > 0)
      {
        logger.LogInformation($"DeleteTxs: removed {count} transactions that referenced policyQuoteIds: {string.Join(";", feeQuotes.Select(x => x.Id))}");
      }

      return NoContent();
    }
  }
}
