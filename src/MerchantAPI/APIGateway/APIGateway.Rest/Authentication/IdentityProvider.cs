// Copyright (c) 2020 Bitcoin Association

using System;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace MerchantAPI.APIGateway.Rest.Authentication
{
  /// <summary>
  /// Describe one identity provider. Only HmacSha256 is currently supported.
  /// </summary>
  public class IdentityProvider
  {
    const string defaultSignatureAlgorith = SecurityAlgorithms.HmacSha256;

    public const string DefaultIdentityClaimName = "sub";

    [Required]
    public string Issuer { get; set; }

    public string Audience { get; set; } 

    [Required]
    public string SymmetricSecurityKey { get; set; }

    public string Algorithm { get; set; } 

    /// <summary>
    /// The name  eof the claim that contains the identity. If not specified DefaultIdentityClaimName is used.
    /// </summary>
    public string IdentityClaimName { get; set; }

    public SecurityKey GetSecurityKey()
    {
      if (!string.IsNullOrEmpty(SymmetricSecurityKey))
      {
        return new SymmetricSecurityKey(Encoding.ASCII.GetBytes(SymmetricSecurityKey));
      }

      return null;
    }

    public bool MatchesToken(SecurityToken token)
    {
      if (!(token is JwtSecurityToken jwt))
      {
        return false;
      }
      var requiredSignatureAlgorithm = Algorithm ?? defaultSignatureAlgorith;
      if (requiredSignatureAlgorithm != jwt.SignatureAlgorithm
          || Issuer != token.Issuer
          || Audience != null && !jwt.Audiences.Contains(Audience)
      )
      {
        return false;
      }

      return true;
    }

  }

}
