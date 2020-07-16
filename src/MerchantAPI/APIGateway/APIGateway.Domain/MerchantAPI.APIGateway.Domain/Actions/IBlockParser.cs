// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Domain.Actions
{
  public interface IBlockParser
  {
    /// <summary>
    /// Check if database is empty and insert first block
    /// </summary>
    Task InitializeDB();
  }
}
