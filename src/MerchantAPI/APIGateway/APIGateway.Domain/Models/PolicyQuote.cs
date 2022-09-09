// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Actions;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class PolicyQuote : IValidatableObject
  {
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ValidFrom { get; set; }
    public string Identity { get; set; }
    public string IdentityProvider { get; set; }
    public string Policies { get; set; }

    public Dictionary<string, object> PoliciesDict 
    { 
      get
      {
        return Policies != null ? JsonSerializer.Deserialize<Dictionary<string, object>>(Policies) : null;
      }
    }

    [JsonPropertyName("fees")]
    public Fee[] Fees { get; set; }

    class ConsolidationPolicies
    {
      public const string MinConsolidationFactor = "minconsolidationfactor";
      public const string MaxConsolidationInputScriptSize = "maxconsolidationinputscriptsize";
      public const string MinConfConsolidationInput = "minconfconsolidationinput";
      public const string AcceptNonStdConsolidationInput = "acceptnonstdconsolidationinput";
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      if (CreatedAt > ValidFrom)
      {
        yield return new ValidationResult("Check ValidFrom value - cannot be valid before created.");
      }
      if ((Identity != null && IdentityProvider == null) || (Identity == null && IdentityProvider != null))
      {
        yield return new ValidationResult("Must provide both (identity and identityProvider) or none. ");
      }
      if (Identity?.Trim() == "")
      {
        yield return new ValidationResult("Identity must contain at least one non-whitespace character.");
      }
      if (IdentityProvider?.Trim() == "")
      {
        yield return new ValidationResult("IdentityProvider must contain at least one non-whitespace character.");
      }
      if (Fees == null || Fees.Length == 0)
      {
        yield return new ValidationResult("Fees array with at least one fee is required. ");
      }
      else
      {
        HashSet<string> hs = new();
        foreach (var fee in Fees)
        {
          if (hs.Contains(fee.FeeType))
          {
            yield return new ValidationResult($"Fees array contains duplicate Fee for FeeType { fee.FeeType }");
          }
          hs.Add(fee.FeeType);

          var results = fee.Validate(validationContext);
          foreach (var result in results)
          {
            yield return result;
          }
        }
      }
    }

    private T GetPolicyValue<T>(string policyName, T defaultValue)
    {
      if (PoliciesDict?.ContainsKey(policyName) == true)
      {
        var jsonElement = (JsonElement)PoliciesDict[policyName];
        var v = JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
        return v;
      }
      return defaultValue;
    }

    private T GetPolicyValueIgnoreZeroValue<T>(string policyName, T defaultValue)
    {
      var v = GetPolicyValue(policyName, defaultValue);
      if (v.Equals(default(T)))
      {
        return defaultValue;
      }
      return v;
    }


    public ConsolidationTxParameters GetMergedConsolidationTxParameters(ConsolidationTxParameters consolidationTxParameters)
    {
      return new ConsolidationTxParameters
      {
        Version = consolidationTxParameters.Version,
        MinConsolidationFactor = GetPolicyValue(ConsolidationPolicies.MinConsolidationFactor, consolidationTxParameters.MinConsolidationFactor),
        MaxConsolidationInputScriptSize = GetPolicyValue(ConsolidationPolicies.MaxConsolidationInputScriptSize, consolidationTxParameters.MaxConsolidationInputScriptSize),
        // Bitcoind for now ignores value 0 for confirmations and uses the default value of 6. Utxos must be confirmed.
        // If this changes: CollectPreviousOutputs that sets IsStandard = true must be updated.
        // We can not use results from getutxos from node, because node does not yet have this output.
        MinConfConsolidationInput = GetPolicyValueIgnoreZeroValue(ConsolidationPolicies.MinConfConsolidationInput, consolidationTxParameters.MinConfConsolidationInput),
        AcceptNonStdConsolidationInput = GetPolicyValue(ConsolidationPolicies.AcceptNonStdConsolidationInput, consolidationTxParameters.AcceptNonStdConsolidationInput)
      };
    }
  }
}
