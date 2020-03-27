package handler

import (
	"testing"
)

func TestGetFees(t *testing.T) {
	_, err := getFees("../fees.json")
	if err != nil {
		t.Error(err)
	}
}
