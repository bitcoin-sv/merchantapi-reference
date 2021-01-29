using System.Threading.Tasks;

namespace MerchantAPI.Common.Startup
{
  public interface IStartupChecker
  {
    public Task<bool> CheckAsync(bool testingEnvironment=false);
  }
}
