// Copyright (c) 2020 Bitcoin Association

namespace MerchantAPI.Common
{
  public class CommonConst
  {
    public class FeeType
    {
      public const string Standard = "standard";
      public const string Data = "data";

      public static readonly string[] RequiredFeeTypes = { Standard, Data };
    }

    public class AmountType
    {
      public const string MiningFee = "MiningFee";
      public const string RelayFee = "RelayFee";
    }
  }
}
