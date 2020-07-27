package multiplexer

import (
	"encoding/json"
	"errors"
	"sort"
)

// BlockInfo stores the block info cache
type BlockInfo struct {
	CurrentHighestBlockHash   string `json:"bestblockhash"`
	CurrentHighestBlockHeight uint32 `json:"blocks"`
}

func getNodesBlockInfo() (*BlockInfo, error) {
	mp := New("getblockchaininfo", nil)
	results := mp.Invoke(false, true)

	// If the count of remaining responses == 0, return an error
	if len(results) == 0 {
		return nil, errors.New("No results from bitcoin multiplexer")
	}

	var blockInfos []*BlockInfo
	for _, result := range results {
		var bi BlockInfo

		err := json.Unmarshal(result, &bi)
		if err != nil {
			continue
		}

		blockInfos = append(blockInfos, &bi)
	}

	// If the count of remaining responses == 0, return an error
	if len(blockInfos) == 0 {
		return nil, errors.New("No results from bitcoin multiplexer")
	}

	// Sort the results with the lowest block height first
	sort.SliceStable(blockInfos, func(p, q int) bool {
		return blockInfos[p].CurrentHighestBlockHeight < blockInfos[q].CurrentHighestBlockHeight
	})

	return blockInfos[0], nil
}

// GetBlockInfo returns block info
func GetBlockInfo() (*BlockInfo, error) {
	return getNodesBlockInfo()
}
