using System.Threading.Tasks;

namespace MerchantAPI.Common
{
  public interface IStartupChecker
  {
    public Task<bool> CheckAsync(bool testingEnvironment=false);
  }
}
