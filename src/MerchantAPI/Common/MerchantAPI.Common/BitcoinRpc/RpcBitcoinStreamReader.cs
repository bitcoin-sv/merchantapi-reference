﻿// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.Common.BitcoinRpc
{
  public class RpcBitcoinStreamReader : Stream
  {
    protected StreamReader StreamReader { get; private set; }

    readonly CancellationToken? token;

    public long TotalBytesRead { get; protected set; }


    public RpcBitcoinStreamReader(StreamReader streamReader, CancellationToken? token) : base()
    {
      StreamReader = streamReader;
      this.token = token;
    }
    public override bool CanRead => StreamReader.BaseStream.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => StreamReader.BaseStream.Length / 2;

    /// <summary>
    /// Position values are correct only if they target position inside the value part of the "result" field 
    /// </summary>
    public override long Position { get => StreamReader.BaseStream.Position / 2; set => StreamReader.BaseStream.Position = value * 2; }

    public override void Flush()
    {
      throw new NotImplementedException();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
      token?.ThrowIfCancellationRequested();

      // Because the data is HEX encoded, we need to read 2 chars for each returned byte
      var charBuffer = new char[count * 2];
      Memory<char> memory = new(charBuffer, offset, count * 2);
      var readCount = await StreamReader.ReadBlockAsync(memory, cancellationToken);

      return CheckCountAndReadBlock(buffer, count, charBuffer, readCount);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      if (token != null)
      {
        return ReadAsync(buffer, offset, count, token.Value).Result;
      }

      // Because the data is HEX encoded, we need to read 2 chars for each returned byte
      var charBuffer = new char[count * 2];
      var readCount = StreamReader.ReadBlock(charBuffer, offset, count * 2);

      return CheckCountAndReadBlock(buffer, count, charBuffer, readCount);
    }

    private int CheckCountAndReadBlock(byte[] buffer, int count, char[] charBuffer, int readCount)
    {
      if (readCount == 0)
      {
        return 0;
      }

      // The amount of data read from stream should always be twice the size of count parameter
      if (readCount != (count * 2))
      {
        throw new RpcException("Error when executing bitcoin RPC method. RPC response contains invalid HEX data in JSON response", null, null);
      }

      var hexChar = new char[2];
      for (int i = 0; i < readCount; i += 2)
      {
        hexChar[0] = charBuffer[i];
        hexChar[1] = charBuffer[i + 1];
        buffer[i / 2] = (byte)int.Parse(hexChar, NumberStyles.AllowHexSpecifier);
        TotalBytesRead++;
      }

      return count;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
      throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      throw new NotImplementedException();
    }

    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
      StreamReader.Dispose();
    }

    public override void Close()
    {
      Dispose(true);
    }

  }
}
