# Merchant API

## [BRFC Spec](https://bitbucket.org/nchteamnch/merchantapi/src/master/)

More details available in the BRFC spec for merchant API

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
