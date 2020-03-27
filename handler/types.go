package handler

// JSONEnvolope struct
type JSONEnvolope struct {
	Payload   string  `json:"payload"`
	Signature *string `json:"signature"` // Can be null
	PublicKey *string `json:"publicKey"` // Can be null
	Encoding  string  `json:"encoding"`
	MimeType  string  `json:"mimetype"`
}

type jsonError struct {
	Status int    `json:"status"`
	Code   int    `json:"code"`
	Err    string `json:"error"`
}

type feeUnit struct {
	Satoshis int `json:"satoshis"` // Fee in satoshis of the amount of Bytes
	Bytes    int `json:"bytes"`    // Nuumber of bytes that the fee covers
}

type fee struct {
	FeeType   string  `json:"feeType"` // standard || data
	MiningFee feeUnit `json:"miningFee"`
	RelayFee  feeUnit `json:"relayFee"` // Fee for retaining Tx in secondary mempool
}

type feeQuoteResponse struct {
	FeeQuote  feeQuote `json:"feeQuote"`
	Signature string   `json:"signature"`
}

type feeQuote struct {
	APIVersion                string   `json:"apiVersion"` // Merchant API version NN.nn (major.minor version no.)
	Timestamp                 jsonTime `json:"timestamp"`  // Quote timeStamp
	ExpiryTime                jsonTime `json:"expiryTime"` // Quote expiry time
	MinerID                   *string  `json:"minerId"`    // Null indicates no minerID
	CurrentHighestBlockHash   string   `json:"currentHighestBlockHash"`
	CurrentHighestBlockHeight uint32   `json:"currentHighestBlockHeight"`
	MinerReputation           *string  `json:"minerReputation"` // Can be null
	Fees                      []fee    `json:"fees"`
}

type transactionJSON struct {
	RawTX string `json:"rawtx"`
}

type transactionResponse struct {
	APIVersion                string   `json:"apiVersion"` // Merchant API version NN.nn (major.minor version no.)
	Timestamp                 jsonTime `json:"timestamp"`
	TxID                      string   `json:"txid"`              // Transaction ID assigned when submitted to mempool
	ReturnResult              string   `json:"returnResult"`      // ReturnResult is defined below
	ResultDescription         string   `json:"resultDescription"` // Reason for failure (e.g. which policy failed and why)
	MinerID                   *string  `json:"minerId"`           // Null indicates no minerID
	CurrentHighestBlockHash   string   `json:"currentHighestBlockHash"`
	CurrentHighestBlockHeight uint32   `json:"currentHighestBlockHeight"`
	TxSecondMempoolExpiry     uint16   `json:"txSecondMempoolExpiry"` // Duration (minutes) Tx will be kept in secondary mempool
}

type transactionStatus struct {
	APIVersion            string   `json:"apiVersion"`            // Merchant API version NN.nn (major.minor version no.)
	Timestamp             jsonTime `json:"timestamp"`             // Fee timeStamp
	ReturnResult          string   `json:"returnResult"`          // ReturnResult is defined below
	ResultDescription     string   `json:"resultDescription"`     // Reason for failure (e.g. which policy failed and why)
	BlockHash             *string  `json:"blockHash"`             // Block that includes this transaction
	BlockHeight           *uint32  `json:"blockHeight"`           // The block height
	Confirmations         uint32   `json:"confirmations"`         // 0 if not yet unconfirmed
	MinerID               *string  `json:"minerId"`               // Null indicates no minerID
	TxSecondMempoolExpiry uint16   `json:"txSecondMempoolExpiry"` // Duration (minutes) Tx will be kept in secondary mempool
}
