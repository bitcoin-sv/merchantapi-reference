// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

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
