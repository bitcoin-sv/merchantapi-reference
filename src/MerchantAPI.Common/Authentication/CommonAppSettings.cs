// Copyright (c) 2020 Bitcoin Association

using System.ComponentModel.DataAnnotations;

namespace MerchantAPI.Common.Authentication
{
  public class CommonAppSettings
  {
    [Required]
    public string RestAdminAPIKey { get; set; }
  }
}
