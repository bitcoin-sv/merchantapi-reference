// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.Authentication;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace MerchantAPI.PaymentAggregator.Domain
{
  public class AppSettings: CommonAppSettings
  {
    [Range(1, int.MaxValue)]
    public int CleanUpServiceRequestAfterDays { get; set; } = 30;

    [Range(600, int.MaxValue)]
    public int CleanUpServiceRequestPeriodSec { get; set; } = 3600;
  }

  public class AppSettingValidator : IValidateOptions<AppSettings>
  {
    public ValidateOptionsResult Validate(string name, AppSettings options)
    {
      // Explicitly trigger validation of nested objects

      return ValidateOptionsResult.Success;
    }
  }
}
