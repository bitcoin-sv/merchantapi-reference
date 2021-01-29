// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using MerchantAPI.PaymentAggregator.Rest.Swagger;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using MerchantAPI.PaymentAggregator.Rest.ViewModels;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MerchantAPI.PaymentAggregator.Rest.Controllers
{
  [Route("api/v1/account/[controller]")]
  [ApiController]
  [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
  [ApiExplorerSettings(GroupName = SwaggerGroup.API)]
  public class SubscriptionController : BaseControllerWithAccount
  {

    public SubscriptionController(ILogger<SubscriptionController> logger, ISubscriptionRepository subscriptionRepository, IAccountRepository accountRepository)
            : base(logger, subscriptionRepository, accountRepository)
    {
    }

    [HttpPost]
    public async Task<ActionResult> Post(SubscriptionViewModelCreate data)
    {
      var result = ValidateToken(User, Request.Headers, out var account);
      if (result != null)
      {
        return result;
      }
      var domainModel = data.ToDomainModel();
      var errors = domainModel.Validate(new ValidationContext(domainModel));
      if (errors.Count() > 0)
      {
        var pd = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        pd.Title = string.Join(",", errors.Select(x => x.ErrorMessage));
        return BadRequest(pd);
      }

      var created = await subscriptionRepository.AddSubscriptionAsync(account.AccountId, domainModel.ServiceType, domainModel.ValidFrom);

      if (created == null)
      {
        return Conflict();
      }

      return CreatedAtAction(nameof(Get),
            new { id = created.SubscriptionId },
            new SubscriptionViewModelGet(created));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SubscriptionViewModelGet>> Get(int id)
    {
      var result = ValidateToken(User, Request.Headers, out var account);
      if (result != null)
      {
        return result;
      }

      var subscription = await subscriptionRepository.GetSubscriptionAsync(account.AccountId, id);
      if (subscription == null)
      {
        return NotFound();
      }
      return Ok(new SubscriptionViewModelGet(subscription));
    }

    [HttpGet()]
    public async Task<ActionResult<IEnumerable<SubscriptionViewModelGet>>> Get(
      [FromQuery]
      bool onlyActive = true)
    {
      var result = ValidateToken(User, Request.Headers, out var account);
      if (result != null)
      {
        return result;
      }

      var subscriptions = await subscriptionRepository.GetSubscriptionsAsync(account.AccountId, onlyActive);
      return Ok(subscriptions.Select(x => new SubscriptionViewModelGet(x)));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
      var result = ValidateToken(User, Request.Headers, out var account);
      if (result != null)
      {
        return result;
      }

      await subscriptionRepository.DeleteSubscriptionAsync(account.AccountId, id);

      return NoContent();
    }

  }
}
