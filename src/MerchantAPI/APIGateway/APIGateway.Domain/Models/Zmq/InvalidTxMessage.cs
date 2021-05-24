// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Domain.Models.Zmq
{
	/// <summary>
	/// Payload of ZMQ message that is sent by node when an invalid transaction is detected
	/// </summary>
  public class InvalidTxMessage
  {
		[JsonPropertyName("fromblock")]
		public bool FromBlock { get; set; }

		[JsonPropertyName("source")]
    public string Source { get; set; }

		[JsonPropertyName("address")]
		public string Address { get; set; }

		[JsonPropertyName("nodeId")]
		public int NodeId { get; set; }

		[JsonPropertyName("txid")]
		public string TxId { get; set; }

		[JsonPropertyName("size")]
		public long Size { get; set; }

		[JsonPropertyName("hex")]
		public string Hex { get; set; }

		[JsonPropertyName("isInvalid")]
		public bool IsInvalid { get; set; }

		[JsonPropertyName("isValidationError")]
		public bool IsValidationError { get; set; }

		[JsonPropertyName("isMissingInputs")]
		public bool IsMissingInputs { get; set; }

		[JsonPropertyName("isDoubleSpendDetected")]
		public bool IsDoubleSpendDetected { get; set; }

		[JsonPropertyName("isMempoolConflictDetected")]
		public bool IsMempoolConflictDetected { get; set; }

		[JsonPropertyName("isCorruptionPossible")]
		public bool IsCorruptionPossible { get; set; }

		[JsonPropertyName("isNonFinal")]
		public bool IsNonFinal { get; set; }

		[JsonPropertyName("isValidationTimeoutExceeded")]
		public bool IsValidationTimeoutExceeded { get; set; }

		[JsonPropertyName("isStandardTx")]
		public bool IsStandardTx { get; set; }

		[JsonPropertyName("rejectionCode")]
		public int RejectionCode { get; set; }

		[JsonPropertyName("rejectionReason")]
		public string RejectionReason { get; set; }

		[JsonPropertyName("collidedWith")]
    public CollisionTx[] CollidedWith { get; set; }

    [Serializable]
    public class CollisionTx
		{
			[JsonPropertyName("txid")]
			public string TxId { get; set; }

			[JsonPropertyName("size")]
			public long Size { get; set; }

			[JsonPropertyName("hex")]
			public string Hex { get; set; }
		}

		[JsonPropertyName("rejectionTime")]
		public DateTime RejectionTime { get; set; }
	}
}
