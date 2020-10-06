# mAPI

More details available in the [BRFC Spec](https://github.com/bitcoin-sv-specs/brfc-merchantapi) for Merchant API.  

### [Swagger UI](https://bitcoin-sv.github.io/merchantapi-reference)  

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
  "callBackUrl":  "https://your.service.callback/endpoint",
  "callBackToken" : "Authorization: <your_authorization_header>",
  "merkleProof" : true,
  "dsCheck" : true
}
```

To submit transaction in binary format use `Content-Type: application/octet-stream` with the binary serialized transaction in the request body. You can specify `callBackUrl`, `callBackToken`, `merkleProof` and `dsCheck` in the query string.


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
    "callBackUrl":  "https://your.service.callback/endpoint",
    "callBackToken" : "Authorization: <your_authorization_header>",
    "merkleProof" : true,
    "dsCheck" : true
  },
  ....
]
```

You can also omit `callBackUrl`, `callBackToken`, `merkleProof` and `dsCheck` from the request body and provide the values in the query string.

To submit transaction in binary format use `Content-Type: application/octet-stream` with the binary serialized transactions in the request body. Use query string to specify the remaining parameters.


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
  -n, --name <name> (REQUIRED)        Unique name od the subject token is being issued to
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

Now anyone `specialuser` using this token will offered special fee rates when uploaded. The special fees needs to be uploaded through admin interface

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

Each node that is being added to the Merchant API has to have zmq notifications enabled (***pubhashblock, pubinvalidtx, pubremovedfrommempool***). When enabling zmq notificationas on node, care should be taken that the URI that will be used for zmq notification is accessible from the host where the MerchantAPI will be running (*WARNING: localhost (127.0.0.1) should only be used if bitcoin node and MerchantAPI are running on same host*)


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
    | DELTA_BLOCKHEIGHT_FOR_DOUBLESPENDCHECK | Number of old blocks that are checked for  double spends |
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
  | DBConnectionString | connection string for to PostgreSQL database |
