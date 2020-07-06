package handler

import (
	"crypto/sha256"
	"crypto/tls"
	"crypto/x509"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"io"
	"io/ioutil"
	"log"
	"net/http"
	"strings"

	"github.com/bitcoin-sv/merchantapi-reference/config"
	"github.com/bitcoin-sv/merchantapi-reference/utils"

	"github.com/btcsuite/btcd/btcec"
)

// APIVersion should be changed when the public API changes.
const APIVersion = "0.1.0"

var (
	minerIDServerURL, _ = config.Config().Get("minerId_URL")
	alias, _            = config.Config().Get("minerId_alias")
)

// NotFound handler
func NotFound(w http.ResponseWriter, r *http.Request) {
	w.WriteHeader(http.StatusBadRequest)
	io.WriteString(w, "BAD\n")
}

func getJSON(URL string) (string, error) {

	client := &http.Client{}

	if strings.HasPrefix(URL, "https") {
		// Read the key pair to create certificate
		cert, err := tls.LoadX509KeyPair("client1.crt", "client1.key")
		if err != nil {
			return "", err
		}

		// Create a CA certificate pool and add cert.pem to it
		caCert, err := ioutil.ReadFile("ca.crt")
		if err != nil {
			return "", err
		}
		caCertPool := x509.NewCertPool()
		caCertPool.AppendCertsFromPEM(caCert)

		// Create a HTTPS client and supply the created CA pool and certificate
		client = &http.Client{
			Transport: &http.Transport{
				TLSClientConfig: &tls.Config{
					RootCAs:      caCertPool,
					Certificates: []tls.Certificate{cert},
				},
			},
		}
	}

	// Request /hello via the created HTTPS client over port 8443 via GET
	r, err := client.Get(URL)
	if err != nil {
		return "", err
	}

	// Read the response body
	defer r.Body.Close()
	body, err := ioutil.ReadAll(r.Body)
	if err != nil {
		return "", err
	}

	return string(body), nil
}

func sendEnvelope(w http.ResponseWriter, payload interface{}, minerID *string) {
	payloadJSON, err := json.Marshal(&payload)
	if err != nil {
		log.Printf("WARN: sendEnvelope: %+v", err)
		return
	}

	var signature *string

	if minerID != nil {
		hash := sha256.Sum256(payloadJSON)

		s, err := signMessage(hash)
		if err != nil {
			log.Printf("WARN: sendEnvelope: %+v", err)
			return
		}
		signature = &s
	}

	envelope := &utils.JSONEnvolope{
		Payload:   string(payloadJSON),
		Signature: signature,
		PublicKey: minerID,
		MimeType:  "application/json",
		Encoding:  "UTF-8",
	}

	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusOK)
	json.NewEncoder(w).Encode(envelope)
}

func sendError(w http.ResponseWriter, status int, code int, err error) {
	e := utils.JSONError{
		Status: status,
		Code:   code,
		Err:    err.Error(),
	}

	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	json.NewEncoder(w).Encode(e)
}

func getPublicKey() *string {
	if minerIDServerURL == "" {
		return nil
	}

	if alias == "" {
		return nil
	}

	URL := fmt.Sprintf("%s/%s", minerIDServerURL, alias)

	publicKey, err := getJSON(URL)
	if err != nil {
		log.Printf("WARN: %+v", err)
		return nil
	}

	if publicKey == "" {
		return nil
	}

	if publicKey[len(publicKey)-1] == '\n' {
		publicKey = publicKey[0 : len(publicKey)-1]
	}

	return &publicKey
}

// signMessage takes a message ([]byte), hashes it and signs the hash with the private key.
// The signature is returned in strict DER format.
func signMessage(hash [32]byte) (sig string, err error) {
	URL := fmt.Sprintf("%s/%s/sign/%x", minerIDServerURL, alias, hash)

	signature, err := getJSON(URL)
	if err != nil {
		return "", err
	}

	if signature[len(signature)-1] == '\n' {
		signature = signature[0 : len(signature)-1]
	}

	return signature, nil
}

// verifyMessage will take a message string, a public key string and a signature string
// (in strict DER format) and verify that the message was signed by the public key.
func verifyMessage(hash [32]byte, pubKeyStr string, sigStr string) (verified bool, err error) {
	sigBytes, err := hex.DecodeString(sigStr)
	if err != nil {
		return
	}

	sig, err := btcec.ParseDERSignature(sigBytes, btcec.S256())
	if err != nil {
		return
	}

	pubKeyBytes, err := hex.DecodeString(pubKeyStr)
	pubKey, err := btcec.ParsePubKey(pubKeyBytes, btcec.S256())
	if err != nil {
		return
	}

	verified = sig.Verify(hash[:], pubKey)
	return
}
