// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain.Models;
using System.Linq;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class ServiceLevelArrayViewModelGet
  {
    [JsonPropertyName("serviceLevels")]
    public ServiceLevelViewModelGet[] ServiceLevels { get; set; }

    public ServiceLevelArrayViewModelGet() { }
    public ServiceLevelArrayViewModelGet(ServiceLevel[] serviceLevels)
    {
      var serviceLevelFeeAmounts = (serviceLevels != null) ? (from serviceLevel in serviceLevels
                                                                             select new ServiceLevelViewModelGet(serviceLevel)) : null;
      ServiceLevels = serviceLevelFeeAmounts.ToArray();
    }

  }
}
