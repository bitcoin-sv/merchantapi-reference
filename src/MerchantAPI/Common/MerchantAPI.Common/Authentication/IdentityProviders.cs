// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.Options;

namespace MerchantAPI.Common.Authentication
{
  public class IdentityProviders 
  {
    public IdentityProvider[] Providers{ get; set; }
  }

  public class IdentityProvidersValidator : IValidateOptions<IdentityProviders>
  {
    public ValidateOptionsResult Validate(string name, IdentityProviders options)
    {
      if (options.Providers != null)
      {
        foreach (var provider in options.Providers)
        {
          var validationResults = new List<ValidationResult>();
          var validationContext = new ValidationContext(provider, serviceProvider: null, items: null);
          if (!Validator.TryValidateObject(provider, validationContext, validationResults, true))
          {
            return ValidateOptionsResult.Fail(string.Join(",",
              validationResults.Select(x => x.ErrorMessage).ToArray()));
          }
        }
      }
      return ValidateOptionsResult.Success;

    }
  }
}
