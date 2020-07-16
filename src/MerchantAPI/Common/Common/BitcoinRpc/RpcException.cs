// Copyright (c) 2020 Bitcoin Association

using System;

namespace MerchantAPI.Common.BitcoinRpc
{
	public class RpcException : Exception
	{
		public RpcException(string message, string host, Exception innerException) : base(message, innerException)
    {
      this.Host = host;
			Code = 0;
		}

		public RpcException(int code, string message, string host) : base(message)
		{
			Code = code;
			this.Host = host;
		}

		/// <summary>
		/// Detailed error message. Must not be exposed to external client, since it included internal addresses.
		/// </summary>
    public string UnsafeMessage  => $"Error while calling host {Host}. {Message}";
    

    public int Code { get; private set; }

    public string  Host { get; private set; }

    public override string ToString()
    {
      return "Host: " + Host + " " + base.ToString();
    }
  }
}
