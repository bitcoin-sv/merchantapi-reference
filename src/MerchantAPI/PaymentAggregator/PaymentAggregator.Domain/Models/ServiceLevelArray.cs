// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Consts;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace MerchantAPI.PaymentAggregator.Domain.Models
{
  public class ServiceLevelArray : IValidatableObject
  {
    public ServiceLevel[] ServiceLevels{ get; set; }

    public ServiceLevelArray() { }

    public ServiceLevelArray(ServiceLevel[] serviceLevels) 
    {
      ServiceLevels = serviceLevels;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      if (!ServiceLevels.Any())
      {
        yield return new ValidationResult($"ServiceLevels: {nameof(ServiceLevelArray)} must contain at least on element.");
      }
      else
      {        
        var serviceLevels = ServiceLevels.Where(x => x != null).ToArray();

        foreach (var serviceLevel in serviceLevels)
        {
          foreach (var result in serviceLevel.Validate(validationContext))
          {
            yield return result;
          }
        }

        int minLevel = serviceLevels.Select(i => i.Level).Min();
        if (minLevel != 0)
        {
          yield return new ValidationResult($"ServiceLevels: value of {nameof(ServiceLevel.Level)} for lowest ServiceLevel must be 0.");
        }

        var missingParents = serviceLevels.Where(x => x.Level > 0 && serviceLevels.Count(p => p.Level == x.Level - 1) == 0);
        if (missingParents.Any()) 
        {
          yield return new ValidationResult($"ServiceLevels: value for {nameof(ServiceLevel.Level)} is invalid. Levels must start from 0 and increment by 1.");
        }

        // only serviceLevel with the highest Level must have Fees null
        int maxLevel = serviceLevels.Select(i => i.Level).Max();
        if (serviceLevels.Any( x => x.Fees != null && x.Level == maxLevel))
        {
          yield return new ValidationResult($"ServiceLevels: value of {nameof(ServiceLevel.Fees)} for highest ServiceLevel must be null.");
        }
        if (serviceLevels.Any(x => x.Fees == null && x.Level != maxLevel))
        {
          yield return new ValidationResult($"ServiceLevels: value of {nameof(ServiceLevel.Fees)} for all but highest ServiceLevel must contain at least two Fees ('{ string.Join("', '", Const.FeeType.RequiredFeeTypes) }').");
        }

        if (maxLevel > 0)
        {
          var allFeeTypes = new HashSet<string>(serviceLevels.First( x => x.Fees != null).Fees.Where(x => x != null).Select(x => x.FeeType));
          if (Const.FeeType.RequiredFeeTypes.Intersect(allFeeTypes).Count() != Const.FeeType.RequiredFeeTypes.Length)
          {
            yield return new ValidationResult($"ServiceLevels: values for {nameof(Fee.FeeType)} are invalid. Fees on all levels (except last) must contain fees with feeTypes: '{ string.Join("', '", Const.FeeType.RequiredFeeTypes) }'.");
          }
          var areFeeTypesOnEveryLevelSame = serviceLevels.Where(x => x.Fees != null).All(x => x.Level < maxLevel && allFeeTypes.SetEquals(x.Fees.Where(x => x != null).Select(x => x.FeeType).ToHashSet()));
          if (!areFeeTypesOnEveryLevelSame)
          {
            yield return new ValidationResult($"ServiceLevels: values for {nameof(Fee.FeeType)} are invalid. Fees on all levels (except last) must have same feeTypes defined.");
          }

          foreach (var feeType in allFeeTypes)
          {
            var fees = serviceLevels.Where(x => x.Fees != null)  // last serviceLevel has Fees null, so we skip it (also some other, if not valid)
                                                .OrderBy(x => x.Level)
                                                .SelectMany(x => x.Fees)
                                                .Where(x => x?.FeeType == feeType).ToArray();
            var miningFees = fees.Select(x => x.MiningFee);
            if (!IsIncremental(miningFees))
            {
              yield return new ValidationResult($"ServiceLevels: MiningFees for FeeType { feeType } are not incremental per level.");
            }
            var relayFees = fees.Select(x => x.RelayFee);
            if (!IsIncremental(relayFees))
            {
              yield return new ValidationResult($"ServiceLevels: RelayFees for FeeType { feeType } are not incremental per level.");
            }
          }
        }

      }
    }

    private bool IsIncremental(IEnumerable<FeeAmount> feeAmounts)
    {
      return feeAmounts.Zip(feeAmounts.Skip(1), (a, b) => (float)a.Satoshis/a.Bytes < (float)b.Satoshis/b.Bytes).All(x => x);
    }
  }
}
