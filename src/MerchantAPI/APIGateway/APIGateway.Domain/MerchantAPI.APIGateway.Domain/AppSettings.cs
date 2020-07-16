// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.Options;

namespace MerchantAPI.APIGateway.Domain
{
  public class MinerIdServer
  {
    [Required]
    [Url]
    public string Url { get; set; } 
    public string Authentication { get; set; }
    [Required]
    public string Alias { get; set; }
  }

  public class AppSettings
  {
    [Range(1, double.MaxValue)]
    public double QuoteExpiryMinutes { get; set; } = 10;
    public string WifPrivateKey { get; set; }
    
    public MinerIdServer MinerIdServer { get; set; }

    [Range(1,int.MaxValue)]
    public int NotificationIntervalSec { get; set; } = 60;

    [Required]
    public string RestAdminAPIKey { get; set; }

    [Required] public 
    int DeltaBlockHeightForDoubleSpendCheck { get; set; } = 144;
  }

  public class AppSettingValidator : IValidateOptions<AppSettings>
  {
    public ValidateOptionsResult Validate(string name, AppSettings options)
    {
      var hasURL = options.MinerIdServer != null && !string.IsNullOrEmpty(options.MinerIdServer.Url);
      if (!hasURL && string.IsNullOrWhiteSpace(options.WifPrivateKey)
      ||
       hasURL && !string.IsNullOrWhiteSpace(options.WifPrivateKey)
      )
      {
        return ValidateOptionsResult.Fail(
          $"Invalid configuration - either  {nameof(AppSettings.MinerIdServer)} or {nameof(AppSettings.WifPrivateKey)} must be specified.");
      }

      // Explicitly trigger validation of nested objects
      if (options.MinerIdServer != null && !string.IsNullOrEmpty(options.MinerIdServer.Url))
      {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(options.MinerIdServer, serviceProvider: null, items: null);
        if (!Validator.TryValidateObject(options.MinerIdServer, validationContext, validationResults, true))
        {
          return ValidateOptionsResult.Fail(string.Join(",", validationResults.Select(x => x.ErrorMessage).ToArray()));
        }
      }

      return ValidateOptionsResult.Success;
    }
  }
}
