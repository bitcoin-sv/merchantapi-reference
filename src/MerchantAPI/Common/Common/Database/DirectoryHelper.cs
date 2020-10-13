// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MerchantAPI.Common.Database
{
  public class DirectoryHelper
  {
    public static string[] GetDirectories(string path)
    {
      string[] dirs = Directory.GetDirectories(path);
      List<string> dirsFiltered = new List<string>();

      foreach (string dir in dirs)
      {
        dirsFiltered.Add(dir);
      }

      dirsFiltered.Sort(new VersionFolderNameSorter());

      return dirsFiltered.ToArray();
    }


    public static Encoding GetSqlScriptFileEncoding(string filePath)
    {
      // default encoding is utf-8
      Encoding encoding = Encoding.GetEncoding("utf-8");

      // we check byte order mark (BOM)
      // *** Detect byte order mark if any - otherwise assume default
      byte[] buffer = new byte[5];
      using (FileStream file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
      {
        int numberOfBytesRead = file.Read(buffer, 0, 5);

        if (numberOfBytesRead >= 3)
        {
          if (buffer[0] == 0xef && buffer[1] == 0xbb && buffer[2] == 0xbf)
            encoding = Encoding.UTF8;
          else if (buffer[0] == 0xfe && buffer[1] == 0xff)
            encoding = Encoding.Unicode;
          else if (buffer[0] == 0 && buffer[1] == 0 && buffer[2] == 0xfe && buffer[3] == 0xff)
            encoding = Encoding.UTF32;
          else if (buffer[0] == 0x2b && buffer[1] == 0x2f && buffer[2] == 0x76)
            encoding = Encoding.UTF7;
        }
      }

      return encoding;
    }


  }
}
