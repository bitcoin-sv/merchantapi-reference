// Copyright (c) 2020 Bitcoin Association

using System;

namespace MerchantAPI.APIGateway.Test.Functional.Attributes
{
  /// <summary>
  /// Atribute to Override setting
  /// </summary>
  public class OverrideSettingAttribute : Attribute
  {

    public string SettingName { get; private set; }
    public object SettingValue { get; private set; }

    public OverrideSettingAttribute(string settingName, object settingValue)
    {
      SettingName = settingName;
      SettingValue = settingValue;
    }
  }
}