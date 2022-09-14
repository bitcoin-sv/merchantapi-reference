// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.ComponentModel.DataAnnotations;

namespace MerchantAPI.Common.Authentication
{
  public class CommonAppSettings
  {
    [Required]
    public string RestAdminAPIKey { get; set; }
  }
}
