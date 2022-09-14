// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;

namespace MerchantAPI.Common.Exceptions
{
  public class RetryException : Exception
  {
    public RetryException(int retries, Exception innerException) 
      : base($"Failed after {retries} retries", innerException)
    {
      Retries = retries;
    }

    public RetryException(int retries, string message, Exception innerException)
      : base(message, innerException)
    {
      Retries = retries;
    }

    public int Retries { get; private set; }
  }
}
