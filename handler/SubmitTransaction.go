package handler

import (
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"io/ioutil"
	"net/http"
	"strings"
	"time"

	"github.com/bitcoin-sv/merchantapi-reference/multiplexer"
	"github.com/bitcoin-sv/merchantapi-reference/utils"
	"github.com/libsv/libsv/transaction"
)

// SubmitTransaction comment
func SubmitTransaction(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Access-Control-Allow-Origin", "*")
	w.Header().Set("Access-Control-Allow-Headers", "X-Requested-With,Content-Type,Authorization")
	if r.Method == http.MethodOptions {
		return
	}

	mimetype := r.Header.Get("Content-Type")

	switch mimetype {
	case "application/json":
	case "application/octet-stream":
	default:
		sendError(w, http.StatusBadRequest, 21, errors.New("Content-Type must be 'application/json' or 'application/octet-stream'"))
		return
	}

	minerID := getPublicKey()

	filename := "fees.json"
	if r.Header.Get("name") != "" {
		filename = fmt.Sprintf("fees_%s.json", r.Header.Get("name"))
	}

	fees, err := getFees(filename)
	if err != nil {
		sendError(w, http.StatusInternalServerError, 22, err)
		return
	}

	reqBody, err := ioutil.ReadAll(r.Body)
	if err != nil {
		sendError(w, http.StatusBadRequest, 23, err)
		return
	}

	var rawTX string
	switch mimetype {
	case "application/json":
		var tx utils.TransactionJSON
		if err := json.Unmarshal(reqBody, &tx); err != nil {
			sendError(w, http.StatusBadRequest, 24, err)
			return
		}

		if tx.RawTX == "" {
			sendError(w, http.StatusBadRequest, 25, fmt.Errorf("rawtx must be provided"))
			return
		}

		rawTX = tx.RawTX

	case "application/octet-stream":
		rawTX = hex.EncodeToString(reqBody)
	}

	blockInfo := bct.GetLastKnownBlockInfo()

	okToMine, okToRelay, err := checkFees(rawTX, fees)
	if err != nil {
		sendError(w, http.StatusBadRequest, 27, err)
		return
	}

	if !okToMine && !okToRelay {
		sendEnvelope(w, &utils.TransactionResponse{
			ReturnResult:              "failure",
			ResultDescription:         "Not enough fees",
			Timestamp:                 utils.JsonTime(time.Now().UTC()),
			MinerID:                   minerID,
			CurrentHighestBlockHash:   blockInfo.CurrentHighestBlockHash,
			CurrentHighestBlockHeight: blockInfo.CurrentHighestBlockHeight,
			TxSecondMempoolExpiry:     0,
			APIVersion:                APIVersion,
			// DoubleSpendTXIDs:          []string{"N/A"},
		}, minerID)
		return
	}

	allowHighFees := false
	dontcheckfee := okToMine

	mp2 := multiplexer.New("sendrawtransaction", []interface{}{rawTX, allowHighFees, dontcheckfee})

	results2 := mp2.Invoke(true, true)

	if len(results2) == 0 {
		sendEnvelope(w, &utils.TransactionResponse{
			APIVersion:                APIVersion,
			Timestamp:                 utils.JsonTime(time.Now().UTC()),
			ReturnResult:              "failure",
			ResultDescription:         "No results from bitcoin multiplexer",
			MinerID:                   minerID,
			CurrentHighestBlockHash:   blockInfo.CurrentHighestBlockHash,
			CurrentHighestBlockHeight: blockInfo.CurrentHighestBlockHeight,
			TxSecondMempoolExpiry:     0,
		}, minerID)
	} else if len(results2) == 1 {
		result := string(results2[0])
		if strings.HasPrefix(result, "ERROR:") {
			sendEnvelope(w, &utils.TransactionResponse{
				APIVersion:                APIVersion,
				Timestamp:                 utils.JsonTime(time.Now().UTC()),
				ReturnResult:              "failure",
				ResultDescription:         result,
				MinerID:                   minerID,
				CurrentHighestBlockHash:   blockInfo.CurrentHighestBlockHash,
				CurrentHighestBlockHeight: blockInfo.CurrentHighestBlockHeight,
				TxSecondMempoolExpiry:     0,
			}, minerID)
		} else {
			sendEnvelope(w, &utils.TransactionResponse{
				APIVersion:                APIVersion,
				Timestamp:                 utils.JsonTime(time.Now().UTC()),
				TxID:                      strings.Trim(result, "\""),
				ReturnResult:              "success",
				MinerID:                   minerID,
				CurrentHighestBlockHash:   blockInfo.CurrentHighestBlockHash,
				CurrentHighestBlockHeight: blockInfo.CurrentHighestBlockHeight,
				TxSecondMempoolExpiry:     0,
			}, minerID)
		}
	} else {
		sendEnvelope(w, &utils.TransactionResponse{
			APIVersion:                APIVersion,
			Timestamp:                 utils.JsonTime(time.Now().UTC()),
			ReturnResult:              "failure",
			ResultDescription:         "Mixed results",
			MinerID:                   minerID,
			CurrentHighestBlockHash:   blockInfo.CurrentHighestBlockHash,
			CurrentHighestBlockHeight: blockInfo.CurrentHighestBlockHeight,
			TxSecondMempoolExpiry:     0,
		}, minerID)
	}
}

