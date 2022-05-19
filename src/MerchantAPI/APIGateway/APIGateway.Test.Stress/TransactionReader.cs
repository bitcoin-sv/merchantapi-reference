// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.IO;

namespace MerchantAPI.APIGateway.Test.Stress
{
  /// <summary>
  /// Thread safe file reader
  /// </summary>
  class TransactionReader
  {
    readonly object objLock = new();

    readonly int txIndex;
    readonly IEnumerator<string> lines;
    bool hasCurrent;
    long limit;
    readonly int skip;
    public long ReturnedCount { get; private set; }

    public TransactionReader(string fileName, int txIndex, int skip, long limit)
    {
      ReturnedCount = 0;
      this.txIndex = txIndex;
      lines = File.ReadLines(fileName).GetEnumerator();
      hasCurrent = lines.MoveNext();
      this.limit = limit;
      lock (objLock)
      {
        this.skip = 0;
        while (this.skip < skip)
        {
          if (!LimitReached)
          {
            hasCurrent = lines.MoveNext();
          }
          this.skip++;
        }
      }
    }

    public void SetLimit(long limit)
    {
      this.limit = limit;
    }

    public bool LimitReached => !hasCurrent || ReturnedCount == (limit - skip);

    public bool TryGetnextTransaction(out string transaction)
    {
      lock (objLock)
      {
        if (LimitReached)
        {
          transaction = null;
          return false;
        }


        var line = lines.Current;
        hasCurrent = lines.MoveNext();

        var parts = line.Split(';');
        if (parts.Length == 1)
        {
          if (txIndex != 0)
          {
            throw new Exception($"Invalid format of input file. Expected transaction at position {txIndex}. Line: {line}");
          }
          transaction = parts[0];
        }
        else if (txIndex >= parts.Length)
        {
          throw new Exception($"Invalid format of input file. Expected transaction at position {txIndex}. Line: {line}");
        }
        else
        {
          transaction = parts[txIndex];
        }

        ReturnedCount++;
        return true;
      }
    }
  }
}