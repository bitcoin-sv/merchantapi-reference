package utils

// JSONEnvolope struct
type JSONEnvolope struct {
	Payload   string  `json:"payload"`
	Signature *string `json:"signature"` // Can be null
	PublicKey *string `json:"publicKey"` // Can be null
	Encoding  string  `json:"encoding"`
	MimeType  string  `json:"mimetype"`
}

type JsonError struct {
	Status int    `json:"status"`
	Code   int    `json:"code"`
	Err    string `json:"error"`
}

type FeeUnit struct {
	Satoshis int `json:"satoshis"` // Fee in satoshis of the amount of Bytes
	Bytes    int `json:"bytes"`    // Nuumber of bytes that the Fee covers
}

type Fee struct {
	FeeType   string  `json:"feeType"` // standard || data
	MiningFee FeeUnit `json:"miningFee"`
	RelayFee  FeeUnit `json:"relayFee"` // Fee for retaining Tx in secondary mempool
}

type FeeQuoteResponse struct {
	FeeQuote  FeeQuote `json:"FeeQuote"`
	Signature string   `json:"signature"`
}

type FeeQuote struct {
	APIVersion                string   `json:"apiVersion"` // Merchant API version NN.nn (major.minor version no.)
	Timestamp                 JsonTime `json:"timestamp"`  // Quote timeStamp
	ExpiryTime                JsonTime `json:"expiryTime"` // Quote expiry time
	MinerID                   *string  `json:"minerId"`    // Null indicates no minerID
	CurrentHighestBlockHash   string   `json:"currentHighestBlockHash"`
	CurrentHighestBlockHeight uint32   `json:"currentHighestBlockHeight"`
	MinerReputation           *string  `json:"minerReputation"` // Can be null
	Fees                      []Fee    `json:"fees"`
}

type TransactionJSON struct {
	RawTX string `json:"rawtx"`
}

type TransactionResponse struct {
	APIVersion                string   `json:"apiVersion"` // Merchant API version NN.nn (major.minor version no.)
	Timestamp                 JsonTime `json:"timestamp"`
	TxID                      string   `json:"txid"`              // Transaction ID assigned when submitted to mempool
	ReturnResult              string   `json:"returnResult"`      // ReturnResult is defined below
	ResultDescription         string   `json:"resultDescription"` // Reason for failure (e.g. which policy failed and why)
	MinerID                   *string  `json:"minerId"`           // Null indicates no minerID
	CurrentHighestBlockHash   string   `json:"currentHighestBlockHash"`
	CurrentHighestBlockHeight uint32   `json:"currentHighestBlockHeight"`
	TxSecondMempoolExpiry     uint16   `json:"txSecondMempoolExpiry"` // Duration (minutes) Tx will be kept in secondary mempool
}

type TransactionStatus struct {
	APIVersion            string   `json:"apiVersion"`            // Merchant API version NN.nn (major.minor version no.)
	Timestamp             JsonTime `json:"timestamp"`             // Fee timeStamp
	ReturnResult          string   `json:"returnResult"`          // ReturnResult is defined below
	ResultDescription     string   `json:"resultDescription"`     // Reason for failure (e.g. which policy failed and why)
	BlockHash             *string  `json:"blockHash"`             // Block that includes this transaction
	BlockHeight           *uint32  `json:"blockHeight"`           // The block height
	Confirmations         uint32   `json:"confirmations"`         // 0 if not yet unconfirmed
	MinerID               *string  `json:"minerId"`               // Null indicates no minerID
	TxSecondMempoolExpiry uint16   `json:"txSecondMempoolExpiry"` // Duration (minutes) Tx will be kept in secondary mempool
}
