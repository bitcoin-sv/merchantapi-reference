// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class Fee: IValidatableObject
  {
    public long Id { get; set; }

    public string FeeType { get; set; }

    public FeeAmount MiningFee { get; set; }

    public FeeAmount RelayFee { get; set; }

    public void SetFeeAmount(FeeAmount feeAmount)
    {
      if (feeAmount.IsMiningFee())
      {
        MiningFee = feeAmount;
      }
      else if (feeAmount.IsRelayFee())
      {
        RelayFee = feeAmount;
      }
      else
      {
        throw new Exception("Invalid feeAmountType.");
      }
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      if (String.IsNullOrEmpty(FeeType))
      {
        yield return new ValidationResult($"Fee: value for {nameof(FeeType)} must not be null or empty.");
      }
      if (MiningFee == null)
      {
        yield return new ValidationResult($"Fee: null value for {nameof(MiningFee)} is invalid.");
      }
      else
      {
        if (!MiningFee.IsMiningFee())
        {
          yield return new ValidationResult($"Fee: type of {nameof(MiningFee)} is invalid ({MiningFee.FeeAmountType}).");
        }
        foreach (var result in MiningFee.Validate(validationContext))
        {
          yield return result;
        }
      }

      if (RelayFee == null)
      {
        yield return new ValidationResult($"Fee: null value for {nameof(RelayFee)} is invalid.");
      }
      else
      {
        if (!RelayFee.IsRelayFee())
        {
          yield return new ValidationResult($"Fee: type of {nameof(RelayFee)} is invalid ({RelayFee.FeeAmountType}).");
        }
        foreach (var result in RelayFee.Validate(validationContext))
        {
          yield return result;
        }
      }

    }
  }

}
