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
)

// MultiSubmitTransaction comment
func MultiSubmitTransaction(w http.ResponseWriter, r *http.Request) {
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
		sendError(w, http.StatusBadRequest, 41, errors.New("Content-Type must be 'application/json' or 'application/octet-stream'"))
		return
	}

	filename := "fees.json"
	if r.Header.Get("name") != "" {
		filename = fmt.Sprintf("fees_%s.json", r.Header.Get("name"))
	}

	fees, err := getFees(filename)
	if err != nil {
		sendError(w, http.StatusInternalServerError, 42, err)
		return
	}

	reqBody, err := ioutil.ReadAll(r.Body)
	if err != nil {
		sendError(w, http.StatusBadRequest, 43, err)
		return
	}

	var txs []string

	if mimetype != "application/json" {
		sendError(w, http.StatusBadRequest, 44, err)
		return
	}

	if err := json.Unmarshal(reqBody, &txs); err != nil {
		sendError(w, http.StatusBadRequest, 45, err)
		return
	}

	if len(txs) == 0 {
		sendError(w, http.StatusBadRequest, 46, fmt.Errorf("must send at least 1 tx"))
		return
	}

	blockInfo := bct.GetLastKnownBlockInfo()

	var txInfo []utils.TxSubmitData
	var failureCount uint32

	for _, tx := range txs {
		okToMine, okToRelay, err := checkFees(tx, fees)
		var txData utils.TxSubmitData

		if err != nil {
			txData = utils.TxSubmitData{
				ReturnResult:      "failure",
				ResultDescription: err.Error(),
			}
			failureCount++

		} else if !okToMine && !okToRelay {
			txData = utils.TxSubmitData{
				ReturnResult:      "failure",
				ResultDescription: "Not enough fees",
			}
			failureCount++

		} else {
			allowHighFees := false
			dontcheckfee := okToMine

			mp2 := multiplexer.New("sendrawtransaction", []interface{}{tx, allowHighFees, dontcheckfee})

			results2 := mp2.Invoke(true, true)

			if len(results2) == 0 {
				txData = utils.TxSubmitData{
					ReturnResult:      "failure",
					ResultDescription: "No results from bitcoin multiplexer",
				}
				failureCount++

			} else if len(results2) == 1 {
				result := string(results2[0])
				if strings.HasPrefix(result, "ERROR:") {
					txData = utils.TxSubmitData{
						ReturnResult:      "failure",
						ResultDescription: result,
					}
					failureCount++

				} else {
					txData = utils.TxSubmitData{
						TxID:         strings.Trim(result, "\""),
						ReturnResult: "success",
					}
				}

			} else {
				txData = utils.TxSubmitData{
					ReturnResult:      "failure",
					ResultDescription: "Mixed results",
				}
				failureCount++
			}
		}

		txInfo = append(txInfo, txData)
	}

	minerID := getPublicKey()

	multiTxStatus := &utils.MultiSubmitTransactionResponse{
		APIVersion:                APIVersion,
		Timestamp:                 utils.JsonTime(time.Now().UTC()),
		MinerID:                   minerID,
		CurrentHighestBlockHash:   blockInfo.CurrentHighestBlockHash,
		CurrentHighestBlockHeight: blockInfo.CurrentHighestBlockHeight,
		Txs:                       txInfo,
		FailureCount:              failureCount,
	}

	sendEnvelope(w, multiTxStatus, minerID)
}
