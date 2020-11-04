// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using MerchantAPI.APIGateway.Domain.Actions;

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
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl))
        {
          yield return new ValidationResult($"{memberName} should be a valid URL");
        }
        else if (
          string.Compare(parsedUrl.Scheme, "http", StringComparison.InvariantCultureIgnoreCase) != 0 &&
          string.Compare(parsedUrl.Scheme, "https", StringComparison.InvariantCultureIgnoreCase) != 0)
        {
          yield return new ValidationResult(
            $"{memberName} uses invalid scheme. Only 'http' and 'https' are supported");
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
