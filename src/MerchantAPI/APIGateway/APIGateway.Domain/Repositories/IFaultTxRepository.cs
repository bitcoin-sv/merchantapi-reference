// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using static MerchantAPI.APIGateway.Domain.Faults;

namespace MerchantAPI.APIGateway.Domain.Repositories
{
  public interface IFaultTxRepository
  {
    // faults triggering is only possible for certain TxRepository actions
    Task<byte[][]> InsertOrUpdateTxsAsync(DbFaultComponent? faultComponent, IList<Tx> transactions, bool areUnconfirmedAncestors, bool insertTxInputs = true, bool returnInsertedTransactions = false);
    Task UpdateTxsOnResubmitAsync(DbFaultComponent? faultComponent, IList<Tx> transactions);
  }
}
