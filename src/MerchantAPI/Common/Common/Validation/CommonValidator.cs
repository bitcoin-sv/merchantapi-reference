// Copyright (c) 2020 Bitcoin Association

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MerchantAPI.Common.Validation
{
  public class CommonValidator
  {
    public static bool IsUrlValid(string url, string memberName, out string error)
    {
      error = null;
      if (url == null)
      {
        error = $"{memberName}: URL must be provided.";
      }
      else if (Uri.TryCreate(url, UriKind.Absolute, out Uri validatedUri)) //.NET URI validation.
      {
        //If true: validatedUri contains a valid Uri. Check for the scheme in addition.
        if (validatedUri.Scheme != Uri.UriSchemeHttp && validatedUri.Scheme != Uri.UriSchemeHttps)
        {
          error = $"{memberName}: { url } uses invalid scheme. Only 'http' and 'https' are supported";
        }
      }
      else
      {
        error = $"{memberName}: { url } should be a valid URL";
      }
      return error == null;
    }

  // https://docs.microsoft.com/en-us/dotnet/standard/base-types/how-to-verify-that-strings-are-in-valid-email-format
  public static bool IsEmailValid(string email)
  {
    if (string.IsNullOrWhiteSpace(email))
    {
      return false;
    }
    try
    {
      // Normalize the domain
      email = Regex.Replace(email, @"(@)(.+)$", DomainMapper,
                            RegexOptions.None, TimeSpan.FromMilliseconds(200));

        // Examines the domain part of the email and normalizes it.
        static string DomainMapper(Match match)
      {
        // Use IdnMapping class to convert Unicode domain names.
        var idn = new IdnMapping();

        // Pull out and process domain name (throws ArgumentException on invalid)
        string domainName = idn.GetAscii(match.Groups[2].Value);

        return match.Groups[1].Value + domainName;
      }
    }
    catch (RegexMatchTimeoutException)
    {
      return false;
    }
    catch (ArgumentException)
    {
      return false;
    }

    try
    {
      return Regex.IsMatch(email,
          @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
          RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
    }
    catch (RegexMatchTimeoutException)
    {
      return false;
    }
  }

  }
}
