// Copyright (c) 2020 Bitcoin Association

using System;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class Block
  {
    public long BlockInternalId { get; set; }

    public DateTime BlockTime { get; set; }

    public byte[] BlockHash { get; set; }
    
    public byte[] PrevBlockHash { get; set; }

    public long? BlockHeight { get; set; }

    public bool OnActiveChain { get; set; }

    public DateTime? ParsedForMerkleAt { get; set; }

    public DateTime? ParsedForDSAt { get; set; }
  }
}
