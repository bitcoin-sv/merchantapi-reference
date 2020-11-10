// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain.Models;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class SLAViewModelGet
  {
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("slacategory")]
    public int SlaCategory { get; set; }

    [JsonPropertyName("sladescription")]
    public string SlaDescription { get; set; }

    public SLAViewModelGet() { }

    public SLAViewModelGet(ServiceLevel serviceLevel, string type)
    {
      Type = type;
      SlaCategory = serviceLevel.Level;
      SlaDescription = serviceLevel.Description;
    }
  }
}
