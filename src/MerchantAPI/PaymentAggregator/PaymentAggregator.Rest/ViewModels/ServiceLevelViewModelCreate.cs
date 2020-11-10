// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain.Models;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class ServiceLevelViewModelCreate
  {
    [JsonIgnore]
    public long ServiceLevelId { get; set; }

    [Required]
    [JsonPropertyName("level")]
    public int Level { get; set; }

    [Required]
    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("fees")]
    public ServiceLevelFeeViewModelCreate[] Fees { get; set; }

    public ServiceLevelViewModelCreate()
    {

    }

    public ServiceLevelViewModelCreate(ServiceLevel serviceLevel)
    {
      ServiceLevelId = serviceLevel.ServiceLevelId;
      Level = serviceLevel.Level;
      Description = serviceLevel.Description;
      Fees = (serviceLevel.Fees != null) ? (from fee in serviceLevel.Fees
                               select new ServiceLevelFeeViewModelCreate(fee)).ToArray() : null;
    }

    public ServiceLevel ToDomainObject()
    {
      return new ServiceLevel
      {
        ServiceLevelId = ServiceLevelId,
        Level = Level,
        Description = Description,
        Fees = (Fees != null) ? (from fee in Fees
                                 select fee?.ToDomainObject()).ToArray() : null
      };
    }
  }
}
