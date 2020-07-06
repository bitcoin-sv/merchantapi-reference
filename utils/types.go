package utils

// JSONEnvolope struct
// see https://github.com/bitcoin-sv-specs/brfc-misc/tree/master/jsonenvelope
type JSONEnvolope struct {
	Payload   string  `json:"payload"`
	Signature *string `json:"signature"` // Can be null
	PublicKey *string `json:"publicKey"` // Can be null
	Encoding  string  `json:"encoding"`
	MimeType  string  `json:"mimetype"`
}

// JSONError is the structure in which
// an error is returned to the caller
type JSONError struct {
	Status int    `json:"status"`
	Code   int    `json:"code"`
	Err    string `json:"error"`
}

// FeeUnit displays the amount of Satoshis needed
// for a specific amount of Bytes in a transaction
// see https://github.com/bitcoin-sv-specs/brfc-misc/tree/master/feespec
type FeeUnit struct {
	Satoshis int `json:"satoshis"` // Fee in satoshis of the amount of Bytes
	Bytes    int `json:"bytes"`    // Nuumber of bytes that the Fee covers
}

// Fee displays the MiningFee as well as the RelayFee for a specific
// FeeType, for example 'standard' or 'data'
// see https://github.com/bitcoin-sv-specs/brfc-misc/tree/master/feespec
type Fee struct {
	FeeType   string  `json:"feeType"` // standard || data
	MiningFee FeeUnit `json:"miningFee"`
	RelayFee  FeeUnit `json:"relayFee"` // Fee for retaining Tx in secondary mempool
}

// FeeQuote is the payload that is returned
// on a GET /mapi/feeQuote API call
// see https://github.com/bitcoin-sv-specs/brfc-merchantapi/tree/master#get-fee-quote
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

// TransactionJSON is the JSON structure sent
// to the Submit Transaction POST API call
// see https://github.com/bitcoin-sv-specs/brfc-merchantapi/tree/master#Submit-transaction
type TransactionJSON struct {
	RawTX string `json:"rawtx"`
}

// TransactionResponse is the payload that
// is returned on a POST /mapi/tx API call
// see https://github.com/bitcoin-sv-specs/brfc-merchantapi/tree/master#Submit-transaction
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

// TransactionStatus is the payload that is returned
// on a GET /mapi/tx/{hash:[0-9a-fA-F]+} API call
// see https://github.com/bitcoin-sv-specs/brfc-merchantapi/tree/master#Query-transaction-status
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

// MultiSubmitTransactionResponse is the payload that
// is returned on a POST /mapi/txs/submit API call
// see https://github.com/bitcoin-sv-specs/brfc-merchantapi/tree/master#Submit-multiple-transactions
type MultiSubmitTransactionResponse struct {
	APIVersion                string         `json:"apiVersion"` // Merchant API version NN.nn (major.minor version no.)
	Timestamp                 JsonTime       `json:"timestamp"`
	MinerID                   *string        `json:"minerId"` // Null indicates no minerID
	CurrentHighestBlockHash   string         `json:"currentHighestBlockHash"`
	CurrentHighestBlockHeight uint32         `json:"currentHighestBlockHeight"`
	TxSecondMempoolExpiry     uint16         `json:"txSecondMempoolExpiry"` // Duration (minutes) Tx will be kept in secondary mempool
	Txs                       []TxSubmitData `json:"txs"`                   // Transaction ID assigned when submitted to mempool
	FailureCount              uint32         `json:"failureCount"`
}

// TxSubmitData is the structure in which the transactions
// are shown in the MultiSubmitTransactionResponse
type TxSubmitData struct {
	TxID              string `json:"txid"`
	ReturnResult      string `json:"returnResult"`      // ReturnResult is defined below
	ResultDescription string `json:"resultDescription"` // Reason for failure (e.g. which policy failed and why)
}

// MultiTransactionStatusResponse is the payload that
// is returned on a POST /mapi/txs/submit API call
// see https://github.com/bitcoin-sv-specs/brfc-merchantapi/tree/master#Send-multi-transaction-status-query
type MultiTransactionStatusResponse struct {
	APIVersion            string        `json:"apiVersion"` // Merchant API version NN.nn (major.minor version no.)
	Timestamp             JsonTime      `json:"timestamp"`
	MinerID               *string       `json:"minerId"`               // Null indicates no minerID
	TxSecondMempoolExpiry uint16        `json:"txSecondMempoolExpiry"` // Duration (minutes) Tx will be kept in secondary mempool
	Txs                   []TxQueryData `json:"txs"`                   // Transaction ID assigned when submitted to mempool
	FailureCount          uint32        `json:"failureCount"`
}

// TxQueryData is the structure in which the transactions
// are shown in the MultiTransactionStatusResponse
type TxQueryData struct {
	TxID              string  `json:"txid"`
	ReturnResult      string  `json:"returnResult"`      // ReturnResult is defined below
	ResultDescription string  `json:"resultDescription"` // Reason for failure (e.g. which policy failed and why)
	BlockHash         *string `json:"blockHash"`         // Block that includes this transaction
	BlockHeight       *uint32 `json:"blockHeight"`       // The block height
	Confirmations     uint32  `json:"confirmations"`     // 0 if not yet unconfirmed
}
