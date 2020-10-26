using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace MerchantAPI.APIGateway.Test.Stress
{
  public class SendConfig : IValidatableObject
  {
    // File containing transactions to send
    [Required]
    public string Filename { get; set; }

    // Only submit up to specified number of transactions from transaction file
    public long? Limit { get; set; }
    // Specifies a zero based index of column that contains hex encoded transaction in 
    public int TxIndex { get; set; } = 1;

    // Authorization header used when submitting transactions
    public string Authorization { get; set; }

    // "URL used for submitting transactions. Example: http://localhost:5000/"
    [Required]
    public string MapiUrl { get; set; }

    // Number of transactions submitted in one call"
    public int BatchSize { get; set; } = 100;

    // Number of concurrent threads that will be used to submitting transactions.
    // When using multiple threads, make sure that transactions in the file are not dependent on each other"
    public int Threads { get; set; } = 1;

    public CallbackConfig CallBack { get; set; }

    public BitcoindConfig BitcoindConfig { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContextRoot)
    {
      if (CallBack != null)
      {
        var validationContext = new ValidationContext(CallBack, serviceProvider: null, items: null);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(CallBack, validationContext, validationResults, true);
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

  public class CallbackConfig : IValidatableObject
  {
    // Url that will process double spend and merkle proof notifications. When present, transactions will be submitted
    // with MerkleProof and DsCheck set to true. Example: http://localhost:2000/callbacks
    [Required]
    public string Url { get; set; }

    // When specified, a random number between 1 and  AddRandomNumberToHost will be appended to host name specified in Url when submitting each batch of transactions.
    // This is useful for testing callbacks toward different hosts
    public int? AddRandomNumberToHost { get; set; }

    // Full authorization header that mAPI should use when performing callbacks.
    public string CallbackToken { get; set; }

    // Encryption parameters used when performing callbacks.
    public string CallBackEncryption { get; set; }

    // Start a listener that will listen to callbacks on port specified by Url
    // When specified, error will be reported if not all callbacks are received
    public bool StartListener { get; set; }

    /// Maximum number of milliseconds that we are willing to wait for next callbacks 
    public int IdelTimeoutMS { get; set; } = 30_000;

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
    public int CallBackFailurePercent { get; set; }
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
  }


}
