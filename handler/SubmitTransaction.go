package handler

import (
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"io/ioutil"
	"merchant_api/multiplexer"
	"net/http"
	"sort"
	"strings"
	"time"

	"bitbucket.org/simon_ordish/cryptolib/transaction"
)

// SubmitTransaction comment
func SubmitTransaction(w http.ResponseWriter, r *http.Request) {
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
		sendError(w, http.StatusBadRequest, 21, err)
		return
	}

	reqBody, err := ioutil.ReadAll(r.Body)
	if err != nil {
		sendError(w, http.StatusBadRequest, 21, err)
		return
	}

	var rawTX string
	switch mimetype {
	case "application/json":
		var tx transactionJSON
		if err := json.Unmarshal(reqBody, &tx); err != nil {
			sendError(w, http.StatusBadRequest, 21, err)
			return
		}

		if tx.RawTX == "" {
			sendError(w, http.StatusBadRequest, 21, fmt.Errorf("Transaction hex must be provided"))
			return
		}

		rawTX = tx.RawTX

	case "application/octet-stream":
		rawTX = hex.EncodeToString(reqBody)
	}

	mp := multiplexer.New("getblockchaininfo", nil)
	results := mp.Invoke(false, true)

	// If the count of remaining responses == 0, return an error
	if len(results) == 0 {
		sendError(w, http.StatusInternalServerError, 21, errors.New("No results from bitcoin multiplexer'"))
		return
	}

	// Sort the results with the lowest block height first
	sort.SliceStable(results, func(p, q int) bool {
		var m map[string]interface{}
		json.Unmarshal(results[p], &m)
		pBlock := int64(m["blocks"].(float64))
		json.Unmarshal(results[q], &m)
		qBlock := int64(m["blocks"].(float64))
		return pBlock < qBlock
	})

	now := time.Now()

	var m map[string]interface{}
	json.Unmarshal(results[0], &m)

	okToMine, okToRelay, err := checkFees(rawTX, fees)
	if err != nil {
		sendError(w, http.StatusBadRequest, 21, err)
		return
	}

	if !okToMine && !okToRelay {
		sendEnvelope(w, &transactionResponse{
			ReturnResult:              "failure",
			ResultDescription:         "Not enough fees",
			Timestamp:                 jsonTime(now.UTC()),
			MinerID:                   minerID,
			CurrentHighestBlockHash:   m["bestblockhash"].(string),
			CurrentHighestBlockHeight: uint32(m["blocks"].(float64)),
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
		sendEnvelope(w, &transactionResponse{
			APIVersion:                APIVersion,
			Timestamp:                 jsonTime(now.UTC()),
			ReturnResult:              "failure",
			ResultDescription:         "No results from bitcoin multiplexer",
			MinerID:                   minerID,
			CurrentHighestBlockHash:   m["bestblockhash"].(string),
			CurrentHighestBlockHeight: uint32(m["blocks"].(float64)),
			TxSecondMempoolExpiry:     0,
		}, minerID)
	} else if len(results2) == 1 {
		result := string(results2[0])
		if strings.HasPrefix(result, "ERROR:") {
			sendEnvelope(w, &transactionResponse{
				APIVersion:                APIVersion,
				Timestamp:                 jsonTime(time.Now().UTC()),
				ReturnResult:              "failure",
				ResultDescription:         result,
				MinerID:                   minerID,
				CurrentHighestBlockHash:   m["bestblockhash"].(string),
				CurrentHighestBlockHeight: uint32(m["blocks"].(float64)),
				TxSecondMempoolExpiry:     0,
			}, minerID)
		} else {
			sendEnvelope(w, &transactionResponse{
				APIVersion:                APIVersion,
				Timestamp:                 jsonTime(time.Now().UTC()),
				TxID:                      result,
				ReturnResult:              "success",
				MinerID:                   minerID,
				CurrentHighestBlockHash:   m["bestblockhash"].(string),
				CurrentHighestBlockHeight: uint32(m["blocks"].(float64)),
				TxSecondMempoolExpiry:     0,
			}, minerID)
		}
	} else {
		sendEnvelope(w, &transactionResponse{
			APIVersion:                APIVersion,
			Timestamp:                 jsonTime(time.Now().UTC()),
			TxID:                      "Mixed results",
			ReturnResult:              "failure",
			MinerID:                   minerID,
			CurrentHighestBlockHash:   m["bestblockhash"].(string),
			CurrentHighestBlockHeight: uint32(m["blocks"].(float64)),
			TxSecondMempoolExpiry:     0,
		}, minerID)
	}
}

// checkFees will return 2 booleans: goodForMiningFee and goodForRelay
func checkFees(txHex string, fees []fee) (bool, bool, error) {
	bt, err := transaction.NewFromString(txHex)
	if err != nil {
		return false, false, err
	}

	var feeAmount int64

	// Lookup the value of each input by querying the bitcoin node...
	for _, in := range bt.GetInputs() {
		mp := multiplexer.New("getrawtransaction", []interface{}{hex.EncodeToString(in.PreviousTxHash[:]), 0})
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

		feeAmount += int64(oldTx.GetOutputs()[in.PreviousTxOutIndex].Value)
	}

	// Subtract the value of each output as well as keeping track of OP_RETURN outputs...

	var dataBytes int64
	for _, out := range bt.GetOutputs() {
		feeAmount -= int64(out.Value)

		if out.Value == 0 && len(out.Script) > 0 && (out.Script[0] == 0x6a || (out.Script[0] == 0x00 && out.Script[1] == 0x6a)) {
			dataBytes += int64(len(out.Script))
		}
	}

	normalBytes := int64(len(bt.Hex())) - dataBytes

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
