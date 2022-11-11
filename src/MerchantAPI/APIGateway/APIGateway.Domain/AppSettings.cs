// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using MerchantAPI.Common.Authentication;
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

    [Range(1, int.MaxValue)]
    public int? RequestTimeoutSec { get; set; } = 100;
  }

  public class Notification : IValidatableObject
  {
    [Range(1, int.MaxValue)]
    public int? NotificationIntervalSec { get; set; } = 60;

    [Range(2, 100)]
    public int InstantNotificationsTasks { get; set; }

    [Required]
    public int? InstantNotificationsQueueSize { get; set; }

    [Required]
    public int? MaxNotificationsInBatch { get; set; }

    [Required]
    public int? SlowHostThresholdInMs { get; set; }

    [Range(1, 100)]
    public int InstantNotificationsSlowTaskPercentage { get; set; }

    [Required]
    public int? NoOfSavedExecutionTimes { get; set; }

    [Required]
    public int? NotificationsRetryCount { get; set; }

    [Range(100, int.MaxValue)]
    public int? SlowHostResponseTimeoutMS { get; set; } = 3000;

    [Range(100, int.MaxValue)]
    public int? FastHostResponseTimeoutMS { get; set; } = 1000;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      if (SlowHostResponseTimeoutMS <= FastHostResponseTimeoutMS)
      {
        yield return new ValidationResult($"Value for {nameof(SlowHostResponseTimeoutMS)} must be greater than {nameof(FastHostResponseTimeoutMS)}.");
      }
    }
  }

  public class DbConnectionSettings
  {
    [Range(1, int.MaxValue)]
    public int? StartupTestConnectionMaxRetries { get; set; } = 10;
    [Range(1, int.MaxValue)]
    public int? StartupCommandTimeoutMinutes { get; set; }

    [Range(1, int.MaxValue)]
    public int? OpenConnectionTimeoutSec { get; set; } = 30;

    [Range(1, int.MaxValue)]
    public int? OpenConnectionMaxRetries { get; set; } = 3;

  }

  public class RpcClientSettings
  {
    [Range(1, int.MaxValue)]
    public int? RequestTimeoutSec { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int? MultiRequestTimeoutSec { get; set; } = 20;

    [Range(1, int.MaxValue)]
    public int? NumOfRetries { get; set; } = 3;

    [Range(1, int.MaxValue)]
    public int? WaitBetweenRetriesMs { get; set; } = 100;

    [Range(1, int.MaxValue)]
    public int? RpcCallsOnStartupRetries { get; set; } = 3;

    [Range(1, int.MaxValue)]
    public int? RpcGetBlockTimeoutMinutes { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int? RpcGetRawMempoolTimeoutMinutes { get; set; } = 2;
  }
  public class AppSettings : CommonAppSettings
  {
    [Range(1, double.MaxValue)]
    public double? QuoteExpiryMinutes { get; set; } = 10;
    public string CallbackIPAddresses { get; set; }
    public string[] CallbackIPAddressesArray
    {
      get
      {
        return string.IsNullOrEmpty(CallbackIPAddresses) ? null : CallbackIPAddresses?.Split(",");
      }
    }
    public string WifPrivateKey { get; set; }
    
    public MinerIdServer MinerIdServer { get; set; }

    public int? MaxBlockChainLengthForFork { get; set; } = 432;

    [Range(1, int.MaxValue)]
    public int? ZmqConnectionTestIntervalSec { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int? ZmqConnectionRpcResponseTimeoutSec { get; set; } = 5;

    [Range(1, int.MaxValue)]
    public int? ZmqStatsLogPeriodMin{ get; set; } = 10;

    public long? DeltaBlockHeightForDoubleSpendCheck { get; set; } = 144;

    [Range(1, int.MaxValue)]
    public int? CleanUpTxAfterDays { get; set; } = 3;

    [Range(1, int.MaxValue)]
    public int? CleanUpTxAfterMempoolExpiredDays { get; set; } = 14;

    [Range(600, int.MaxValue)]
    public int? CleanUpTxPeriodSec { get; set; } = 3600;

    public bool? MempoolCheckerDisabled { get; set; } = false;

    [Range(10, int.MaxValue)]
    public int? MempoolCheckerIntervalSec { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int? MempoolCheckerUnsuccessfulIntervalSec { get; set; } = 10;

    [Range(0, int.MaxValue)]
    public int? MempoolCheckerBlockParserQueuedMax { get; set; } = 0;

    [Range(0, int.MaxValue)]
    public int? MempoolCheckerMissingInputsRetries { get; set; } = 5;

    [Range(1, int.MaxValue)]
    public int DSHostBanTimeSec { get; set; }

    [Range(2, int.MaxValue)]
    public int DSMaxNumOfTxQueries { get; set; }

    [Range(1, int.MaxValue)]
    public int DSCachedTxRequestsCooldownPeriodSec { get; set; }

    [Range(1, int.MaxValue)]
    public int DSMaxNumOfUnknownTxQueries { get; set; }

    [Range(1, int.MaxValue)]
    public int DSUnknownTxQueryCooldownPeriodSec { get; set; }

    [Range(1, int.MaxValue)]
    public int DSScriptValidationTimeoutSec { get; set; }
    public Notification Notification { get; set; }

    public DbConnectionSettings DbConnection { get; set; }
    public RpcClientSettings RpcClient { get; set; }

    public bool? CheckFeeDisabled { get; set; } = false;

    public bool? EnableHTTP { get; set; } = false;
    
    public bool? DontParseBlocks { get; set; } = false;

    public bool? DontInsertTransactions { get; set; } = false;

    public bool? ResubmitKnownTransactions { get; set; } = false;

    public bool? EnableFaultInjection { get; set; } = false;

    public bool? EnableMissingParentsResubmission { get; set; } = false;

    private readonly string defaultAllowedTxOutFields = $"{TxOutFields.ScriptPubKeyLen},{TxOutFields.Value},{TxOutFields.IsStandard},{TxOutFields.Confirmations}";
    public string AllowedTxOutFields { get; set; }
    public string[] AllowedTxOutFieldsArray
    {
      get
      {
        return string.IsNullOrEmpty(AllowedTxOutFields) ? defaultAllowedTxOutFields.Split(",").Select(f => f.Trim()).ToArray() : AllowedTxOutFields?.Split(",").Select(f => f.Trim()).ToArray();
      }
    }
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
      if (!string.IsNullOrEmpty(options.CallbackIPAddresses))
      {
        foreach (var ipString in options.CallbackIPAddressesArray)
        {
          string error = $"Invalid configuration - {nameof(AppSettings.CallbackIPAddresses)}: url '{ ipString }' is invalid.";
          if (String.IsNullOrWhiteSpace(ipString))
          {
            return ValidateOptionsResult.Fail(error);
          }

          _ = IPEndPoint.TryParse(ipString, out var ipPort);
          if (ipPort == null)
          {
            return ValidateOptionsResult.Fail(error);
          }
        }
      }
      if(!string.IsNullOrEmpty(options.AllowedTxOutFields))
      {
        foreach(var txOutField in options.AllowedTxOutFieldsArray)
        {
          if(!TxOutFields.ValidFields.Any(x => x == txOutField))
          {
            return ValidateOptionsResult.Fail($"Invalid configuration - {nameof(AppSettings.AllowedTxOutFields)}: field '{txOutField}' is invalid (valid options: {String.Join(",", TxOutFields.ValidFields)}).");
          }
        }
      }
      if (options.MempoolCheckerUnsuccessfulIntervalSec >= options.MempoolCheckerIntervalSec)
      {
        return ValidateOptionsResult.Fail(
  $"Invalid configuration - {nameof(AppSettings.MempoolCheckerUnsuccessfulIntervalSec)} must be smaller than {nameof(AppSettings.MempoolCheckerIntervalSec)}.");
      }
      if (options.Notification == null)
      {
        return ValidateOptionsResult.Fail(
          $"Invalid configuration - {nameof(AppSettings.Notification)} settings must be specified.");
      }
      else
      {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(options.Notification, serviceProvider: null, items: null);
        if (!Validator.TryValidateObject(options.Notification, validationContext, validationResults, true))
        {
          return ValidateOptionsResult.Fail(string.Join(",", validationResults.Select(x => x.ErrorMessage).ToArray()));
        }
      }
      if (options.RpcClient == null)
      {
        // all default values
        options.RpcClient = new RpcClientSettings();
      }
      else
      {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(options.RpcClient, serviceProvider: null, items: null);
        if (!Validator.TryValidateObject(options.RpcClient, validationContext, validationResults, true))
        {
          return ValidateOptionsResult.Fail(string.Join(",", validationResults.Select(x => x.ErrorMessage).ToArray()));
        }
      }
      if (options.DbConnection == null)
      {
        // all default values
        options.DbConnection = new DbConnectionSettings();
      }
      else
      {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(options.DbConnection, serviceProvider: null, items: null);
        if (!Validator.TryValidateObject(options.DbConnection, validationContext, validationResults, true))
        {
          return ValidateOptionsResult.Fail(string.Join(",", validationResults.Select(x => x.ErrorMessage).ToArray()));
        }
      }

      return ValidateOptionsResult.Success;
    }
  }
}
