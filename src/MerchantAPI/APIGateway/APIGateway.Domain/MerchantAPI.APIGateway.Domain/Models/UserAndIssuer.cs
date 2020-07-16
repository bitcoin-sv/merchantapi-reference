// Copyright (c) 2020 Bitcoin Association

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class UserAndIssuer
  {
    public string Identity { get; set; }
    public string IdentityProvider { get; set; }

    public override string ToString()
    {
      return base.ToString() + ": " + (Identity ?? "") + " " + (IdentityProvider ?? "");
    }
  }
}
