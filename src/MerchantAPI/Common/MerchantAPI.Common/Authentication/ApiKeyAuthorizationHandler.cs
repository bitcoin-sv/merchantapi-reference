// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace MerchantAPI.Common.Authentication
{
   public class ApiKeyAuthorizationHandler<T>: ApiKeyAuthenticationHandler<T> where T : CommonAppSettings, new()
  {
    // supports Authorization with Bearer token
    public const string AuthorizationHeaderName = "Authorization";

    public ApiKeyAuthorizationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IOptions<T> appSettingOptions) : base(options, logger, encoder, clock, appSettingOptions)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
      if (Logger.IsEnabled(LogLevel.Trace))
      {
        var builder = new StringBuilder(Environment.NewLine);
        builder.AppendLine($"HandleAuthenticateAsync:");
        foreach (var header in Request.Headers)
        {
          builder.AppendLine($"{header.Key}:{header.Value}");
        }

        Logger.LogTrace(builder.ToString());
      }

      return await Task.Run(() =>
      {
        if (!Request.Headers.TryGetValue(AuthorizationHeaderName, out var apiKeyHeaderValues))
        {
          return AuthenticateResult.Fail("No API Key provided.");
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();

        if ($"Bearer {appSettings.RestAdminAPIKey}" == providedApiKey)
        {
          var ticket = CreateAuthenticationTicket(providedApiKey, ApiKeyAuthenticationOptions.BearerScheme);
          return AuthenticateResult.Success(ticket);
        }

        return AuthenticateResult.Fail("Invalid API Key provided.");
      });
    }
  }
}
