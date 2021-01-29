// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.Common;
using MerchantAPI.Common.Validation;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class SubmitTransaction : IValidatableObject
  {
    public byte[] RawTx { get; set; }
    public string RawTxString { get; set; }
    public string  CallbackUrl { get; set; }

    public string CallbackToken { get; set; }

    public string CallbackEncryption { get; set; }

    public bool MerkleProof { get; set; }
    public bool DsCheck { get; set; }

    public IList<TxInput> TransactionInputs { get; set; }

    public static IEnumerable<ValidationResult> IsSupportedCallbackUrl(string url, string memberName)
    {
      if (!string.IsNullOrEmpty(url))
      {
        if (!CommonValidator.IsUrlValid(url, memberName, out var error))
        {
          yield return new ValidationResult(error);
        }
      }
    }

    public static IEnumerable<ValidationResult> IsSupportedEncryption(string s, string memberName)
    {
      if (!string.IsNullOrEmpty(s))
      {
        if (!MapiEncryption.IsEncryptionSupported(s))
        {
          yield return new ValidationResult($"{memberName} contains unsupported encryption type");
        }

        // 1024 is DB limit. It should not happen.
        if (s.Length > 1024)
        {
          yield return new ValidationResult($"{memberName} contains encryption token that is too long");
        }
      }
    }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      if (string.IsNullOrWhiteSpace(CallbackUrl) && (MerkleProof || DsCheck))
      {
        yield return new ValidationResult($"{nameof(CallbackUrl)} is required when {nameof(MerkleProof)} or {nameof(DsCheck)} is not false");
      }

      foreach (var x in IsSupportedCallbackUrl(CallbackUrl, nameof(CallbackUrl)))
      {
        yield return x;
      }

      foreach (var x in  IsSupportedEncryption(CallbackEncryption, nameof(CallbackEncryption)))
      {
        yield return x;
      }
    }
  }
}
