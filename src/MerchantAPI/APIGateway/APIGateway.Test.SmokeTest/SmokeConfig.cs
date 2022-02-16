// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.ComponentModel.DataAnnotations;

namespace MerchantAPI.APIGateway.Test.SmokeTest
{
  public class SmokeConfig
  {

    public MapiConfig MapiConfig { get; set; }

    public Node Node { get; set; }

    public CallbackConfig Callback { get; set; }
  }
  public class MapiConfig
  {
    public string AdminAuthorization { get; set; }

    [Required]
    public string MapiUrl { get; set; }    
  }

  public class CallbackConfig
  {
    [Required]
    public string Url { get; set; }

    [Required]
    public string Token { get; set; }

    public string Encryption { get; set; }

    public bool DsCheck { get; set; }

    public bool MerkleProof { get; set; }

    public string MerkleFormat { get; set; } = "TSC";
  }

  public class Node
  {
    [Required]
    public string Host { get; set; }

    [Required]
    public int Port { get; set; }

    [Required]
    public string Username { get; set; }

    [Required]
    public string Password { get; set; }

    [Required]
    public string ZMQ { get; set; }
    
  }
}
