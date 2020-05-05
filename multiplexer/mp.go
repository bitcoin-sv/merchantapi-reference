package multiplexer

import (
	"bytes"
	"encoding/json"
	"fmt"
	"log"
	"sync"

	"github.com/jadwahab/merchantapi-reference/config"
)

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

func contains(s []json.RawMessage, e json.RawMessage) bool {
	for _, a := range s {
		if bytes.Compare([]byte(a), []byte(e)) == 0 {
			return true
		}
	}
	return false
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
	responses := make([]json.RawMessage, 0)

	for i, client := range clients {
		wg.Add(1)

		go func(i int, client *rpcClient) {
			res, err := client.call(mp.Method, mp.Params)
			if err != nil {
				log.Printf("ERROR %s: %+v", client.serverAddr, err)
				if includeErrors {
					s := json.RawMessage("ERROR: " + err.Error())
					if !uniqueResults || !contains(responses, s) {
						responses = append(responses, s)
					}
				}
			} else if res.Err != nil {
				log.Printf("ERROR %s: %+v", client.serverAddr, err)
				if includeErrors {
					s := json.RawMessage("ERROR: " + res.Err.(string))
					if !uniqueResults || !contains(responses, s) {
						responses = append(responses, s)
					}
				}
			} else {
				s := res.Result
				if !uniqueResults || !contains(responses, s) {
					responses = append(responses, s)
				}
			}
			wg.Done()
		}(i, client)
	}

	wg.Wait()

	return responses
}
