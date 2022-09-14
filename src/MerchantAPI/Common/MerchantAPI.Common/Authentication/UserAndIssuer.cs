// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

namespace MerchantAPI.Common.Authentication
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
