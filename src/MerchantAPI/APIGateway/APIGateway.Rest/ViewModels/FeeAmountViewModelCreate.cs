// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class FeeAmountViewModelCreate : FeeAmountViewModelGet
  {
    // for now same
    public FeeAmountViewModelCreate() { }
    public FeeAmountViewModelCreate(FeeAmount feeAmount): base(feeAmount) { }
  }
}
