package blockchaintracker

import (
	"encoding/json"
	"errors"
	"sort"
	"time"

	"github.com/bitcoin-sv/merchantapi-reference/multiplexer"
)

// BlockInfo stores the block info cache to avoid making calls
// to the BitCoin node RPC every time the data is requested
type BlockInfo struct {
	Timestamp                 time.Time
	CurrentHighestBlockHash   string `json:"bestblockhash"`
	CurrentHighestBlockHeight uint32 `json:"blocks"`
}

func (bct *Tracker) setLatestBlockInfo() error {
	mp := multiplexer.New("getblockchaininfo", nil)
	results := mp.Invoke(false, true)

	// If the count of remaining responses == 0, return an error
	if len(results) == 0 {
		return errors.New("No results from bitcoin multiplexer")
	}

	var blockInfos []*BlockInfo
	now := time.Now()
	for _, result := range results {
		var bi BlockInfo

		err := json.Unmarshal(result, &bi)
		if err != nil {
			continue
		}

		bi.Timestamp = now

		blockInfos = append(blockInfos, &bi)
	}

	// If the count of remaining responses == 0, return an error
	if len(blockInfos) == 0 {
		return errors.New("No results from bitcoin multiplexer")
	}

	// Sort the results with the lowest block height first
	sort.SliceStable(blockInfos, func(p, q int) bool {
		return blockInfos[p].CurrentHighestBlockHeight < blockInfos[q].CurrentHighestBlockHeight
	})

	bct.mu.Lock()
	bct.latestBlockInfo = blockInfos[0]
	bct.mu.Unlock()

	return nil
}

// GetLastKnownBlockInfo returns latest block info
func (bct *Tracker) GetLastKnownBlockInfo() *BlockInfo {
	bct.mu.RLock()
	defer bct.mu.RUnlock()

	return bct.latestBlockInfo
}
