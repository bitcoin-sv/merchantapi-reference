package handler

import (
	"encoding/json"
	"errors"
	"fmt"
	"io/ioutil"
	"merchant_api/multiplexer"
	"net/http"
	"sort"
	"time"
)

// GetFeeQuote comment
func GetFeeQuote(w http.ResponseWriter, r *http.Request) {
	filename := "fees.json"
	if r.Header.Get("name") != "" {
		filename = fmt.Sprintf("fees_%s.json", r.Header.Get("name"))
	}

	fees, err := getFees(filename)
	if err != nil {
		sendError(w, http.StatusBadRequest, 21, err)
		return
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

	minerID := getPublicKey()
	now := time.Now()

	var m map[string]interface{}
	json.Unmarshal(results[0], &m)

	sendEnvelope(w, &feeQuote{
		APIVersion:                APIVersion,
		Timestamp:                 jsonTime(now.UTC()),
		ExpiryTime:                jsonTime(now.UTC().Add(time.Duration(10) * time.Minute)), // 10 minute expiry TODO: change hadcoded expiry
		MinerID:                   minerID,
		CurrentHighestBlockHash:   m["bestblockhash"].(string),
		CurrentHighestBlockHeight: uint32(m["blocks"].(float64)),
		Fees:                      fees,
		// MinerReputation:           "N/A", // TODO:
	}, minerID)
}

func getFees(filename string) ([]fee, error) {
	feesJSON, err := ioutil.ReadFile(filename) // TODO: change hardcoded fees
	if err != nil {
		return nil, err
	}

	var fees []fee
	err = json.Unmarshal([]byte(feesJSON), &fees)
	if err != nil {
		return nil, err
	}

	return fees, nil
}
