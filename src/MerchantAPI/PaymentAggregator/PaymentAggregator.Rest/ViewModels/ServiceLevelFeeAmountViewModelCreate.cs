// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.Domain.Models;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class ServiceLevelFeeAmountViewModelCreate : ServiceLevelFeeAmountViewModelGet
  {
    // for now same
    public ServiceLevelFeeAmountViewModelCreate() { }
    public ServiceLevelFeeAmountViewModelCreate(FeeAmount feeAmount) : base(feeAmount) { }

  }
}
