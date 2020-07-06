package handler

import (
	"encoding/json"
	"errors"
	"fmt"
	"io/ioutil"
	"net/http"
	"strings"
	"time"

	"github.com/bitcoin-sv/merchantapi-reference/multiplexer"
	"github.com/bitcoin-sv/merchantapi-reference/utils"
	"github.com/ordishs/go-bitcoin"
)

// MultiTransactionStatus comment
func MultiTransactionStatus(w http.ResponseWriter, r *http.Request) {
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
		sendError(w, http.StatusBadRequest, 51, errors.New("Content-Type must be 'application/json' or 'application/octet-stream'"))
		return
	}

	reqBody, err := ioutil.ReadAll(r.Body)
	if err != nil {
		sendError(w, http.StatusBadRequest, 52, err)
		return
	}

	var txids []string

	if mimetype != "application/json" {
		sendError(w, http.StatusBadRequest, 53, err)
		return
	}

	if err := json.Unmarshal(reqBody, &txids); err != nil {
		sendError(w, http.StatusBadRequest, 54, err)
		return
	}

	if len(txids) == 0 {
		sendError(w, http.StatusBadRequest, 55, fmt.Errorf("must send at least 1 txid"))
		return
	}

	var txidInfo []utils.TxQueryData
	var failureCount uint32

	for _, txid := range txids {
		mp := multiplexer.New("getrawtransaction", []interface{}{txid, 1})
		results := mp.Invoke(true, true)

		var txData utils.TxQueryData

		if len(results) == 0 {
			txData = utils.TxQueryData{
				TxID:              txid,
				ReturnResult:      "failure",
				ResultDescription: "No results from bitcoin multiplexer",
			}
			failureCount++

		} else if len(results) == 1 {
			result := string(results[0])
			if strings.HasPrefix(result, "ERROR:") {
				txData = utils.TxQueryData{
					TxID:              txid,
					ReturnResult:      "failure",
					ResultDescription: result,
				}
				failureCount++

			} else {
				var bt bitcoin.RawTransaction
				json.Unmarshal(results[0], &bt)

				blockHeight := uint32(bt.BlockHeight)

				txData = utils.TxQueryData{
					TxID:          txid,
					ReturnResult:  "success",
					BlockHash:     &bt.BlockHash,
					BlockHeight:   &blockHeight,
					Confirmations: bt.Confirmations,
				}
			}

		} else {
			txData = utils.TxQueryData{
				TxID:              txid,
				ReturnResult:      "failure",
				ResultDescription: "Mixed results",
			}
			failureCount++
		}

		txidInfo = append(txidInfo, txData)
	}

	minerID := getPublicKey()

	multiTxStatus := &utils.MultiTransactionStatusResponse{
		APIVersion:   APIVersion,
		Timestamp:    utils.JsonTime(time.Now().UTC()),
		MinerID:      minerID,
		Txs:          txidInfo,
		FailureCount: failureCount,
	}

	sendEnvelope(w, multiTxStatus, minerID)
}
