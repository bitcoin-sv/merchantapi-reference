# Merchant API

More details available in the [BRFC Spec](https://github.com/bitcoin-sv-specs/brfc-merchantapi) for merchant API.  

### [Swagger UI](https://bitcoin-sv.github.io/merchantapi-reference) 

## Requirements

For development, you will only need GoLang installed in your environement.

## Configuration

Open [settings.conf](settings.conf) and edit it with your settings:  

- change `httpAddress` or `httpsAddress` to bind on specific interfaces
- change `jwtKey` for tokens
  Generate new JWT key:
  ```console
  $ node -e "console.log(require('crypto').randomBytes(32).toString('hex'));"
  ```
- change `quoteExpiryMinutes` to set feeQuote expiry time
- change count of bitcoin nodes that merchant API is connected to as well as their respective Bitcoin RPC parameters:
  - `bitcoin_count`
  - `bitcoin_1_host`
  - `bitcoin_1_port`
  - `bitcoin_1_username`
  - `bitcoin_1_password`

- change `minerId_URL` and `minerId_alias` to set URL alias of minerId

## Run

```console
$ ./run.sh
```

## Build

```console
$ ./build.sh
```

## Test
Run individual tests or run all tests with:

```console
$ go test ./...
```

## Implementation

The **REST API** has 3 endpoints:

### 1. getFeeQuote

```
GET /mapi/feeQuote
```

### 2. submitTransaction

```
POST /mapi/tx
```

body:

```json
{
  "rawtx": "[transaction_hex_string]"
}
```

### 3. queryTransactionStatus

```
GET /mapi/tx/{hash:[0-9a-fA-F]+}
```

## Authorization/Authentication and Special Rates

Merchant API providers would likely want to offer special or discounted rates to specific customers. To do this they would need to add an extra layer to enable authorization/authentication. An example implementation would be to use JSON Web Tokens (JWT) issued to specific users. The users would then include that token in their HTTP header and as a result recieve lower fee rates.

If no token is used and the call is done anonymously, then the default rate is supplied. If a JWT token (issued by the merchant API provider) is used, then the caller will receive the corresponding fee rate. At the moment, for this version of the merchant API implementation, the token must be issued and sent to the customer manually.

### Authorization/Authentication Example

```
curl -H "Authorization:Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjE1ODY0ODAwMTEsIm5hbWUiOiJzaW1vbiJ9.eCeTF-8DwPY-FgbIKu-88oSQTDS5GJKj_L3SvIjqNy8" localhost:9004/mapi/feeQuote
```
