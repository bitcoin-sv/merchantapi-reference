package handler

import (
	"net/http"
	"strings"

	"merchant_api/config"

	"github.com/dgrijalva/jwt-go"
)

var key, _ = config.Config().Get("jwtKey")

// AuthMiddleware handler
func AuthMiddleware(next http.HandlerFunc) http.HandlerFunc {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if key == "" {
			// JWT tokens are disabled altogether
			next.ServeHTTP(w, r)
			return
		}

		tokenString := r.Header.Get("Authorization")
		if len(tokenString) == 0 {
			// No Authorization header, so allow the request to continue anonymously
			next.ServeHTTP(w, r)
			return
		}

		tokenString = strings.Replace(tokenString, "Bearer ", "", 1)
		claims, err := verifyToken(tokenString)
		if err != nil {
			w.WriteHeader(http.StatusUnauthorized)
			w.Write([]byte("Error verifying JWT token: " + err.Error()))
			return
		}

		name := claims.(jwt.MapClaims)["name"].(string)

		r.Header.Set("name", name)

		next.ServeHTTP(w, r)
	})
}

func getToken(name string, expiry int64) (string, error) {
	signingKey := []byte(key)
	token := jwt.NewWithClaims(jwt.SigningMethodHS256, jwt.MapClaims{
		"name": name,
		"exp":  expiry,
	})
	tokenString, err := token.SignedString(signingKey)
	return tokenString, err
}

func verifyToken(tokenString string) (jwt.Claims, error) {
	signingKey := []byte(key)
	token, err := jwt.Parse(tokenString, func(token *jwt.Token) (interface{}, error) {
		return signingKey, nil
	})
	if err != nil {
		return nil, err
	}

	return token.Claims, err
}
