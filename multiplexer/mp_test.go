package multiplexer

import (
	"encoding/json"
	"sort"
	"testing"
)

func Test(t *testing.T) {
	mp := New("decoderawtransaction", []interface{}{"00000000"})

	responses := mp.Invoke(true, true)

	for _, response := range responses {
		t.Logf("%+v", string(response))
	}
}

func TestGetBlockchainInfo(t *testing.T) {
	mp := New("getblockchaininfo", nil)

	results := mp.Invoke(false, true)
	for _, response := range results {
		t.Logf("%+v", string(response))
	}

	sort.SliceStable(results, func(p, q int) bool {
		var m map[string]interface{}
		json.Unmarshal(results[p], &m)
		pBlock := int64(m["blocks"].(float64))
		json.Unmarshal(results[q], &m)
		qBlock := int64(m["blocks"].(float64))
		return pBlock < qBlock
	})

	for _, response := range results {
		var m map[string]interface{}
		json.Unmarshal(response, &m)
		t.Logf("%d", int64(m["blocks"].(float64)))
	}
}
