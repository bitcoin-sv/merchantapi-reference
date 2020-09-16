package handler

import (
	"encoding/json"
	"errors"
	"fmt"
	"io/ioutil"
	"net/http"
	"time"

	"github.com/bitcoin-sv/merchantapi-reference/config"
	"github.com/bitcoin-sv/merchantapi-reference/utils"
)

// GetFeeQuote comment
func GetFeeQuote(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Access-Control-Allow-Origin", "*")
	w.Header().Set("Access-Control-Allow-Headers", "X-Requested-With,Content-Type,Authorization")
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

	blockInfo := bct.GetLastKnownBlockInfo()

	minerID := getPublicKey()
	now := time.Now()

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
		CurrentHighestBlockHash:   blockInfo.CurrentHighestBlockHash,
		CurrentHighestBlockHeight: blockInfo.CurrentHighestBlockHeight,
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
