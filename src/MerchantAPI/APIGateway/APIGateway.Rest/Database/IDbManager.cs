// Copyright (c) 2020 Bitcoin Association

namespace MerchantAPI.APIGateway.Rest.Database
{
  public interface IDbManager
  {
    public bool DatabaseExists();
    public bool CreateDb(out string errorMessage, out string errorMessageShort);
  }
}
