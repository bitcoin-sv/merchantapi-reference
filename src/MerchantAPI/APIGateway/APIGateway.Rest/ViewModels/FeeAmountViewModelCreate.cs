// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class FeeAmountViewModelCreate : FeeAmountViewModelGet
  {
    public FeeAmountViewModelCreate() { }
    public FeeAmountViewModelCreate(FeeAmount feeAmount): base(feeAmount) { }
  }
}
