package blockchaintracker

import (
	"fmt"
	"log"
	"sync"
	"time"

	"github.com/bitcoin-sv/merchantapi-reference/config"
	"github.com/ordishs/go-bitcoin"
)

// Tracker is a struct which holds the latest block information,
// namely the CurrentHighestBlockHash and CurrentHighestBlockHeight
// along with their timestamp
type Tracker struct {
	mu              sync.RWMutex
	latestBlockInfo *BlockInfo
}

// Start the global blockchain tracker in order to track latest blockchain
// information (CurrentHighestBlockHash and CurrentHighestBlockHeight). This
// is done by listening on the ZMQ of the BitCoin nodes connected to as well
// as through a ticker that get triggered based on the 'bitcoin_tracker_interval'
// in the settings.conf file
func Start() (*Tracker, error) {
	bct := &Tracker{}

	err := bct.setLatestBlockInfo()
	if err != nil {
		return nil, err
	}

	btiStr, _ := config.Config().Get("bitcoin_tracker_interval", "1m")
	bti, err := time.ParseDuration(btiStr)
	if err != nil {
		return nil, err
	}

	go func() {

		ch := make(chan []string, 10) //TODO: number of nodes (double # of nodes?)
		var connected bool

		count, _ := config.Config().GetInt("bitcoin_count")

		for i := 0; i < count; i++ {
			host, _ := config.Config().Get(fmt.Sprintf("bitcoin_%d_host", i+1))
			zmqPort, _ := config.Config().GetInt(fmt.Sprintf("bitcoin_%d_zmqport", i+1))

			zmq := bitcoin.NewZMQ(host, zmqPort)

			err := zmq.Subscribe("hashblock", ch)
			if err == nil {
				connected = true
			}
		}

		if connected == false {
			log.Println("no ZMQ listeners connected")
		}

		t := time.NewTicker(bti)
		for {
			select {
			case <-ch:
				// consume all items on channel (to avoid duplicates)
				for len(ch) > 0 {
					<-ch
				}
				bct.setLatestBlockInfo()

			case <-t.C:
				bct.setLatestBlockInfo()
			}
		}

	}()

	return bct, nil
}
