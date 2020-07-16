// Copyright (c) 2020 Bitcoin Association

using Microsoft.AspNetCore.Authentication;

namespace MerchantAPI.APIGateway.Rest.Authentication
{
  public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
  {
    public const string DefaultScheme = "API Key";
    public string Scheme => DefaultScheme;
    public string AuthenticationType = DefaultScheme;
  }
}
