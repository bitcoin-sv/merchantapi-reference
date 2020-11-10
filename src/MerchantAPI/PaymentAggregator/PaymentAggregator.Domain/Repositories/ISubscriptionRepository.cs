// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain.Models;
using System;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Domain.Repositories
{
  public interface ISubscriptionRepository
  {
    Task<Subscription> AddSubscriptionAsync(int accountId, string serviceType, DateTime validFrom);
    Task<bool> DeleteSubscriptionAsync(int accountId, int subscriptionId);
    Task<Subscription[]> GetSubscriptionsAsync(int accountId, bool onlyActive);
    Task<Subscription> GetSubscriptionAsync(int accountId, int subscriptionId);
  }
}
