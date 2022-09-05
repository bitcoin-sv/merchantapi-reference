// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE
using MerchantAPI.APIGateway.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestCategory("TestCategoryNo1")]
  [TestClass]
  public class AppSettingsTest : TestBase
  {
    IValidateOptions<AppSettings> validator;
    static class SettingName
    {
      public const string CallbackIPAddresses = "AppSettings:CallbackIPAddresses";
      public const string Notification = "AppSettings:Notification";
    }

    [TestInitialize]
    public void TestInitialize()
    {
      Initialize(mockedServices: true);
      validator = server.Services.GetRequiredService<IValidateOptions<AppSettings>>();
    }

    [TestCleanup]
    public void TestCleanup()
    {
      Cleanup();
    }

    [TestMethod]
    public void TestCallbackIPAddresses_IPv4()
    { 
      string settingName = SettingName.CallbackIPAddresses;

      AppSettings.CallbackIPAddresses = "120.0.0.255";
      var failures = validator.Validate(settingName, this.AppSettings).Failures;
      Assert.IsNull(failures);

      AppSettings.CallbackIPAddresses = "120.0.0.255:1234";
      failures = validator.Validate(settingName, this.AppSettings).Failures;
      Assert.IsNull(failures);

      AppSettings.CallbackIPAddresses = "127.1:1234"; // compact 127.0.0.1
      failures = validator.Validate(settingName, this.AppSettings).Failures;
      Assert.IsNull(failures);

      AppSettings.CallbackIPAddresses = "120.0.0.256";
      failures = validator.Validate(settingName, this.AppSettings).Failures;
      Assert.IsNotNull(failures);
      Assert.AreEqual("Invalid configuration - CallbackIPAddresses: url '120.0.0.256' is invalid.", failures.Single());

      AppSettings.CallbackIPAddresses = "0.0.0 .0";
      failures = validator.Validate(settingName, this.AppSettings).Failures;
      Assert.IsNotNull(failures);
      Assert.AreEqual("Invalid configuration - CallbackIPAddresses: url '0.0.0 .0' is invalid.", failures.Single());
    }

    [TestMethod]
    public void TestCallbackIPAddresses_IPv6()
    {
      string settingName = SettingName.CallbackIPAddresses;

      // https://iplocation.io/ipv6-compress - shortest form '::'
      AppSettings.CallbackIPAddresses = "::1";
      var failures = validator.Validate(settingName, this.AppSettings).Failures;
      Assert.IsNull(failures);

      AppSettings.CallbackIPAddresses = "[::1]:5000";
      failures = validator.Validate(settingName, this.AppSettings).Failures;
      Assert.IsNull(failures);

      AppSettings.CallbackIPAddresses = "0000::ffff";
      failures = validator.Validate(settingName, this.AppSettings).Failures;
      Assert.IsNull(failures);

      AppSettings.CallbackIPAddresses = "0000:0000:0000:0000:0000:ffff";
      failures = validator.Validate(settingName, this.AppSettings).Failures;
      Assert.IsNotNull(failures);
      Assert.AreEqual("Invalid configuration - CallbackIPAddresses: url '0000:0000:0000:0000:0000:ffff' is invalid.", failures.Single());

      AppSettings.CallbackIPAddresses = "0000::fffg";
      failures = validator.Validate(settingName, this.AppSettings).Failures;
      Assert.IsNotNull(failures);
      Assert.AreEqual("Invalid configuration - CallbackIPAddresses: url '0000::fffg' is invalid.", failures.Single());
    }

    [TestMethod]
    public void TestCallbackIPAddresses_Multiple()
    {
      string settingName = SettingName.CallbackIPAddresses;

      AppSettings.CallbackIPAddresses = "::1,127.0.0.1";
      var failures = validator.Validate(settingName, this.AppSettings).Failures;
      Assert.IsNull(failures);

      AppSettings.CallbackIPAddresses = "[::1]:5555,127.0.0.1:5555";
      failures = validator.Validate(settingName, this.AppSettings).Failures;
      Assert.IsNull(failures);

      AppSettings.CallbackIPAddresses = "0000::ffff, 127.0.0.1";
      failures = validator.Validate(settingName, this.AppSettings).Failures;
      Assert.IsNotNull(failures);
      Assert.AreEqual("Invalid configuration - CallbackIPAddresses: url ' 127.0.0.1' is invalid.", failures.Single());
    }

    [TestMethod]
    public void TestNotificationNull()
    {
      AppSettings.Notification = null;
      var failures = validator.Validate(SettingName.Notification, this.AppSettings).Failures;
      Assert.IsNotNull(failures);
      Assert.AreEqual("Invalid configuration - Notification settings must be specified.", failures.Single());
    }

    [TestMethod]
    public void TestNotificationHostResponseTimeout()
    {
      AppSettings.Notification.SlowHostResponseTimeoutMS = 1000;
      AppSettings.Notification.FastHostResponseTimeoutMS = 2000;
      var failures = validator.Validate(SettingName.Notification, this.AppSettings).Failures;
      Assert.IsNotNull(failures);
      Assert.AreEqual("Value for SlowHostResponseTimeoutMS must be greater than FastHostResponseTimeoutMS.", failures.Single());

      AppSettings.Notification.FastHostResponseTimeoutMS = 1000;
      failures = validator.Validate(SettingName.Notification, this.AppSettings).Failures;
      Assert.IsNotNull(failures);
      Assert.AreEqual("Value for SlowHostResponseTimeoutMS must be greater than FastHostResponseTimeoutMS.", failures.Single());
    }
  }
}
