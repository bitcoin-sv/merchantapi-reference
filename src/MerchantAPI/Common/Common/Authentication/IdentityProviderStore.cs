// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MerchantAPI.Common.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;


namespace MerchantAPI.Common.Authentication
{
  /// <summary>
  /// A list of identity providers. use to look up issuers and keys during JWT authorization.
  /// Use GetUserAndIssuer from a controller to get authenticated user data.
  /// </summary>
  public class IdentityProviderStore
  {
    const string UnifiedIdentityClaimName = "MAPI_CLAIM_IDENTITY";

    readonly IdentityProvider[] identityProviders;

    // for fast reverse lookup
    readonly Dictionary<IdentityProvider, SecurityKey> providerTokey;
    public IdentityProviderStore(IOptions<IdentityProviders> providers)
    {
      identityProviders = providers.Value?.Providers ?? new IdentityProvider[] {};
      providerTokey = identityProviders.ToDictionary(k => k, v => v.GetSecurityKey());
    }

    private static string GetBearerHeader(IHeaderDictionary headers)
    {
      headers.TryGetValue("Authorization", out var authorizationHeader);
      return authorizationHeader.SingleOrDefault(x => x.StartsWith("Bearer")); 
    }

    /// <summary>
    /// Extract user and issuer from authenticated user. Returns null if not found or if user is not authenticated
    /// </summary>
    /// <param name="user"></param>
    /// <returns>
    /// false: if we were not able to perform authentication because token was not formatted correctly and there was exception during authorization
    /// When false is returned the request should be rejected
    /// true: if there was no token (anonymous user) or we were able to extract user identity
    /// </returns>
    public static bool GetUserAndIssuer(ClaimsPrincipal user, IHeaderDictionary headers, out UserAndIssuer result)
    {
      result = null;
      var authorizationHeader = GetBearerHeader(headers);
      bool tokenPresentInRequest = authorizationHeader != null;

      // User can have multiple identities. Find the right one
      var theIdentity = user.Identities.FirstOrDefault(ci => ci.IsAuthenticated && ci.HasClaim(c => c.Type == UnifiedIdentityClaimName));
      
      if (theIdentity == null)
      {
        return !tokenPresentInRequest;
      }
      
      result = new UserAndIssuer
      {
        Identity = theIdentity.FindFirst(c => c.Type == UnifiedIdentityClaimName).Value,
        IdentityProvider = theIdentity.FindFirst(c => c.Type == JwtRegisteredClaimNames.Iss).Value
      };
      return true;
    }

    SecurityKey GetKeyOrNull(IdentityProvider provider)
    {
      if (providerTokey.TryGetValue(provider, out var key))
      {
        return key;
      }

      return null;
    }

    static bool KeysEqual(SecurityKey k1, SecurityKey k2)
    {
      return
        k1 != null && k2 != null
                   && k1.GetType() == k2.GetType()
                   && k1 is SymmetricSecurityKey k1s
                   && k2 is SymmetricSecurityKey k2s
                   && HelperTools.AreByteArraysEqual(k1s.Key, k2s.Key);
    }

    
    public IEnumerable<SecurityKey> IssuerSigningKeyResolver(
      string token,
      SecurityToken securityToken,
      string kid,
      TokenValidationParameters validationParameters)
    {
      return identityProviders.Where(
          x => securityToken.Issuer == x.Issuer && x.MatchesToken(securityToken))
        .Select(GetKeyOrNull)
        .Where(x => x != null);
    }


    IdentityProvider FindProviderWithSigningKey(SecurityToken securityToken)
    {
      if (securityToken.SigningKey == null)
      {
        throw new ArgumentNullException(nameof(securityToken.SigningKey));
      }

      return identityProviders.FirstOrDefault(
        x => securityToken.Issuer == x.Issuer && x.MatchesToken(securityToken)
                                              && KeysEqual(x.GetSecurityKey(), GetKeyOrNull(x)));
    }

    
    public Task OnTokenValidated(TokenValidatedContext arg)
    {

      var jwt = arg.SecurityToken as JwtSecurityToken;
      if (jwt is null)
      {
        arg.Fail("A JWT token expected");
        return Task.CompletedTask;
      }

      if (arg.Principal.HasClaim(x => x.Type == UnifiedIdentityClaimName))
      {
        // make sure that JWT does not include our internal claim
        arg.Fail("Token contains a forbidden claim");
        return Task.CompletedTask;
      }

      var provider = FindProviderWithSigningKey(arg.SecurityToken);
      if (provider == null)
      {
        // Shouldn't happen but if it does, controller authentication code will fail 
        return Task.CompletedTask;
      }

      var claimName = provider.IdentityClaimName ?? IdentityProvider.DefaultIdentityClaimName;

      // We extract value  directly from jwt instead of using arg.Principal.Claimssince type in Principal are already mapepd (we could turn this off)
      var name = jwt.Claims.SingleOrDefault(x => x.Type == claimName)?.Value;

      if (string.IsNullOrEmpty(name))
      {
        arg.Fail($"Token does not contain required claim {claimName}");
        return Task.CompletedTask;
      }

      var claims = new List<Claim>
      {
        new Claim(UnifiedIdentityClaimName, name),
        new Claim(JwtRegisteredClaimNames.Iss, provider.Issuer)

      };

      var appIdentity = new ClaimsIdentity(claims, arg.Scheme.Name);

      // Add another identity. It can be extracted with GetUserAndIssuer
      // As an alternative , we could override static callback ClaimsPrincipal.PrimaryIdentitySelector, but 
      // this could cause conflict with other code

      arg.Principal.AddIdentity(appIdentity);
      return Task.CompletedTask;
    }


  }
}
