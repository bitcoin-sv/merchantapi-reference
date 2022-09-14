// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;


namespace MerchantAPI.Common.Authentication
{
  public class ConfigureJwtBearerOptions : IPostConfigureOptions<JwtBearerOptions>
  {
    readonly IdentityProviderStore store;

    public ConfigureJwtBearerOptions( IdentityProviderStore store)
    {
      this.store = store;
    }

    public void PostConfigure(string name, JwtBearerOptions options)
    {
      options.TokenValidationParameters.IssuerSigningKeyResolver = store.IssuerSigningKeyResolver;
      options.Events = new JwtBearerEvents
      {
        OnTokenValidated = store.OnTokenValidated
      };    
    }
  }
}
