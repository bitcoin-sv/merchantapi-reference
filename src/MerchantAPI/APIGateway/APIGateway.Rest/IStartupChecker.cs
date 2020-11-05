using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Rest
{
  public interface IStartupChecker
  {
    public Task<bool> CheckAsync(bool testingEnvironment=false);
  }
}
