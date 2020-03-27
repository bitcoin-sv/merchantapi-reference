package main

import (
	"flag"
	"fmt"
	"os"
	"time"

	"github.com/dgrijalva/jwt-go"
)

func main() {
	namePtr := flag.String("name", "", "name of fee file to use for this token. (Required)")
	daysPtr := flag.Int("days", 0, "Days the token will be valid for. (Required)")
	keyPtr := flag.String("key", "", "The key used for JWT tokens")

	flag.Parse()

	if *namePtr == "" {
		flag.PrintDefaults()
		os.Exit(1)
	}

	if *daysPtr == 0 {
		flag.PrintDefaults()
		os.Exit(2)
	}

	if *keyPtr == "" {
		flag.PrintDefaults()
		os.Exit(3)
	}

	expiry := time.Now().AddDate(0, 0, *daysPtr)

	signingKey := []byte(*keyPtr)

	token := jwt.NewWithClaims(jwt.SigningMethodHS256, jwt.MapClaims{
		"name": *namePtr,
		"exp":  expiry,
	})
	tokenString, err := token.SignedString(signingKey)
	if err != nil {
		fmt.Printf("Error creating token [%v]\n", err)
		os.Exit(4)
	}

	fmt.Printf("Fee filename=%q\nExpiry=%s\nToken = %s\n", fmt.Sprintf("fees_%s.json", *namePtr), expiry, tokenString)

}
