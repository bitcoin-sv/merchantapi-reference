package handler

import (
	"encoding/json"
	"errors"
	"fmt"
	"io/ioutil"
	"net/http"
	"sort"
	"time"

	"github.com/bitcoin-sv/merchantapi-reference/config"
	"github.com/bitcoin-sv/merchantapi-reference/multiplexer"
	"github.com/bitcoin-sv/merchantapi-reference/utils"
)

// GetFeeQuote comment
func GetFeeQuote(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Access-Control-Allow-Origin", "*")
	if r.Method == http.MethodOptions {
		return
	}

	filename := "fees.json"
	if r.Header.Get("name") != "" {
		filename = fmt.Sprintf("fees_%s.json", r.Header.Get("name"))
	}

	fees, err := getFees(filename)
	if err != nil {
		sendError(w, http.StatusInternalServerError, 11, err)
		return
	}

	mp := multiplexer.New("getblockchaininfo", nil)
	results := mp.Invoke(false, true)

	// If the count of remaining responses == 0, return an error
	if len(results) == 0 {
		sendError(w, http.StatusInternalServerError, 12, errors.New("No results from bitcoin multiplexer"))
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

	qem, ok := config.Config().GetInt("quoteExpiryMinutes")
	if !ok {
		sendError(w, http.StatusInternalServerError, 13, errors.New("No 'quoteExpiryMinutes' defined in settings.conf"))
		return
	}

	sendEnvelope(w, &utils.FeeQuote{
		APIVersion:                APIVersion,
		Timestamp:                 utils.JsonTime(now.UTC()),
		ExpiryTime:                utils.JsonTime(now.UTC().Add(time.Duration(qem) * time.Minute)),
		MinerID:                   minerID,
		CurrentHighestBlockHash:   m["bestblockhash"].(string),
		CurrentHighestBlockHeight: uint32(m["blocks"].(float64)),
		Fees:                      fees,
	}, minerID)
}

func getFees(filename string) ([]utils.Fee, error) {
	feesJSON, err := ioutil.ReadFile(filename)
	if err != nil {
		return nil, err
	}

	var fees []utils.Fee
	err = json.Unmarshal([]byte(feesJSON), &fees)
	if err != nil {
		return nil, err
	}

	return fees, nil
}
