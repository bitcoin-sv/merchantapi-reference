package multiplexer

import (
	"bytes"
	"encoding/json"
	"fmt"
	"log"
	"sync"

	"merchant_api/config"
)

type rawMessages struct {
	mu       sync.RWMutex
	messages []json.RawMessage
}

func newRawMessages() *rawMessages {
	return &rawMessages{
		messages: make([]json.RawMessage, 0),
	}
}

func (rm *rawMessages) addIfNecessary(unique bool, m json.RawMessage) {
	rm.mu.Lock()
	defer rm.mu.Unlock()

	// If we only want unique mesages, check if m already exists.
	if unique {
		for _, message := range rm.messages {
			if bytes.Compare([]byte(message), []byte(m)) == 0 {
				return
			}
		}
	}

	// If we reach here, we need to add the message
	rm.messages = append(rm.messages, m)
}

// MPWrapper type
type MPWrapper struct {
	Method  string
	Params  interface{}
	ID      int64
	Version string
}

var clients []*rpcClient

func init() {
	count, _ := config.Config().GetInt("bitcoin_count")
	clients = make([]*rpcClient, count)

	for i := 0; i < count; i++ {
		host, _ := config.Config().Get(fmt.Sprintf("bitcoin_%d_host", i+1))
		port, _ := config.Config().GetInt(fmt.Sprintf("bitcoin_%d_port", i+1))
		username, _ := config.Config().Get(fmt.Sprintf("bitcoin_%d_username", i+1))
		password, _ := config.Config().Get(fmt.Sprintf("bitcoin_%d_password", i+1))

		clients[i], _ = newClient(host, port, username, password)
	}
}

// New function
func New(method string, params interface{}) *MPWrapper {
	return &MPWrapper{
		Method: method,
		Params: params,
	}
}

// Invoke function
func (mp *MPWrapper) Invoke(includeErrors bool, uniqueResults bool) []json.RawMessage {
	var wg sync.WaitGroup
	responses := newRawMessages()

	for i, client := range clients {
		wg.Add(1)

		go func(i int, client *rpcClient) {
			res, err := client.call(mp.Method, mp.Params)
			if err != nil {
				log.Printf("ERROR %s: %+v", client.serverAddr, err)
				if includeErrors {
					s := json.RawMessage("ERROR: " + err.Error())
					responses.addIfNecessary(uniqueResults, s)
				}
			} else if res.Err != nil {
				log.Printf("ERROR %s: %+v", client.serverAddr, err)
				if includeErrors {
					s := json.RawMessage("ERROR: " + res.Err.(string))
					responses.addIfNecessary(uniqueResults, s)
				}
			} else {
				responses.addIfNecessary(uniqueResults, res.Result)
			}

			wg.Done()
		}(i, client)
	}

	wg.Wait()

	return responses.messages
}
