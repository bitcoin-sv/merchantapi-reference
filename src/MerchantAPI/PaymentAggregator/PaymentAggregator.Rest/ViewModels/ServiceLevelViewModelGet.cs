// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain.Models;
using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class ServiceLevelViewModelGet
  {

    [JsonPropertyName("level")]
    public int Level { get; set; }
    [JsonPropertyName("description")]
    public string Description { get; set; }
    [JsonIgnore] // we only show active service levels 
    public DateTime? ValidTo { get; set; }
    [JsonPropertyName("fees")]
    public ServiceLevelFeeViewModelGet[] Fees { get; set; }

    public ServiceLevelViewModelGet() { }

    public ServiceLevelViewModelGet(ServiceLevel serviceLevel)
    {
      Level = serviceLevel.Level;
      Description = serviceLevel.Description;
      ValidTo = serviceLevel.ValidTo;
      Fees = (serviceLevel.Fees != null) ? (from fee in serviceLevel.Fees
                                                       select new ServiceLevelFeeViewModelGet(fee)).ToArray() : null;
    }

  }
}
