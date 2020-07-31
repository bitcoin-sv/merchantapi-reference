package main

import (
	"log"
	"os"
	"os/signal"
	"sync"

	"github.com/bitcoin-sv/merchantapi-reference/handler"
)

// Name used by build script for the binaries. (Please keep on single line)
const progname = "merchant_api"

// Version & commit strings injected at build with -ldflags -X...
var version string
var commit string

func main() {
	log.Printf("VERSION\n-------\n%s (%s)\n\n", version, commit)

	// setup signal catching
	signalChan := make(chan os.Signal, 1)

	signal.Notify(signalChan, os.Interrupt)

	go func() {
		<-signalChan

		appCleanup()
		os.Exit(1)
	}()

	start()
}

func appCleanup() {
	log.Printf("INFO: Shutting dowm...")
}

func start() {
	var wg sync.WaitGroup
	var listenerCount = 0

	listenerCount += handler.StartServer(&wg, progname)

	// Keep server running by waiting for a channel that will never receive anything...
	wg.Wait()

	if listenerCount == 0 {
		log.Printf("WARN: Process terminated because no listeners were defined")
	}
}
