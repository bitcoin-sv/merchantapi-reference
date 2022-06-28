// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace MerchantAPI.APIGateway.Test.Stress
{
  public class SendConfig : IValidatableObject
  {
    // See the readme file for more information.
    [Required]
    public string Filename { get; set; }

    public int TxIndex { get; set; } = 1;

    [Range(0, int.MaxValue)]
    public int Skip { get; set; } = 0;

    public long? Limit { get; set; }

    public int BatchSize { get; set; } = 100;

    public int Threads { get; set; } = 1;

    public int StartGenerateBlocksAtTx { get; set; } = -1;

    public int GenerateBlockPeriodMs { get; set; } = 500;

    public int GetRawMempoolEveryNTxs { get; set; } = 0;

    public string CsvComment { get; set; }

    [Required]
    public MapiConfig MapiConfig { get; set; }
    public BitcoindConfig BitcoindConfig { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContextRoot)
    {
      if (Limit.HasValue && Skip > Limit)
      {
        yield return new ValidationResult($"{ nameof(Skip) } must be smaller than { nameof(Limit) }.");
      }
      if (Limit.HasValue && BatchSize > (Limit - Skip))
      {
        yield return new ValidationResult(@$"{ nameof(BatchSize) }({ BatchSize }) must be smaller than
{ nameof(Limit) } - { nameof(Skip) }({ Limit - Skip }).");
      }
      if (Limit.HasValue && StartGenerateBlocksAtTx > -1)
      {
        if (StartGenerateBlocksAtTx < Skip)
        {
          yield return new ValidationResult(@$"GenerateBlocks will not run - { nameof(StartGenerateBlocksAtTx)}({ StartGenerateBlocksAtTx }) must 
be bigger than { nameof(Skip) }({ Skip }).");
        }
        if (StartGenerateBlocksAtTx > Limit)
        {
          yield return new ValidationResult(@$"GenerateBlocks will not run - { nameof(StartGenerateBlocksAtTx)}({ StartGenerateBlocksAtTx }) must 
be smaller than { nameof(Limit) }({ Limit }).");
        }
      }
      if (GenerateBlockPeriodMs < 0)
      {
        yield return new ValidationResult($"GenerateBlockPeriodMs must be positive.");
      }
      if (MapiConfig.MapiUrl != null)
      {
        if (!MapiConfig.MapiUrl.EndsWith("/"))
        {
          yield return new ValidationResult($"MapiUrl must end with '/'");
        }
      }
      if (MapiConfig.Callback != null)
      {
        var validationContext = new ValidationContext(MapiConfig.Callback, serviceProvider: null, items: null);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(MapiConfig.Callback, validationContext, validationResults, true);
        foreach (var x in validationResults)
        {
          yield return x;
        }
      }

      if (BitcoindConfig != null)
      {
        var validationContext = new ValidationContext(BitcoindConfig, serviceProvider: null, items: null);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(BitcoindConfig, validationContext, validationResults, true);
        foreach (var x in validationResults)
        {
          yield return x;
        }
      }
    }
  }

  public class MapiConfig
  {
    public string Authorization { get; set; }

    [Required]
    public string MapiUrl { get; set; }

    public bool RearrangeNodes { get; set; }

    public string AddFeeQuotesFromJsonFile { get; set; }

    public string NodeHost { get; set; }

    public string NodeZMQNotificationsEndpoint { get; set; }

    public CallbackConfig Callback { get; set; }
  }

  public class CallbackConfig : IValidatableObject
  {
    [Required]
    public string Url { get; set; }

    public int? AddRandomNumberToPort { get; set; }

    public string CallbackToken { get; set; }

    public string CallbackEncryption { get; set; }

    public bool StartListener { get; set; }

    public int IdleTimeoutMS { get; set; } = 30_000;

    public CallbackHostConfig[] Hosts { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContextRoot)
    {
      if (Hosts != null)
      {
        foreach (var host in Hosts)
        {
          var validationContext = new ValidationContext(host, serviceProvider: null, items: null);
          var validationResults = new List<ValidationResult>();
          Validator.TryValidateObject(host, validationContext, validationResults, true);
          foreach (var x in validationResults)
          {
            yield return x;
          }
        }

        var duplicateHosts = Hosts.GroupBy(x => x.HostName, StringComparer.InvariantCultureIgnoreCase)
          .Where(x => x.Count() > 1).ToArray();
        foreach (var duplicate in duplicateHosts)
        {
          yield return new ValidationResult($"Host {duplicate.Key} is listed in configuration multiple times");
        }
      }
    }

  }

  public class CallbackHostConfig
  {
    // Name of host to which configuration applies to. use empty string for default setting
    public string HostName { get; set; }

    public int? MinCallbackDelayMs { get; set; }
    public int? MaxCallbackDelayMs { get; set; }

    [Range(0, 100)]
    public int CallbackFailurePercent { get; set; }
  }

  public class BitcoindConfig
  {

    // Full path to bitcoind executable. Used when starting new node if --templateData is specified.
    // TODO: fix this - make it required If not specified, bitcoind executable must be in current directory. Example :/usr/bitcoin/bircoind 
    [Required]
    public string BitcoindPath { get; set; }


    // Template directory containing snapshot if data directory that will be used as initial state of new node that is started up. 
    // If specified --authAdmin must also be specified.
    [Required]
    public string TemplateData { get; set; }

    // Full authorization header used for accessing mApi admin endpoint. The admin endpoint is used to automatically register
    // bitcoind with mAPI 
    [Required]
    public string MapiAdminAuthorization { get; set; }

    // override default "127.0.0.1" zmqEndpoint ip
    public string ZmqEndpointIp { get; set; }
  }


}
