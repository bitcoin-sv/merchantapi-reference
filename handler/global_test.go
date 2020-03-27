package handler

import (
	"crypto/sha256"
	"testing"
)

func TestSignMessage(t *testing.T) {
	message := []byte("Hello world")
	messageHash := sha256.Sum256(message)

	sig, err := signMessage(messageHash)
	if err != nil {
		t.Error(err)
	}

	pubKey := getPublicKey()

	ok, err := verifyMessage(messageHash, *pubKey, sig)
	if err != nil {
		t.Error(err)
	}

	if !ok {
		t.Error("Signature was invalid")
	}

}
