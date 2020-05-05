package handler

import (
	"encoding/json"
	"fmt"
	"net/http"
	"strings"
	"time"

	"github.com/jadwahab/merchantapi-reference/multiplexer"
	"github.com/jadwahab/merchantapi-reference/utils"

	"github.com/gorilla/mux"
	"github.com/ordishs/go-bitcoin"
)

// QueryTransactionStatus comment
func QueryTransactionStatus(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Access-Control-Allow-Origin", "*")
	if r.Method == http.MethodOptions {
		return
	}

	txid := mux.Vars(r)["id"]
	if txid == "" {
		sendError(w, http.StatusBadRequest, 31, fmt.Errorf("txid must be provided"))
		return
	}

	minerID := getPublicKey()

	mp := multiplexer.New("getrawtransaction", []interface{}{txid, 1})
	results := mp.Invoke(true, true)

	if len(results) == 0 {
		sendEnvelope(w, &utils.TransactionStatus{
			APIVersion:        APIVersion,
			Timestamp:         utils.JsonTime(time.Now().UTC()),
			ReturnResult:      "failure",
			ResultDescription: "No results from bitcoin multiplexer",
			MinerID:           minerID,
		}, minerID)

	} else if len(results) == 1 {
		result := string(results[0])
		if strings.HasPrefix(result, "ERROR:") {
			sendEnvelope(w, &utils.TransactionStatus{
				APIVersion:        APIVersion,
				Timestamp:         utils.JsonTime(time.Now().UTC()),
				ReturnResult:      "failure",
				ResultDescription: result,
				MinerID:           minerID,
			}, minerID)
		} else {
			var bt bitcoin.RawTransaction
			json.Unmarshal(results[0], &bt)

			blockHeight := uint32(bt.BlockHeight)

			sendEnvelope(w, &utils.TransactionStatus{
				APIVersion:    APIVersion,
				Timestamp:     utils.JsonTime(time.Now().UTC()),
				ReturnResult:  "success",
				BlockHash:     &bt.BlockHash,
				BlockHeight:   &blockHeight,
				MinerID:       minerID,
				Confirmations: bt.Confirmations,
			}, minerID)
		}
	} else {
		sendEnvelope(w, &utils.TransactionStatus{
			APIVersion:        APIVersion,
			Timestamp:         utils.JsonTime(time.Now().UTC()),
			ReturnResult:      "failure",
			ResultDescription: "Mixed results",
			MinerID:           minerID,
		}, minerID)
	}

}
