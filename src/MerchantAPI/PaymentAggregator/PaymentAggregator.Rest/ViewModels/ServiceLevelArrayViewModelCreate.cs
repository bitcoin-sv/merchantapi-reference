// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain.Models;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class ServiceLevelArrayViewModelCreate
  {
    [Required]
    [JsonPropertyName("serviceLevels")]
    public ServiceLevelViewModelCreate[] ServiceLevels { get; set; }

    public ServiceLevelArrayViewModelCreate()
    {

    }

    public ServiceLevelArrayViewModelCreate(ServiceLevel[] serviceLevels)
    {
      var serviceLevelFeeAmounts = (ServiceLevels != null) ? (from serviceLevel in serviceLevels
                                                                             select new ServiceLevelViewModelCreate(serviceLevel)).ToArray() : null;
      ServiceLevels = serviceLevelFeeAmounts;
    }

    public ServiceLevelArray ToDomainObject()
    {
      var serviceLevels = (ServiceLevels != null) ? 
                          (from serviceLevelViewModels in ServiceLevels 
                           select serviceLevelViewModels?.ToDomainObject()).ToArray() : null;                                                                          
      return new ServiceLevelArray(serviceLevels);
    }
  }
}
