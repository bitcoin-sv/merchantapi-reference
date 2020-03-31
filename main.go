package main

import (
	"crypto/tls"
	"crypto/x509"
	"io/ioutil"
	"log"
	"net/http"
	"os"
	"os/signal"
	"sync"

	"merchant_api/config"

	"github.com/gorilla/mux"

	"merchant_api/handler"
)

// Name used by build script for the binaries. (Please keep on single line)
const progname = "merchant_api"

// Version of the app to be incremented automatically build script (Please keep on single line)
const version = "0.1.0"

// Commit string injected at build with -ldflags -X...
var commit string

func main() {
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
	router := mux.NewRouter().StrictSlash(true)

	// IMPORTANT: you must specify an OPTIONS method matcher for the middleware to set CORS headers
	router.HandleFunc("/mapi/feeQuote", handler.AuthMiddleware(handler.GetFeeQuote)).Methods(http.MethodGet, http.MethodOptions)
	router.HandleFunc("/mapi/tx", handler.AuthMiddleware(handler.SubmitTransaction)).Methods(http.MethodPost, http.MethodOptions)
	router.HandleFunc("/mapi/tx/{id}", handler.AuthMiddleware(handler.QueryTransactionStatus)).Methods(http.MethodGet, http.MethodOptions)

	router.NotFoundHandler = http.HandlerFunc(handler.NotFound)

	router.Use(mux.CORSMethodMiddleware(router))

	var wg sync.WaitGroup
	var listenerCount = 0

	httpAddress, _ := config.Config().Get("httpAddress")
	if len(httpAddress) > 0 {
		wg.Add(1)
		listenerCount++

		go func(wg *sync.WaitGroup) {
			var err error

			server := &http.Server{
				Addr:    httpAddress,
				Handler: router,
			}

			log.Printf("INFO: HTTP server listening on %s", server.Addr)

			err = server.ListenAndServe()
			if err != nil {
				log.Printf("ERROR: HTTP server failed [%v]", err)
			}

			wg.Done()
		}(&wg)

	}

	httpsAddress, _ := config.Config().Get("httpsAddress")
	if len(httpsAddress) > 0 {
		wg.Add(1)
		defer wg.Done()
		listenerCount++

		go func(wg *sync.WaitGroup) {
			var err error

			certFile, _ := config.Config().Get("certFile", "../certificate_authority/ca.crt")
			keyFile, _ := config.Config().Get("keyFile", "../certificate_authority/ca.key")

			// Create a CA certificate pool and add ca.crt to it
			caCert, err := ioutil.ReadFile(certFile)
			if err != nil {
				log.Printf("ERROR: Could not start secure server [%v]", err)
				return
			}
			caCertPool := x509.NewCertPool()
			caCertPool.AppendCertsFromPEM(caCert)

			// Create the TLS Config with the CA pool and enable Client certificate validation
			tlsConfig := &tls.Config{
				ClientCAs:  caCertPool,
				ClientAuth: tls.NoClientCert,
			}
			tlsConfig.BuildNameToCertificate()

			server := &http.Server{
				Addr:      httpsAddress,
				TLSConfig: tlsConfig,
				Handler:   router,
			}

			log.Printf("INFO: HTTPS server listening on %s", server.Addr)

			// Listen to HTTPS connections with the server certificate and wait
			err = server.ListenAndServeTLS(certFile, keyFile)
			if err != nil {
				log.Printf("ERROR: HTTPS server failed [%v]", err)
			}

			wg.Done()
		}(&wg)
	}

	// Keep server running by waiting for a channel that will never receive anything...
	wg.Wait()

	if listenerCount == 0 {
		log.Printf("WARN: Process terminated because no listeners were defined")
	}
}
