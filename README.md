# Merchant API

More details available in the [BRFC Spec](https://github.com/bitcoin-sv-specs/brfc-merchantapi) for merchant API.  

> Note: MAPI uses the [JSON envelopes BRFC](https://github.com/bitcoin-sv-specs/brfc-misc/tree/master/jsonenvelope) as well as the [Fee Spec BRFC](https://github.com/bitcoin-sv-specs/brfc-misc/tree/master/feespec).

## [Swagger UI](https://bitcoin-sv.github.io/merchantapi-reference)

## Support

For support and general discussion of both standards and reference implementations please join the following [telegram group](https://t.me/joinchat/JB6ZzktqwaiJX_5lzQpQIA).

## Requirements

For development, you will only need GoLang installed in your environement.

## Configuration

MAPI configuration relies on a [settings.conf](settings.conf) file for the main service configurations as well as one or more fees*.json files (ex. [fees.json](fees.json) for default fees, [fees_low.json](fees_low.json) for lower fees, fees_user1.json for user1, etc.) to specify feesto be charged.

> In order to sign responses, you will also need to run [MinerId](https://github.com/bitcoin-sv/minerid-reference) and provide the endpoint to MAPI in the settings configurations.

### settings.conf File

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

### fees*.json Files

Please see the [Fee Spec BRFC](https://github.com/bitcoin-sv-specs/brfc-misc/tree/master/feespec) for the fees JSON format.

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

## Docker

You can find the public Docker Hub repository for mAPI [here](https://hub.docker.com/r/bitcoinsv/mapi).

### Build Image

```console
$ docker build . -t mapi_reference:1.1.0
```

### Run Container

Example configuration:

```console
$ docker run -p 9004:9004 \
    -e httpAddress=:9004 \
    -e bitcoin_1_host=host.docker.internal \
    -e minerId_URL=http://host.docker.internal:9002/minerid \
    -e minerId_alias=testMiner \
    mapi_reference:1.1.0
```

Example running in daemon mode:

```console
$ docker run -p 9004:9004 \
    -e httpAddress=:9004 \
    -e bitcoin_1_host=host.docker.internal \
    -e minerId_URL=http://host.docker.internal:9002/minerid \
    -e minerId_alias=testMiner \
    --restart=always \
    -d mapi_reference:1.1.0
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

### 4. sendMultiTransaction

```
POST /mapi/txs
```

body:

```json
[
  {
    "rawtx": "[transaction_hex_string]"
  },
  {
    "rawtx": "[transaction_hex_string]"
  },
  {
    "rawtx": "[transaction_hex_string]"
  }
]
```

## Authorization/Authentication and Special Rates

Merchant API providers would likely want to offer special or discounted rates to specific customers. To do this they would need to add an extra layer to enable authorization/authentication. An example implementation would be to use JSON Web Tokens (JWT) issued to specific users. The users would then include that token in their HTTP header and as a result recieve lower fee rates.

If no token is used and the call is done anonymously, then the default rate is supplied. If a JWT token (issued by the merchant API provider) is used, then the caller will receive the corresponding fee rate. At the moment, for this version of the merchant API implementation, the token must be issued and sent to the customer manually.

### Authorization/Authentication Example

```console
$ curl -H "Authorization:Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOiIyMDIwLTEwLTE0VDExOjQ0OjA3LjEyOTAwOCswMTowMCIsIm5hbWUiOiJsb3cifQ.LV8kz02bwxZ21qgqCvmgWfbGZCtdSo9px47wQ3_6Zrk" localhost:9004/mapi/feeQuote
```

### JWT Token Manager

To create a token:

```console
$ go run token_manager/main.go -days 100 -name "low"
Fee filename="fees_low.json"
Expiry=2020-10-14 11:44:07.129008 +0100 BST
Token = eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOiIyMDIwLTEwLTE0VDExOjQ0OjA3LjEyOTAwOCswMTowMCIsIm5hbWUiOiJsb3cifQ.LV8kz02bwxZ21qgqCvmgWfbGZCtdSo9px47wQ3_6Zrk
```

Now anyone using this token will be offered the fees in the [fees_low.json](fees_low.json) file.
