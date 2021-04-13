// Copyright (c) 2021 Bitcoin Association

using MerchantAPI.Common.BitcoinRpc;
using System.IO;
using System.Threading;

namespace MerchantAPI.Common.Test
{
  public class RpcBitcoinStreamReaderMock : RpcBitcoinStreamReader
  {
    public RpcBitcoinStreamReaderMock(StreamReader streamReader, CancellationToken? token) : base(streamReader, token) { }

    public override int Read(byte[] buffer, int offset, int count)
    {
      return base.StreamReader.BaseStream.Read(buffer, offset, count);
    }
  }
}
