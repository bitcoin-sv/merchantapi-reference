// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;

namespace TokenManager
{
  static class Program
  {
    static SymmetricSecurityKey ParseKey(string key)
    {

      if (key.Length < SymmetricSignatureProvider.DefaultMinimumSymmetricKeySizeInBits / 8)
      {
        Console.Error.WriteLine($"Key should be at lest {SymmetricSignatureProvider.DefaultMinimumSymmetricKeySizeInBits / 8} characters long");
        return null;
      }
      // Use encoder that throws exception if character is not supported to make sure that key only consists for ASCII characters
      Encoding ae = Encoding.GetEncoding(
        Encoding.ASCII.EncodingName,
        new EncoderExceptionFallback(),
        new DecoderExceptionFallback());

      return new SymmetricSecurityKey(ae.GetBytes(key));
    }
    static int ValidateToken(string token, string key)
    {
      var securityKey = ParseKey(key);
      if (securityKey == null)
      {
        return -1;
      }

      var tokenHandler = new JwtSecurityTokenHandler();
      try
      {
        IdentityModelEventSource.ShowPII = true; // Show private information in exception (since token was input to the program anyway)
        tokenHandler.ValidateToken(token,
          new TokenValidationParameters
          {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = false,
            ValidateAudience = false,
            IssuerSigningKey = securityKey
          }, out SecurityToken validatedToken);

        Console.WriteLine("Token signature and time constraints are OK. Issuer and audience were not validated.");
        Console.WriteLine("Token:");
        Console.WriteLine(validatedToken);

      }
      catch (Exception ex)
      {
        Console.WriteLine("ValidateToken failed: " + ex.Message);
        return -1;
      }

      return 0;
    }

    static int GenerateToken(string name, int days, string key, string issuer, string audience)
    {
      var securityKey = ParseKey(key);
      if (securityKey == null)
      {
        return -1;
      }

      var tokenHandler = new JwtSecurityTokenHandler();
      var tokenDescriptor = new SecurityTokenDescriptor
      {
        Subject = new ClaimsIdentity(new[]
        {
          new Claim("sub", name), // "sub" for subject
        }),
        NotBefore = DateTime.UtcNow,
        Expires = DateTime.UtcNow.AddDays(days),
        Issuer = issuer,
        Audience = audience,
        SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature)
      };

      
      var token = tokenHandler.CreateToken(tokenDescriptor);
      string tokenString = tokenHandler.WriteToken(token);
      Console.WriteLine("Token:" + token);
      Console.WriteLine($"Valid until UTC: {tokenDescriptor.Expires}");
      Console.WriteLine();
      Console.WriteLine("The following should be used as authorization header:");
      Console.WriteLine("Bearer " + tokenString);
      

      return 0;
    }



    static int Main(string[] args)
    {

      var optionSecretKey = new Option<string>(
        new[] {"--key", "-k"},
        description:
        $"Secret shared use to sign the token. At lest {SymmetricSignatureProvider.DefaultMinimumSymmetricKeySizeInBits / 8} characters"
      )
      {
        IsRequired = true,
      };


      // Create a root command with some options
      var generateCommand = new Command("generate")
      {
        new Option<string>(
          new [] {"--name", "-n"},
          description: "Unique name of the subject token is being issued to"
        )
        {
          IsRequired = true
        },
        new Option<int>(
          new [] {"--days", "-d"},
          description: "Days the token will be valid for"
          )
        {
          IsRequired = true
        },
        optionSecretKey,

        new Option<string>(
          new [] {"--issuer", "-i"},
          description: "Unique issuer of the token (for example URI identifiably the miner)"
        )
        {
          IsRequired = true
        },
        new Option<string>(
          new [] {"--audience", "-a"},
          description: "Audience tha this token should be used for",
          getDefaultValue: () => "merchant_api"
        )
      };

      generateCommand.Description = "Generates a new JWT token using a symmetric signature algorithm HS256";

      var validateCommand = new Command("validate")
      {
        optionSecretKey,
        new Option<string>(
          new[] {"--token", "-t"},
          description: "security token formatted as string"
        )
        {
          IsRequired = true
        }
      };

      validateCommand.Description = "Validates a HS256 JWT token signature";

      var rootCommand = new RootCommand
      {
        generateCommand,
        validateCommand
      };
      
      rootCommand.Description = "TokenManager";

      generateCommand.Handler = CommandHandler.Create((string name, int days, string key, string issuer, string audience) => GenerateToken(name, days, key, issuer, audience));

      validateCommand.Handler = CommandHandler.Create((string token, string key) => ValidateToken(token, key));

      return rootCommand.Invoke(args);
    }


  }
}
