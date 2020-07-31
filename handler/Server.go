package handler

import (
	"crypto/tls"
	"crypto/x509"
	"io/ioutil"
	"log"
	"net/http"
	"sync"

	"github.com/bitcoin-sv/merchantapi-reference/blockchaintracker"
	"github.com/bitcoin-sv/merchantapi-reference/config"
	"github.com/gorilla/mux"
)

func StartServer(wg *sync.WaitGroup, sName string) int {
	var err error
	bct, err = blockchaintracker.Start()
	if err != nil {
		log.Printf("blocktracker returned %v", err)
		return 0
	}

	listenerCount := 0

	router := mux.NewRouter().StrictSlash(true)

	// IMPORTANT: you must specify an OPTIONS method matcher for the middleware to set CORS Access-Control-Allow-Methods header.
	router.Use(mux.CORSMethodMiddleware(router))

	// Many try to “solve” CORS with a Preflight middleware that has a global CORS policy. This flies in the face of the purpose of CORS,
	// which is to protect Cross Origin Resource Sharing. The spirit of CORS lives in the capabilities of the individual resources.
	// This means you MUST have a resource aware CORS implementation in my opinion.  For that reason, the Access-Control-Allow-Origin and
	// Access-Control-Allow-Headers are set in each handler.
	router.HandleFunc("/mapi/feeQuote", AuthMiddleware(GetFeeQuote)).Methods(http.MethodGet, http.MethodOptions)
	router.HandleFunc("/mapi/tx", AuthMiddleware(SubmitTransaction)).Methods(http.MethodPost, http.MethodOptions)
	router.HandleFunc("/mapi/tx/{id}", AuthMiddleware(QueryTransactionStatus)).Methods(http.MethodGet, http.MethodOptions)
	router.HandleFunc("/mapi/txs/submit", AuthMiddleware(MultiSubmitTransaction)).Methods(http.MethodPost, http.MethodOptions)
	router.HandleFunc("/mapi/txs/status", AuthMiddleware(MultiTransactionStatus)).Methods(http.MethodPost, http.MethodOptions)

	router.NotFoundHandler = http.HandlerFunc(NotFound)

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
		}(wg)

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
		}(wg)
	}

	return listenerCount
}