// checkFees will return 2 booleans: goodForMiningFee and goodForRelay
func checkFees(txHex string, fees []utils.Fee) (bool, bool, error) {
	bt, err := transaction.NewFromString(txHex)
	if err != nil {
		return false, false, err
	}

	var feeAmount int64

	// Lookup the value of each input by querying the bitcoin node...
	for index, in := range bt.GetInputs() {
		mp := multiplexer.New("getrawtransaction", []interface{}{in.PreviousTxID, 0})
		results := mp.Invoke(false, true)

		if len(results) == 0 {
			return false, false, errors.New("No previous transaction found")
		}

		var txHex string
		json.Unmarshal(results[0], &txHex)

		oldTx, err := transaction.NewFromString(txHex)
		if err != nil {
			return false, false, err
		}

		previousOutputs := oldTx.GetOutputs()

		// check previous output index is in range
		if len(previousOutputs) <= int(in.PreviousTxOutIndex) {
			return false, false, fmt.Errorf("Invalid previous tx index for input %d", index)
		}

		feeAmount += int64(previousOutputs[in.PreviousTxOutIndex].Satoshis)
	}

	// Subtract the value of each output as well as keeping track of OP_RETURN outputs...

	var dataBytes int64
	for _, out := range bt.GetOutputs() {
		feeAmount -= int64(out.Satoshis)

		if out.Satoshis == 0 && len(*out.LockingScript) > 0 && out.LockingScript.IsData() {
			dataBytes += int64(len(*out.LockingScript))
		}
	}

	normalBytes := int64(len(bt.ToBytes())) - dataBytes

	// Check mining fees....
	var feesRequired int64
	for _, fee := range fees {
		if fee.FeeType == "standard" {
			feesRequired += normalBytes * int64(fee.MiningFee.Satoshis) / int64(fee.MiningFee.Bytes)
		} else if fee.FeeType == "data" {
			feesRequired += dataBytes * int64(fee.MiningFee.Satoshis) / int64(fee.MiningFee.Bytes)
		}
	}

	miningOK := false
	if feeAmount >= feesRequired {
		miningOK = true
	}

	// Now check relay fees...
	feesRequired = 0
	for _, fee := range fees {
		if fee.FeeType == "standard" {
			feesRequired += normalBytes * int64(fee.RelayFee.Satoshis) / int64(fee.RelayFee.Bytes)
		} else if fee.FeeType == "data" {
			feesRequired += dataBytes * int64(fee.RelayFee.Satoshis) / int64(fee.RelayFee.Bytes)
		}
	}

	relayOK := false
	if feeAmount >= feesRequired {
		relayOK = true
	}

	return miningOK, relayOK, nil
}
