// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;

namespace MerchantAPI.APIGateway.Domain.Models.Faults
{
  public class FaultException : Exception
  {
    public FaultException(string message) : base(message)
    {
    }
  }
}
