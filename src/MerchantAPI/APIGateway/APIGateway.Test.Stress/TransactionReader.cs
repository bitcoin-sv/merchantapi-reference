// Copyright (c) 2020 Bitcoin Association

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
    object objLock = new object();

    int txIndex;
    IEnumerator<string> lines;
    bool hasCurrent;
    long limit;
    long returnedCount = 0;

    public TransactionReader(string fileName, int txIndex, long limit)
    {
      this.txIndex = txIndex;
      lines = File.ReadLines(fileName).GetEnumerator();
      hasCurrent = lines.MoveNext();
      this.limit = limit;
    }

    public bool TryGetnextTransaction(out string transaction)
    {
      lock (objLock)
      {
        if (!hasCurrent || returnedCount == limit)
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

        returnedCount++;
        return true;
      }
    }
  }
}
