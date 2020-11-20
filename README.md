# mAPI

More details available in the [BRFC Spec](https://github.com/bitcoin-sv-specs/brfc-merchantapi) for Merchant API.  

> The old golang (v1.1) implementation is no longer being maintained and has been moved to the [golang-v1.1 branch](https://github.com/bitcoin-sv/merchantapi-reference/tree/golang-v1.1).

## Swagger UI

The REST API can also be seen in the [Swagger UI](https://bitcoin-sv.github.io/merchantapi-reference).

## Support

For support and general discussion of both standards and reference implementations please join the following [telegram group](https://t.me/joinchat/JB6ZzktqwaiJX_5lzQpQIA).

## Requirements

mAPI requires access to Bitcoin SV node version 1.0.6 or newer. See [Managing nodes](#Managing-nodes) for details how to connect to a bitcoin node.

For running in production, you should should use Docker. Docker images are created as part of the [build](#build-and-deploy). See [Deploying docker images](#Deploying-docker-images) for details how to run them.

A SSL server certificate is required for installation. You can obtain the certificate from your IT support team. There are are also services that issue free SSL certificates such as letsencrypt.org.  The certificate must be issued for the host with fully qualified domain name. To use the server side certificate, you need to export it (including corresponding private key) it in PFX file format (*.pfx).

For setting up development environment see [bellow](#setting-up-a-development-environment)


## REST interface

The reference implementation exposes different **REST API** interfaces

* an interface for submitting transactions implemented according [BRFC Spec](https://github.com/bitcoin-sv-specs/brfc-merchantapi)
* an admin interface for managing connections to bitcoin nodes and fee quotes


## Public interface

Public interface can be used to submit transactions and query transactions status. It is accessible to both authenticated and unauthenticated users, but authenticated users might get special fee rates.

### 1. getFeeQuote

```
GET /mapi/feeQuote
```

### 2. submitTransaction

```
POST /mapi/tx
```

To submit a transaction in JSON format use `Content-Type: application/json` with the following request body:

```json
{
  "rawtx":        "[transaction_hex_string]",
  "callbackUrl":  "https://your.service.callback/endpoint",
  "callbackToken" : "Authorization: <your_authorization_header>",
  "merkleProof" : true,
  "dsCheck" : true
}
```

To submit transaction in binary format use `Content-Type: application/octet-stream` with the binary serialized transaction in the request body. You can specify `callbackUrl`, `callbackToken`, `merkleProof` and `dsCheck` in the query string.

If a double spend notification or merkle proof is requested in Submit transaction, the response is sent to the specified callbackURL. Where recipients are using [SPV Channels](https://github.com/bitcoin-sv/brfc-spvchannels), this would require the recipient to have a channel setup and ready to receive messages.
Check [Callback Notifications](#callback-notifications) for details.

### 3. queryTransactionStatus

```
GET /mapi/tx/{hash:[0-9a-fA-F]+}
```

### 4. sendMultiTransaction


```
POST /mapi/txs
```

To submit a list of transactions in JSON format use `Content-Type: application/json` with the following request body:

```json
[
  {

    "rawtx":        "[transaction_hex_string]",
    "callbackUrl":  "https://your.service.callback/endpoint",
    "callbackToken" : "Authorization: <your_authorization_header>",
    "merkleProof" : true,
    "dsCheck" : true
  },
  ....
]
```

You can also omit `callbackUrl`, `callbackToken`, `merkleProof` and `dsCheck` from the request body and provide the values in the query string.

To submit transaction in binary format use `Content-Type: application/octet-stream` with the binary serialized transactions in the request body. Use query string to specify the remaining parameters.


### Callback Notifications

Merchants can request callbacks for *merkle proofs* and/or *double spend notifications* in Submit transaction.

Double Spend example:
```
POST /mapi/tx
```

#### Request:

Request Body:

```json
{
    "rawtx": "01000000015d7d8ffefc2b95a68a95d8e3c50715f8affc0e56ef58a05c773789e6fa3eb537010000006a47304402206c1ba36989bdca944c4ac1e74c23afaaf93fb6ded3a3d6e01f2c28667373c26e0220676085f6fe30071022ea5c8e790e7d9cf52671d0bc3c4d374991be65b6e11bc34121027ae06a5b3fe1de495fa9d4e738e48810b8b06fa6c959a5305426f78f42b48f8cffffffff018c949800000000001976a91482932cf55b847ffa52832d2bbec2838f658f226788ac00000000",
    "callbackUrl":"https://your-server/api/v1/channel/533",
    "callbackToken":"CNaecHA44nGNJCvvccx3TSxwb4F490574knnkf44S19W6cNmbumVa6k3ESQw",
    "merkleProof":false,
    "dsCheck": true
}
```
#### Response:
```json
{
    "payload": "{\"apiVersion\":\"1.2.0\",\"timestamp\":\"2020-11-13T08:04:25.9291559Z\",\"txid\":\"0d0ad5677eb0862f94b3eda7f13633f91cf7c4c8c14e1451ffd333d52ff8e207\",\"returnResult\":\"failure\",\"resultDescription\":\"Missing inputs\",\"minerId\":\"030d1fe5c1b560efe196ba40540ce9017c20daa9504c4c4cec6184fc702d9f274e\",\"currentHighestBlockHash\":\"100677f99bdd7d4f0b8ea3f35d575d0f69a80f89b5b5f14e11005f57e5e63ef5\",\"currentHighestBlockHeight\":151,\"txSecondMempoolExpiry\":0,\"conflictedWith\":[{\"txid\":\"9f817649adde97338bcda695ee13ae1c71960eac60e49671fed0bdcf45581d94\",\"size\":191,\"hex\":\"01000000015d7d8ffefc2b95a68a95d8e3c50715f8affc0e56ef58a05c773789e6fa3eb537010000006a47304402206a9372778ff1ea314cfb2ec4e6bc93a57fe67c5ca915d004850f8079c876977c022066e3581cbec0eb2d525d4d83d01fff4f4e0b13a477f4f6a07d9168cc40bbabe54121027ae06a5b3fe1de495fa9d4e738e48810b8b06fa6c959a5305426f78f42b48f8cffffffff0198929800000000001976a91482932cf55b847ffa52832d2bbec2838f658f226788ac00000000\"}]}",
    "signature": "3044022048739a74a7f14b870d410f02c60dafcee2899348c7cd1184977e9ac5096ba63a022038ca0066645d1201ba0f385bd88da4c9bc7410582ae7bb3e248d79b7dbcfd205",
    "publicKey": "030d1fe5c1b560efe196ba40540ce9017c20daa9504c4c4cec6184fc702d9f274e",
    "encoding": "UTF-8",
    "mimetype": "application/json"
}
```

Merkle proof callback can be requested by specifying:
```json
{
 "merkleProof": true
}
```

If callback was requested on transaction submit, merchant should receive a notification of doublespend and/or merkle proof via callback URL. mAPI process all requested notifications and sends them out in batches.
Callbacks have three possible callbackReason: "doubleSpend", "doubleSpendAttempt" and "merkleProof". DoubleSpendAttempt implies, that double spend was detected in mempool.

Double spend callback example:
```json
{	
  "callbackPayload": "{\"doubleSpendTxId\":\"f1f8d3de162f3558b97b052064ce1d0c45805490c210bdbc4d4f8b44cd0f143e\", \"payload\":\"01000000014979e6d8237d7579a19aa657a568a3db46a973f737c120dffd6a8ba9432fa3f6010000006a47304402205fc740f902ccdadc2c3323f0258895f597fb75f92b13d14dd034119bee96e5f302207fd0feb68812dfa4a8e281f9af3a5b341a6fe0d14ff27648ae58c9a8aacee7d94121027ae06a5b3fe1de495fa9d4e738e48810b8b06fa6c959a5305426f78f42b48f8cffffffff018c949800000000001976a91482932cf55b847ffa52832d2bbec2838f658f226788ac00000000\"}",
  "apiVersion": "1.2.0",
  "timestamp": "2020-11-03T13:24:31.233647Z",
  "minerId": "030d1fe5c1b560efe196ba40540ce9017c20daa9504c4c4cec6184fc702d9f274e",
  "blockHash": "34bbc00697512058cb040e1c7bbba5d03a2e94270093eb28114747430137f9b7",
  "blockHeight": 153,
  "callbackTxId": "8750e986a296d39262736ed8b8f8061c6dce1c262844e1ad674a3bc134772167",
  "callbackReason": "doubleSpend"
}
```

Double spend attempt callback example:
```json
{	
  "callbackPayload": "{\"doubleSpendTxId\":\"7ea230b1610768374285150537323add313c1b9271b1b8110f5ddc629bf77f46\", \"payload\":\"0100000001e75284dc47cb0beae5ebc7041d04dd2c6d29644a000af67810aad48567e879a0000000006a47304402203d13c692142b4b50737141145795ccb5bb9f5f8505b2d9b5a35f2f838b11feb102201cee2f2fe33c3d592f5e990700861baf9605b3b0199142bbc69ae88d1a28fa964121027ae06a5b3fe1de495fa9d4e738e48810b8b06fa6c959a5305426f78f42b48f8cffffffff018c949800000000001976a91482932cf55b847ffa52832d2bbec2838f658f226788ac00000000\"}",
  "apiVersion": "1.2.0",
  "timestamp": "2020-11-03T13:24:31.233647Z",
  "minerId": "030d1fe5c1b560efe196ba40540ce9017c20daa9504c4c4cec6184fc702d9f274e",
  "blockHash": "34bbc00697512058cb040e1c7bbba5d03a2e94270093eb28114747430137f9b7",
  "blockHeight": 153,
  "callbackTxId": "8750e986a296d39262736ed8b8f8061c6dce1c262844e1ad674a3bc134772167",
  "callbackReason": "doubleSpendAttempt"
}
```

Merkle proof callback example:
```json
{	  
  "callbackPayload": "{\"flags\":2,\"index\":1,\"txOrId\":\"acad8d40b3a17117026ace82ef56d269283753d310ddaeabe7b5d226e8dbe973\",\"target\": {\"hash\":\"0e9a2af27919b30a066383d512d64d4569590f935007198dacad9824af643177\",\"confirmations\":1,\"height\":152,\"version\":536870912,\"versionHex\":"20000000",\"merkleroot\":\"0298acf415976238163cd82b9aab9826fb8fbfbbf438e55185a668d97bf721a8\",\"num_tx\":2,\"time\":1604409778,\"mediantime\":1604409777,\"nonce\":0,\"bits\":\"207fffff\",\"difficulty\":4.656542373906925E-10,\"chainwork\":\"0000000000000000000000000000000000000000000000000000000000000132\",\"previousblockhash\":\"62ae67b463764d045f4cbe54f1f7eb63ccf70d52647981ffdfde43ca4979a8ee\"},\"nodes\":[\"5b537f8fba7b4057971f7e904794c59913d9a9038e6900669d08c1cf0cc48133\"]}",
  "apiVersion":"1.2.0",
  "timestamp":"2020-11-03T13:22:42.1341243Z",
  "minerId":"030d1fe5c1b560efe196ba40540ce9017c20daa9504c4c4cec6184fc702d9f274e",
  "blockHash":"0e9a2af27919b30a066383d512d64d4569590f935007198dacad9824af643177",
  "blockHeight":152,
  "callbackTxId":"acad8d40b3a17117026ace82ef56d269283753d310ddaeabe7b5d226e8dbe973",
  "callbackReason":"merkleProof"
}
```


### Authorization/Authentication and Special Rates

Merchant API providers would likely want to offer special or discounted rates to specific customers. To do this they would need to add an extra layer to enable authorization/authentication on public interface. Current implementation supports JSON Web Tokens (JWT) issued to specific users. The users can include that token in their HTTP header and as a result receive lower fee rates.

If no token is used and the call is done anonymously, then the default rate is supplied. If a JWT token (issued by merchant API or other identity provider) is used, then the caller will receive the corresponding fee rate. At the moment, for this version of the merchant API implementation, the token must be issued and sent to the customer manually.

### Authorization/Authentication Example

```console
$ curl -H "Authorization:Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOiIyMDIwLTEwLTE0VDExOjQ0OjA3LjEyOTAwOCswMTowMCIsIm5hbWUiOiJsb3cifQ.LV8kz02bwxZ21qgqCvmgWfbGZCtdSo9px47wQ3_6Zrk" localhost:5051/mapi/feeQuote
```

### JWT Token Manager

The reference implementation contains a token manager that can be used to generate and verify validity of the tokens. Token manager currently only supports symmetric encryption `HS256`.

The following command line options can be specified when generating a token

```console
Options:
  -n, --name <name> (REQUIRED)        Unique name of the subject token is being issued to
  -d, --days <days> (REQUIRED)        Days the token will be valid for
  -k, --key <key> (REQUIRED)          Secret shared use to sign the token. At lest 16 characters
  -i, --issuer <issuer> (REQUIRED)    Unique issuer of the token (for example URI identifiably the miner)
  -a, --audience <audience>           Audience tha this token should be used for [default: merchant_api]
```

For example, you can generate the token by running

```console
$ TokenManager generate -n specialuser -i http://mysite.com -k thisisadevelopmentkey -d 1000

Token:{"alg":"HS256","typ":"JWT"}.{"sub":"specialuser","nbf":1599494789,"exp":1685894789,"iat":1599494789,"iss":"http://mysite.com","aud":"merchant_api"}
Valid until UTC: 4. 06. 2023 16:06:29

The following should be used as authorization header:
Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJzcGVjaWFsdXNlciIsIm5iZiI6MTU5OTQ5NDc4OSwiZXhwIjoxNjg1ODk0Nzg5LCJpYXQiOjE1OTk0OTQ3ODksImlzcyI6Imh0dHA6Ly9teXNpdGUuY29tIiwiYXVkIjoibWVyY2hhbnRfYXBpIn0.xbtwEKdbGv1AasXe_QYsmb5sURyrcr-812cX-Ps98Yk

```

Now any `specialuser` using this token will be offered special fee rates when uploaded. The special fees needs to be uploaded through admin interface

To validate a token, you can use `validate` command:

```console
$ TokenManager validate -k thisisadevelopmentkey -t eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJzcGVjaWFsdXNlciIsIm5iZiI6MTU5OTQ5NDc4OSwiZXhwIjoxNjg1ODk0Nzg5LCJpYXQiOjE1OTk0OTQ3ODksImlzcyI6Imh0dHA6Ly9teXNpdGUuY29tIiwiYXVkIjoibWVyY2hhbnRfYXBpIn0.xbtwEKdbGv1AasXe_QYsmb5sURyrcr-812cX-Ps98Yk

Token signature and time constraints are OK. Issuer and audience were not validated.

Token:
{"alg":"HS256","typ":"JWT"}.{"sub":"specialuser","nbf":1599494789,"exp":1685894789,"iat":1599494789,"iss":"http://mysite.com","aud":"merchant_api"}
```

## Admin interface  

Admin interface can be used to add, update or remove connections to this node. It is only accessible to authenticated users. Authentication is performed through `Api-Key` HTTP header. The provided value must match the one provided in configuration variable `RestAdminAPIKey`.


### Managing fee quotes

To create a new fee quote use the following:

```
POST api/v1/FeeQuote
```

Example with curl - add feeQuote valid from 01/10/2020 for anonymous user:

```console
$ curl -H "Api-Key: [RestAdminAPIKey]" -H "Content-Type: application/json" -X POST https://localhost:5051/api/v1/FeeQuote -d "{ \"validFrom\": \"2020-10-01T12:00:00\", \"identity\": null, \"identityProvider\": null, \"fees\": [{ \"feeType\": \"standard\", \"miningFee\" : { \"satoshis\": 100, \"bytes\": 200 }, \"relayFee\" : { \"satoshis\": 100, \"bytes\": 200 } }, { \"feeType\": \"data\", \"miningFee\" : { \"satoshis\": 100, \"bytes\": 200 }, \"relayFee\" : { \"satoshis\": 100, \"bytes\": 200 } }] }"
```

To get list of all fee quotes, matching one or more criterias use the following

```
GET api/v1/FeeQuote
```

You can filter fee quotes by providing additional optional criteria in query string:

* `identity` - return only fee quotes for users that authenticate with a JWT token that was issued to specified identity
* `identityProvider` - return only fee quotes for users that authenticate with a JWT token that was issued by specified token authority
* `anonymous` - specify  `true` to return only fee quotes for anonymous user.
* `current` - specify  `true` to return only fee quotes that are currently valid.
* `valid` - specify  `true` to return only fee quotes that are valid in interval with QuoteExpiryMinutes

To get list of all fee quotes (including expired ones) for all users use GET api/v1/FeeQuote without filters.


To get a specific fee quote by id use:

```
GET api/v1/FeeQuote/{id}
```

Note: it is not possible to delete or update a fee quote once it is published, but you can make it obsolete by publishing a new fee quote.


### Managing nodes

The reference implementation can talk to one or more instances of bitcoind nodes.

Each node that is being added to the Merchant API has to have zmq notifications enabled (***pubhashblock, pubinvalidtx, pubdiscardedfrommempool***). When enabling zmq notificationas on node, care should be taken that the URI that will be used for zmq notification is accessible from the host where the MerchantAPI will be running (*WARNING: localhost (127.0.0.1) should only be used if bitcoin node and MerchantAPI are running on same host*)


To create new connection to a new  bitcoind instance use:

```
POST api/v1/Node
```

Add node with curl:

```console
curl -H "Api-Key: [RestAdminAPIKey]" -H "Content-Type: application/json" -X POST https://localhost:5051/api/v1/Node -d "{ \"id\" : \"[host:port]\", \"username\": \"[username]\", \"password\": \"[password]\", \"remarks\":\"[remarks]\" }"
```

To update parameters for an existing bitcoind instance use:

```
PUT api/v1/Node/{nodeId}
```

To update node's password created with curl before use `Content-Type: application/json` and authorization `Api-Key: [RestAdminAPIKey]` with the following JSON request body:

```json
{
    "id": "[host:port]",
    "username": "[username]",
    "password": "[newPassword]",
    "remarks": "[remarks]"
}
```

To remove connection to an existing bitcoind instance use:

```bash
DELETE api/v1/Node/{nodeId}
```

To get a list of parameters for a specific node use:

```bash
GET api/v1/Node/{nodeId}
```

To get a list of parameters for all nodes use:

```bash
GET api/v1/Node
```

NOTE: when returning connection parameters, password is not return for security reasons.

### Status check

To check status of ZMQ subscriptions use:

```
GET api/v1/status/zmq
```

## How callbacks are being processed

For each transaction that is submitted to mAPI it can be set if the submiter should receive a notification of doublespend or merkle proof via callback URL. mAPI processes all requested notifications and sends them out as described below:

* all notifications are sent out in batches
* each batch contains a limited number of notifications for single host (configurable with `NOTIFICATION_MAX_NOTIFICATIONS_IN_BATCH`)
* response time for each host is tracked and two separate pools of tasks are used for delivering instant notifications: One pool for fast hosts and second pool for slow hosts. (threshold for slow/fast pools can be configured with `NOTIFICATION_SLOW_HOST_THRESHOLD_MS`)
* when an event is received from the node an attempt is made to insert notification it into queue for instant delivery
* if a callback fails or if instant delivery queues are full, the notifications is scheduled for delivery in background.
* background delivery queue is used for periodically processing failed notifications. Single task is used for background delivery

## Build and deploy

### Building docker images

Build docker images for **MerchantAPI App & Data**  running this commands in folder `/src/Deploy`

```bash
On Linux: ./build.sh
On Windows: build.bat
```

### Deploying docker images
  
1. Create `config` folder and save SSL server certificate file (*<certificate_file_name>.pfx*) into to the `config` folder. This server certificate is required to setup TLS (SSL).
2. Copy .crt files with with root and intermediate CA certificates that issued SSL server certificates which are used by callback endpoint. Each certificate must be exported as a **Base-64 encoded X.509** file with a crt extension type. This step is required if callback endpoint uses SSL server certificate issued by untrusted CA (such as self signed certificate).
3. Create and copy **providers.json** file into config folder. Sample provider.json :

    ```JSON
    {
      "IdentityProviders": {
        "Providers": [
          {
          "Issuer": "http://mysite.com",
          "Audience": "http://myaudience.com",
          "Algorithm": "HS256",
          "SymmetricSecurityKey": "thisisadevelopmentkey"
          }
        ]
      }
    }
    ```

    | Parameter | Description |
    | ----------- | ----------- |
    | Issuer | Token issuer |
    | Audience | Token audience |
    | Algorithm | (optional) Signing algorithm allowed for token (if not set **HS256** will be used) |
    | SymmetricSecurityKey | Symmetric security key that token should be signed with |

4. Populate all environment variables in `.env` file in target folder:

    | Parameter | Description |
    | ----------- | ----------- |
    | HTTPSPORT | port where the application will listen/run |
    | CERTIFICATEPASSWORD | the password of the *.pfx file in the config folder |
    | CERTIFICATEFILENAME | *<certificate_file_name.pfx>* |
    | QUOTE_EXPIRY_MINUTES | Specify fee quote expiry time |
    | ZMQ_CONNECTION_TEST_INTERVAL_SEC | how often does ZMQ subscription service test that the connection with node is still alive. Default: 60 seconds |
    | RESTADMIN_APIKEY | Authorization key for accessing administration interface |
    | DELTA_BLOCKHEIGHT_FOR_DOUBLESPENDCHECK | Number of old blocks that are checked for double spends |
    | CLEAN_UP_TX_AFTER_DAYS | Number of days transactions and blocks are kept in database. Default: 3 days |
    | CLEAN_UP_TX_PERIOD_SEC | Time period of transactions cleanup check. Default: 1 hour |
    | WIF_PRIVATEKEY | Private key that is used to sign responses with (must be omited if minerid settings are specified, and vice versa) |
    | NOTIFICATION_NOTIFICATION_INTERVAL_SEC | Period when background service will retry to send notifications with error |
    | NOTIFICATION_INSTANT_NOTIFICATION_TASKS | Maximum number of concurrent tasks for sending notifications to callback endpoints (must be between 2-100) |
    | NOTIFICATION_INSTANT_NOTIFICATIONS_QUEUE_SIZE | Maximum number of notifications waiting in instant queue before new notifications will be scheduled for slow background delivery |
    | NOTIFICATION_MAX_NOTIFICATIONS_IN_BATCH | Maximum number of notifications per host being processed by delivery task at once |
    | NOTIFICATION_SLOW_HOST_THRESHOLD_MS | Callback response time threshold that determines which host is deemed slow/fast |
    | NOTIFICATION_INSTANT_NOTIFICATIONS_SLOW_TASK_PERCENTAGE | Percent of notification tasks from NOTIFICATION_INSTANT_NOTIFICATION_TASKS that will be reserved for slow hosts |
    | NOTIFICATION_NO_OF_SAVED_EXECUTION_TIMES | Maximum number of callback response times saved for each host. Used for calculating average response time for a host |
    | NOTIFICATION_NOTIFICATIONS_RETRY_COUNT | Number of retries for failed notifications, before quiting with retries |
    | NOTIFICATION_SLOW_HOST_RESPONSE_TIMEOUT_MS | Callback response timeout for slow host |
    | NOTIFICATION_FAST_HOST_RESPONSE_TIMEOUT_MS | Callback response timeout for fast host |
    | MINERID_SERVER_URL | URL pointing to MinerID REST endpoint |
    | MINERID_SERVER_ALIAS | Alias be used when communicating with the endpoint |
    | MINERID_SERVER_AUTHENTICATION | HTTP authentication header that be used to when communicating with the endpoint |

5. Run this command in target folder to start mAPI application:

    ```bash
    docker-compose up -d
    ```

The docker images are automatically pulled from Docker Hub. Database updates are triggered upon application start or when tests are run.

# Setting up a development environment

For development, you will need the following

1. [.NET core SDK 3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) installed in your environnement.
2. and instance of PostgreSQL database. You can download it from [here](https://www.postgresql.org/download/) or use a [Docker image](https://hub.docker.com/_/postgres).
3. access to instance of running [BSV node](https://github.com/bitcoin-sv/bitcoin-sv/releases) with RPC interface and ZMQ notifications enabled

Perform the following set up steps:

1. Update `DBConnectionString`(connection string used by mAPI) and `DBConnectionStringMaster` (same as DBConnectionString, but with user that has admin privileges - is used to upgrade database) setting in `src/MerchantAPI/APIGateway/APIGateway.Rest/appsettings.Development.json` and `src/MerchantAPI/APIGateway/APIGateway.Test.Functional/appsettings.Development.json` so that they point to your PostgreSQL server
2. Update `BitcoindFullPath` in `src/MerchantAPI/APIGateway/APIGateway.Test.Functional/appsettings.Development.json` so that it points to bitcoind executable used during functional tests
3. Run scripts from `src/crea/merchantapi2/src/MerchantAPI/APIGateway.Database/APIGateway/Database/scripts` to create database.


## Run

```console
cd src/MerchantAPI/APIGateway/APIGateway.Rest
dotnet run
```

## Test

Run individual tests or run all tests with:

```console
cd src/MerchantAPI/APIGateway/APIGateway.Test.Functional/
dotnet test
```

## Configuration

Following table lists all configuration settings with mappings to environment variables. For description of each setting see `Populate all environment variables` under **Deploying docker images**

  | Application Setting | Environment variable |
  | ------------------- | -------------------- |
  | QuoteExpiryMinutes | QUOTE_EXPIRY_MINUTES |
  | RestAdminAPIKey | RESTADMIN_APIKEY |
  | DeltaBlockHeightForDoubleSpendCheck | DELTA_BLOCKHEIGHT_FOR_DOUBLESPENDCHECK |
  | CleanUpTxAfterDays| CLEAN_UP_TX_AFTER_DAYS |
  | CleanUpTxPeriodSec| CLEAN_UP_TX_PERIOD_SEC |
  | WifPrivateKey | WIF_PRIVATEKEY |
  | ZmqConnectionTestIntervalSec | ZMQ_CONNECTION_TEST_INTERVAL_SEC |
  | **Notification region**|
  | NotificationIntervalSec | NOTIFICATION_NOTIFICATION_INTERVAL_SEC |
  | InstantNotificationsTasks | NOTIFICATION_INSTANT_NOTIFICATION_TASKS |
  | InstantNotificationsQueueSize | NOTIFICATION_INSTANT_NOTIFICATIONS_QUEUE_SIZE |
  | MaxNotificationsInBatch | NOTIFICATION_MAX_NOTIFICATIONS_IN_BATCH |
  | SlowHostThresholdInMs | NOTIFICATION_SLOW_HOST_THRESHOLD_MS |
  | InstantNotificationsSlowTaskPercentage | NOTIFICATION_INSTANT_NOTIFICATIONS_SLOW_TASK_PERCENTAGE |
  | NoOfSavedExecutionTimes | NOTIFICATION_NO_OF_SAVED_EXECUTION_TIMES |
  | NotificationsRetryCount | NOTIFICATION_NOTIFICATIONS_RETRY_COUNT |
  | SlowHostResponseTimeoutMS | NOTIFICATION_SLOW_HOST_RESPONSE_TIMEOUT_MS |
  | FastHostResponseTimeoutMS | NOTIFICATION_FAST_HOST_RESPONSE_TIMEOUT_MS |
  | **MinerIdServer region** |
  | Url | MINERID_SERVER_URL |
  | Alias | MINERID_SERVER_ALIAS |
  | Authentication | MINERID_SERVER_AUTHENTICATION |


Following table lists additional configuration settings:

  | Setting | Description |
  | ------- | ----------- |
  | **ConnectionStrings** region |
  | DBConnectionString | connection string for access to PostgreSQL database |
  | DBConnectionStringMaster | is same as DBConnectionString, but with user that has admin privileges |
