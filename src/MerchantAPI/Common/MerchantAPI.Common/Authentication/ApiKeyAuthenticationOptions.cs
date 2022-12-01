// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Microsoft.AspNetCore.Authentication;

namespace MerchantAPI.Common.Authentication
{
  public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
  {
    public const string DefaultScheme = "API Key";
    public static string Scheme => DefaultScheme;
    public string AuthenticationType = DefaultScheme;

    public const string Bearer = "Authorization Bearer";
    public static string BearerScheme => Bearer;
    public static string AuthenticationBearerType => Bearer;
  }
}
