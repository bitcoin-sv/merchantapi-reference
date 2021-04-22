// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class Node 
  {
    public long Id { get; private set; }
    public string Host { get; private set; }
    public int Port { get; private set; }
    public string Username { get; private set; }
    public string Password { get; private set; }

    public string Remarks { get; private set; }

    public string ZMQNotificationsEndpoint { get; private set; }
    public NodeStatus Status { get; private set; }
    public string LastError { get; private set; }
    public DateTime? LastErrorAt { get; private set; }

    public Node() 
    {
    }

    public Node(string host, Int32 port, string username, string password, string remarks, string zmqnotificationsendpoint)
        : this(int.MinValue, host, port, username, password, remarks, zmqnotificationsendpoint, (int)NodeStatus.Connected, null, null)
    {
    }

    public Node(Int32 nodeid, string host, Int32 port, string username, string password, string remarks, string zmqnotificationsendpoint,Int32 nodestatus, String lasterror, DateTime? lasterrorat)
    {
      Id = nodeid;
      Host = host;
      Port = port;
      Username = username;
      Password = password;

      Status = (NodeStatus)nodestatus;
      LastError = lasterror;
      LastErrorAt = lasterrorat;
      Remarks = remarks;
      ZMQNotificationsEndpoint = zmqnotificationsendpoint;
    }

    public string ToExternalId()
    {
      return Host + ":" + Port.ToString();
    }

    public override string ToString()
    {
      return ToExternalId();
    }

    public static (string host, int port) SplitHostAndPort(string hostAndPort)
    {
      if (string.IsNullOrEmpty(hostAndPort))
      {
        throw new Exception($"'{nameof(hostAndPort)} must not be empty");
      }

      var split = hostAndPort.Split(':');
      if (split.Length != 2)
      {
        throw new Exception($"'{nameof(hostAndPort)} must be separated by exactly one ':'");
      }

      return (split[0], int.Parse(split[1]));

    }

    public Node Clone()
    {
      return new Node((int)Id, Host, Port, Username, Password, Remarks, ZMQNotificationsEndpoint, (int)Status, LastError, LastErrorAt);
    }

  }
}
