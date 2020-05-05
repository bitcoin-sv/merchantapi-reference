package handler

import (
	"encoding/json"
	"github.com/jadwahab/merchantapi-reference/multiplexer"
	"testing"
)

func TestCheckFees(t *testing.T) {
	tx := "0100000001621ab17ba9a8b190cff9afcdf43d6ccd5d99ffc707479bcb743bb0ae5622311c020000006b483045022100f7fc2088fefccb04480d4e1a59b8472bede8d5a881b5a4eb448fbfa82376f06502200991f7a08046e6f015a93e9666be5945b3a76b5891c29ca022b21d2d09e9b6d641210247e2d43bd2be4a9ccd307a4fa483f6aa14a82796a23344e8fa4e0cde8dfb4595ffffffff040000000000000000fc6a2231394878696756345179427633744870515663554551797131707a5a56646f4175744cbf7b22666f72627376223a22666f72627376222c2274797065223a226c696b655f636f6d6d656e74222c22737562446f6d61696e223a226c6f6e646f6e6f66756e697465646b696e67646f6d222c2263726561746564223a313538303230383039373330332c22757365724964223a2231303638222c22746f5478223a2236376332373431633135346633653861396266313939623933393939343961646362336438646638656661363738646262386132333265373339376236343839227d106170706c69636174696f6e2f6a736f6e055554462d386c060000000000001976a914de95223cdad76cc823080933d4b6ac0a6ff8c6a988ac6c060000000000001976a914d2063b5d1863e34174618a9f7b199dbfacebad2488ac07d01427000000001976a914d2063b5d1863e34174618a9f7b199dbfacebad2488ac00000000"

	fees, err := getFees("../fees_low.json")
	if err != nil {
		t.Error(err)
		t.FailNow()
	}

	miningOK, relayOK, err := checkFees(tx, fees)
	if err != nil {
		t.Error(err)
		t.FailNow()
	}

	if !miningOK {
		t.Error("Expect miningOK to be true")
	}

	if !relayOK {
		t.Error("Expect relayOK to be true")
	}
}

func TestCheckFeesLargeDataTx(t *testing.T) {
	tx := "c4337347c29fea52dea804e866492e832c69c44438bc7e5744fe8f45f23929c9"

	mp := multiplexer.New("getrawtransaction", []interface{}{tx, 0})
	results := mp.Invoke(false, true)

	var txHex string
	json.Unmarshal(results[0], &txHex)

	fees, err := getFees("../fees_low.json")
	if err != nil {
		t.Error(err)
		t.FailNow()
	}

	miningOK, relayOK, err := checkFees(txHex, fees)
	if err != nil {
		t.Error(err)
		t.FailNow()
	}

	if !miningOK {
		t.Error("Expect miningOK to be true")
	}

	if !relayOK {
		t.Error("Expect relayOK to be true")
	}
}
