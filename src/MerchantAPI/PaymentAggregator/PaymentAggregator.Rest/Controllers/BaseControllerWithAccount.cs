// Copyright (c) 2020 Bitcoin Association

using System;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using MerchantAPI.Common.Authentication;
using MerchantAPI.PaymentAggregator.Domain.Models;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MerchantAPI.PaymentAggregator.Rest.Controllers
{
  public class BaseControllerWithAccount : ControllerBase
  {
    protected readonly ILogger<BaseControllerWithAccount> logger;
    protected readonly ISubscriptionRepository subscriptionRepository;
    protected readonly IAccountRepository accountRepository;

    public BaseControllerWithAccount(ILogger<BaseControllerWithAccount> logger, ISubscriptionRepository subscriptionRepository, IAccountRepository accountRepository)
    {
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.subscriptionRepository = subscriptionRepository ?? throw new ArgumentNullException(nameof(subscriptionRepository));
      this.accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
    }

    protected ActionResult ValidateToken(ClaimsPrincipal user, IHeaderDictionary headers, out Account account)
    {
      account = null;
      if (!IdentityProviderStore.GetUserAndIssuer(user, headers, out var identity))
      {
        return Unauthorized("Incorrectly formatted token");
      }
      if (identity == null)
      {
        return Unauthorized("Token must be present");
      }
      account = accountRepository.GetAccountByIdentityAsync(identity.Identity, identity.IdentityProvider).Result;
      if (account == null || account.IdentityProvider != identity.IdentityProvider)
      {
        var pd = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.Unauthorized, "Invalid token.");
        return Unauthorized(pd);
      }
      return null;
    }

    protected async Task<(Account account, int? subscriptionId, ActionResult actionResult)> ValidateAccountAndSubscriptionAsync(ClaimsPrincipal user, IHeaderDictionary headers, string serviceType)
    {
      var result = ValidateToken(User, Request.Headers, out var account);
      if (result != null)
      {
        return (account, null,  result);
      }

      var subscriptions = await subscriptionRepository.GetSubscriptionsAsync(account.AccountId, true);
      var subscription = subscriptions.FirstOrDefault(x => x.ServiceType == serviceType);
      if (subscription != null)
      {
        logger.LogInformation($"Found subscription with serviceType { serviceType } for user { account.Identity } with identity provider { account.IdentityProvider }.");
        return (account, subscription.SubscriptionId, result);
      }
      var pd = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.Unauthorized, $"Subscription for {serviceType} is not active.");
      return (account, null, Unauthorized(pd));
    }
  }
}
