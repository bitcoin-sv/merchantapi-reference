// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Dapper;
using System;
using System.Data;

namespace MerchantAPI.Common.TypeHandlers
{
  public class DateTimeHandler : SqlMapper.TypeHandler<DateTime>
  {
    public override void SetValue(IDbDataParameter parameter, DateTime value)
    {
      parameter.Value = value;
    }

    public override DateTime Parse(object value)
    {
      return DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc);
    }
  }
}
