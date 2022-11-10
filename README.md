# mAPI Reference Implementation

Readme v.1.4.9m.

p

The details of the BRFC mAPI Specification are available in [BRFC mAPI Specification](https://github.com/bitcoin-sv-specs/brfc-merchantapi).  

> The golang (v1.1) implementation is no longer being maintained and has been moved to the [golang-v1.1 branch](https://github.com/bitcoin-sv/merchantapi-reference/tree/golang-v1.1).

## Swagger UI

The REST API can also be seen in the [Swagger UI](https://bitcoin-sv.github.io/merchantapi-reference).

## Support

For support and general discussion of both the mAPI standard and the mAPI Reference Implementation, please join the following [telegram group](https://t.me/joinchat/JB6ZzktqwaiJX_5lzQpQIA).

## Requirements

mAPI Reference Implementation requires access to Bitcoin SV node version 1.0.10 or newer. See [Managing nodes](#Managing-nodes) for details how to connect to a bitcoin node.

For running in production, use Docker. Docker images can be [downloaded](#Download-docker-images) from docker hub, or created as part of the [build](#Building-docker-images). See [Deploying docker images](#Deploying-docker-images) for details on how to run them.

A SSL server certificate is required for installation. You can obtain the certificate from your IT support team. There are also services that issue free SSL certificates, such as letsencrypt.org.  The certificate must be issued for the host with a fully qualified domain name. To use the server side certificate, you need to export it (including its corresponding private key) in PFX file format (*.pfx).

For setting up a development environment, optionally using Prometheus (a monitoring system & time series database) and Grafana (an open observability platform) see [development](#Development).

## Improvements

mAPI Reference Implementation v1.5.0 implements mAPI Specification v1.5.0, which provides additional features for querying transaction data.

mAPI Reference Implementation has greatly improved resilience against adversities.

### Submit transactions

mAPI Reference Implementation records submitted transactions and monitors nodes’ mempool, so that if node fails after it has received a transaction, mAPI Reference Implementation is able to resubmit the transaction on behalf of the user.

If mAPI Reference Implementation resubmits a transaction or submits a transaction that has already been mined, and node returns an error such as TransactionAlreadyKnown, then mAPI Reference Implementation maps that into a successful result for the user.

If mAPI Reference Implementation gets mixed results from multiple nodes, it maps that into a successful result for the user.
 
If mAPI Reference Implementation returns a HTTP code 4xx (such as missing inputs) then the user has an opportunity to fix the error and resubmit the transaction.

If mAPI Reference Implementation returns a HTTP code 5xx, then the user can try again later.

An indication that the transaction may be resubmitted is given by the submit transaction response payload `failureRetryable` flag.

### Query transactions

mAPI Reference Implementation enables the user to obtain the Merkle proof for a transaction by querying it.

### Query transaction outputs

mAPI Reference Implementation enables the user to query various attributes of transaction outputs.

## REST API Interfaces

The mAPI Reference Implementation exposes different **REST API** interfaces:

* a public interface for submitting and querying transactions
* an administrator interface for managing connections to bitcoin nodes and policy quotes

It also provides a JSON Web Tokens (JWT) Manager to enable authenticated users to obtain special policy rates.

## Public Interface

The public interface can be used to submit transactions and query transaction status. It is accessible to both authenticated and unauthenticated users, but authenticated users might get special fee rates.

The endpoints are implemented in accordance with the BRFC mAPI Specification and are summarised below.

The possibility of using JWT means that each REST command may additionally respond with HTTP code 401, Unauthorized if any supplied JWT token is invalid.

Note: Any `/mapi/` REST call will return 5xx if node is unresponsive or the database is down.

### 1. Get Policy Quote

```
GET /mapi/policyQuote
```

Responds with a policy quotation. This is a superset of the fee quotation (below).

#### Special Policy Quotes

The administrator may wish to offer special policy quotes to specific customers. The mAPI Reference Implementation supports JWT issued to authenticated users. The authenticated users include the JWT in their HTTP header and, as a result, receive special policy quotes.

If no JWT is supplied, then the call is anonymous (unauthenticated), and the default policy quote is supplied. If a JWT (created by the mAPI JWT Manager user or other JWT provider) is supplied, then the caller will receive the corresponding special policy quote. For this version of the mAPI Reference Implementation, the JWT must be created by the JWT manager and issued to the customer manually.

##### Special Policy Quote Example

```console
$ curl -H "Authorization:Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOiIyMDIwLTEwLTE0VDExOjQ0OjA3LjEyOTAwOCswMTowMCIsIm5hbWUiOiJsb3cifQ.LV8kz02bwxZ21qgqCvmgWfbGZCtdSo9px47wQ3_6Zrk" localhost:5051/mapi/policyQuote
```

### 2. Get Fee Quote

```
GET /mapi/feeQuote
```

Responds with a fee quotation. This is a subset of the policy quotation (above).

### 3. Submit Transaction

```
POST /mapi/tx
```

#### Example JSON Request body

The body may also be binary.

```json
{
  "rawtx": "[transaction_hex_string]",
  "callbackUrl": "https://your.service.callback/endpoint",
  "callbackToken": "Authorization: <your_authorization_header>",
  "merkleProof": true,
  "merkleFormat": "TSC",
  "dsCheck": true
}
```

Users may either poll the status of the transactions they have submitted, or employ the callback mechanism.
Double spend and Merkle proof notifications will be sent to the callbackURL if supplied. 
Where recipients are using [SPV Channels](https://github.com/bitcoin-sv-specs/brfc-spvchannels), this requires the channel to be set up and ready to receive messages.

#### Response Timeout

There is a small possibility that no response will be forthcoming due to exceptional circumstances such as the node being reset.

Therefore the user may wish to keep a record of all transactions submitted, and if no response is obtained within an acceptable timescale (several seconds), the transaction may be resubmitted.

#### Special Policy Quotes

A JWT may be supplied as above, in order to authenticate the user and cause the associated special policy quote to be applied in computation of the Submit Transaction cost.

### 4. Query Transaction Status

```
GET /mapi/tx/{txid}?merkleProof=Boolean&merkleFormat=TSC
```

Responds with the status of the transaction with optional Merkle proof information.

### 5. Submit Multiple Transactions

```
POST /mapi/txs
```

This is similar to Submit Transaction, but an array of transactions may be sent.

### 6. Query Transaction Outputs

```
POST /mapi/txouts?includeMempool=Boolean&returnField=confirmations
```

#### Example JSON Request body

```json
{
  [
    { "txid": "0bc1733f05aae146c3641fd...57f60f19a430ffe867020619d54800", "n": 0 },
    { "txid": "d013adf525ed5feaffc6e9d...40566470181f099f1560343cdcfd00", "n": 0 },
    { "txid": "d013adf525ed5feaffc6e9d...40566470181f099f1560343cdcfd00", "n": 1 }
  ]
}

```

## Administrator Interface

The Administrator Interface of the mAPI Reference Implementation manages policy quotes, nodes and special policy fee rates for the Public API.

These services are only available to the administrator. Authentication is performed through the Api-Key HTTP header. The value provided must match the one stored in the configuration variable `RestAdminAPIKey`.

Note: Any `/api/v1/` REST call will return 5xx if the database is down.

### Managing Policy Quotes

Policy Quotes may be specified for the unauthenticated users, or specific authenticated users.

#### Create Policy Quote

To create a new policy quote use the following:

```
POST api/v1/PolicyQuote
```

Example with curl - add a policyQuote valid from 01/10/2021 for unauthenticated (anonymous) users:

```console
$ curl -H "Api-Key: [RestAdminAPIKey]" \
       -H "Content-Type: application/json" \
       -X POST https://localhost:5051/api/v1/PolicyQuote \
       -d "{ \"validFrom\": \"2021-10-01T12:00:00\", \
            \"identity\": null, \"identityProvider\": null, \
            \"fees\": [ \
              { \
                \"feeType\": \"standard\", \
                  \"miningFee\": { \"satoshis\": 100, \"bytes\": 200 }, \
                  \"relayFee\": { \"satoshis\": 100, \"bytes\": 200 } }, \
               { \"feeType\": \"data\", \
                  \"miningFee\": { \"satoshis\": 100, \"bytes\": 200 }, \
                  \"relayFee\": { \"satoshis\": 100, \"bytes\": 200 } \
              }], \
            \"policies\": { \
                \"skipscriptflags\": [\"MINIMALDATA\", \"DERSIG\", \
                  \"NULLDUMMY\", \"CLEANSTACK\"], \
                \"maxtxsizepolicy\": 99999, \
                \"datacarriersize\": 100000, \
                \"maxscriptsizepolicy\": 100000, \
                \"maxscriptnumlengthpolicy\": 100000, \
                \"maxstackmemoryusagepolicy\": 10000000, \
                \"limitancestorcount\": 1000, \
                \"limitcpfpgroupmemberscount\": 10, \
                \"acceptnonstdoutputs\": true, \
                \"datacarrier\": true, \
                \"maxstdtxvalidationduration\": 99, \
                \"maxnonstdtxvalidationduration\": 100 \
            } \
          }"
```

> Note: BSV Node v1.0.11 onwards no longer support "dustrelayfee" and "dustlimitfactor" policies and they must not be set. Doing so will cause an error when the user submits a transaction.

The parameters above are:

| Parameter | Description |
| ----------- | ----------- |
| `validFrom` | the timestamp from when the policy is valid. Only one policy should be valid for each identity (or the anonymous user) at any one time |
| `identity` | the identity of the user, or null for the anonymous user |
| `identityProvider` | the identity of the JWT authority, or null for the anonymous user |
| `fees` | fees charged by the miner (see [feeSpec BRFC](https://github.com/bitcoin-sv-specs/brfc-misc/tree/master/feespec)) |
| `callbacks` | IP addresses of DSNT servers (see [specification](https://github.com/bitcoin-sv-specs/protocol/blob/master/updates/double-spend-notifications.md)) such as this mAPI reference implementation |
| `policies` | values of miner policies as configured by the administrator (below) |

#### Get All Policy Quotes

To get a list of all policy quotes matching one or more criteria, use the following:

```
GET api/v1/PolicyQuote
```

You can filter fee quotes by providing additional optional criteria in the query string:

* `identity` - returns only fee quotes for users that authenticate with a JWT token that was issued to the specified `identity`
* `identityProvider` - returns only fee quotes for users that authenticate with a JWT token that was issued by the specified token authority
* `anonymous` - specify `true` to return only fee quotes for anonymous (unauthenticated) users
* `current` - specify `true` to return only fee quotes that are currently valid
* `valid` - specify `true` to return only fee quotes that are valid within QUOTE_EXPIRY_MINUTES (configured in the .env file)

To get a list of all policy quotes (including expired ones) for all users use GET api/v1/PolicyQuote without filters.

#### Get a Policy Quote

To get a specific policy quote by `identity` use:

```
GET api/v1/PolicyQuote/{identity}
```

Note: it is not possible to delete or update a policy quote once it is published, but it can be made obsolete by publishing a new policy quote.

#### Get Unconfirmed Transactions

To get the list of transactions that were sent to node but are not marked as accepted in the database with a with given policyQuoteId {id} or a given identity {identity} or a given identityProvider {IDP} use:
```
GET api/v1/unconfirmedTxs?policyQuoteId={PQID}&identity={ID}&identityProvider={IDP}
```

At least one parameter must be supplied. The others are optional.

If no policies match the request, the response is HTTP code 400 “BadRequest”.

#### Delete Unconfirmed Transactions

To delete the list of transactions that were sent to node but are not marked as accepted in the database with a with given policyQuoteId {id} or a given identity {identity} or a given identityProvider {IDP} use:

```
DELETE api/v1/unconfirmedTxs?policyQuoteId={PQID}&identity={ID}&identityProvider={IDP}
```

At least one parameter must be supplied. The others are optional.

If no policies match the request, the response is HTTP code 400 “BadRequest”. A successful deletion will result in HTTP code 204 “NoContent”.

### Managing Nodes

The reference implementation can communicate with one or more instances of bitcoind nodes.

Each node that is being added to the Merchant API must have zmq notifications enabled (***pubhashblock, pubinvalidtx, pubdiscardedfrommempool***) as well as `invalidtxsink` set to `ZMQ`. When enabling zmq notifications on the node, ensure that the URI that will be used for zmq notification is accessible from the host where the MerchantAPI will be running (*WARNING: localhost (127.0.0.1) should only be used if bitcoin node and Merchant API are running on same host*)

#### Add Node Connection
To create a new connection to a bitcoind instance use:

```
POST api/v1/Node
```

For example, to add a node with curl:

```console
curl -H "Api-Key: [RestAdminAPIKey]" \
     -H "Content-Type: application/json" \
     -X POST https://localhost:5051/api/v1/Node \
     -d "{ \"id\" : \"[host:port]\", \
           \"username\": \"[username]\", \
           \"password\": \"[password]\", \
           \"remarks\": \"[remarks]\", \
           \"zmqNotificationsEndpoint\": \"tcp://a.b.c.d:port\" \
        }"
```

#### Update Node Connection

To update parameters for an existing bitcoind instance use:

```
PUT api/v1/Node/{nodeId}
```

To update a node's fields created with curl, use the authorization `Api-Key: [RestAdminAPIKey]` and `Content-Type: application/json` with the following JSON request body:

```json
{
  "id": "[host:port]",
  "username": "[username]",
  "password": "[newPassword]",
  "remarks": "[remarks]",
  "zmqNotificationsEndpoint": "[zmqNotificationsEndpoint]"
}
```

#### Remove Node Connection

To remove a connection to an existing bitcoind instance, use:

```
DELETE api/v1/Node/{nodeId}
```

#### View Node Connection

To get the list of parameters for a specific node, use:

```
GET api/v1/Node/{nodeId}
```

#### View All Node Connections

To get the list of parameters for all nodes, use:

```
GET api/v1/Node
```

NOTE: When returning connection parameters, the password is not returned for security reasons.

### View ZMQ Status

To check the status of ZMQ subscriptions use:

```
GET api/v1/status/zmq
```

### View Block Parser Status

To view the status of Block Parser use:

```
GET api/v1/status/blockparser
```

Producing an output similar to:
```json
{
  "blocksProcessed": 136,
  "blocksParsed": 136,
  "...",
  "lastBlockParseTime": {
    "totalSeconds": 0.0769461,
    "..."
  },
  "..."
}
```

## JWT Manager

The reference implementation contains a JWT Manager that can be used to generate and verify validity of the JWTs. The JWT Manager supports symmetric encryption `HS256`.

JWTs may be supplied by the administrator to users, who can then supply them when they invoke the public interface REST API calls.

The JWT authenticates the user and ensures that a special policy quote is applied.

The following command line options can be specified when generating a JWT:

```console
Options:
  -n, --name <name> (REQUIRED)        Unique name of the subject that the token is being issued to
  -d, --days <days> (REQUIRED)        Days the token will be valid for
  -k, --key <key> (REQUIRED)          Shared secret used to sign the token (at least 16 characters)
  -i, --issuer <issuer> (REQUIRED)    Unique issuer of the token (for example the URI identifying the miner)
  -a, --audience <audience>           Intended audience for this JWT [default: merchant_api]
```

For example, generate the JWT by running this command:

```console
$ TokenManager generate -n specialuser -i http://mysite.com -k thisisadevelopmentkey -d 1000

Token:{"alg":"HS256","typ":"JWT"}.{"sub":"specialuser","nbf":1599494789,"exp":1685894789,"iat":1599494789,"iss":"http://mysite.com","aud":"merchant_api"}
Valid until UTC: 4. 06. 2023 16:06:29

The following should be used as the authorization header:
Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJzcGVjaWFsdXNlciIsIm5iZiI6MTU5OTQ5NDc4OSwiZXhwIjoxNjg1ODk0Nzg5LCJpYXQiOjE1OTk0OTQ3ODksImlzcyI6Imh0dHA6Ly9teXNpdGUuY29tIiwiYXVkIjoibWVyY2hhbnRfYXBpIn0.xbtwEKdbGv1AasXe_QYsmb5sURyrcr-812cX-Ps98Yk

```

Any authenticated user supplying this JWT will have a special policy quote applied. The special policy quote needs to be configured via the administrator interface as described above.

To validate a JWT, use the `validate` command:

```console
$ TokenManager validate -k thisisadevelopmentkey -t eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJzcGVjaWFsdXNlciIsIm5iZiI6MTU5OTQ5NDc4OSwiZXhwIjoxNjg1ODk0Nzg5LCJpYXQiOjE1OTk0OTQ3ODksImlzcyI6Imh0dHA6Ly9teXNpdGUuY29tIiwiYXVkIjoibWVyY2hhbnRfYXBpIn0.xbtwEKdbGv1AasXe_QYsmb5sURyrcr-812cX-Ps98Yk

Token signature and time constraints are validated. Issuer and audience are not validated.

Token:
{"alg":"HS256","typ":"JWT"}.{"sub":"specialuser","nbf":1599494789,"exp":1685894789,"iat":1599494789,"iss":"http://mysite.com","aud":"merchant_api"}
```

## How Submit Transaction Callbacks are Processed

For each transaction that is submitted to mAPI Reference Implementation it is possible for the submitter to include a DSNT output in the transaction according to the [DSNT specification](https://github.com/bitcoin-sv-specs/protocol/blob/master/updates/double-spend-notifications.md).
The submitter may later receive a notification of a double spend or merkle proof via a callback URL that they included with the submit transaction request.
mAPI Reference Implementation processes all requested notifications and sends them out as described below:

* all notifications are sent out in batches
* each batch contains a limited number of notifications for a single host (configurable with `NOTIFICATION_MAX_NOTIFICATIONS_IN_BATCH`)
* the response time for each host is tracked and two separate pools of tasks are used for delivering notifications: One pool for fast hosts and a second pool for slow hosts. (The threshold for slow/fast hosts can be configured with `NOTIFICATION_SLOW_HOST_THRESHOLD_MS`)
* when an event is received from the node, an attempt is made to insert a notification into the queue for instant delivery
* if a callback fails or if the instant delivery queue is full, the notification is scheduled for delivery in the background
* a background delivery queue is used for periodically processing failed notifications. A single task is used for background delivery

The mAPI Reference Implementation Submit Transaction command enables use of Peer Channels for the message broker. This provides a secure transport mechanism to send messages from the miner to a merchant. Merchants or service providers may use other messaging systems to achieve the same goal.

## Download and deploy

### Download docker images

Download docker images from [here](https://hub.docker.com/r/bitcoinsv/mapi).

Or see below for building an image from this source kit.

### Deploying docker images
  
1. Create a `config` folder and save the SSL server certificate file (*<certificate_file_name>.pfx*) into the `config` folder. This server certificate is required to set up TLS (SSL).
2. Copy the .crt files with the root and intermediate CA certificates that issued the SSL server certificates which are used by the callback endpoint. Each certificate must be exported as a **Base-64 encoded X.509** file with a .crt extension type. This step is required if the callback endpoint uses SSL server certificates issued by an untrusted CA (such as a self signed certificate).
3. Create and copy the **providers.json** file into the config folder. A sample provider.json file is shown below:

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
    | Algorithm | (optional) Signing algorithm allowed for the token (if not set, **HS256** will be used) |
    | SymmetricSecurityKey | Symmetric security key that the token should be signed with |

4. Populate all environment variables in the `.env` file in the target folder:

    | Parameter | Description |
    | ----------- | ----------- |
    | **Communications** | |
    | HTTPSPORT | https port where the application will listen/run |
    |CERTIFICATEPASSWORD	|Password of the `*.pfx` file in the config folder|
    |CERTIFICATEFILENAME	|<certificate_file_name.pfx>|
    |RESTADMIN_APIKEY	|Authorization key for accessing administration interface|
    |ENABLEHTTP	|Enables requests through http port when set to True. This should only be used for testing and must be set to False in the production environment in order to maintain security|
    |HTTPPORT	|Http port where the application will listen/run. Default: port 80|
    | **RPC** | |
|RPC_CLIENT_REQUEST_TIMEOUT_SEC	| Request timeout for single RPC call (without retries). Default: 60 seconds|
|RPC_CLIENT_MULTI_REQUEST_TIMEOUT_SEC	|Request timeout for multi-RPC call (with retries). Default: 20 seconds|
|RPC_CLIENT_NUM_OF_RETRIES	|Maximum number of retries for multi-RPC call. Default: 3|
|RPC_CLIENT_WAIT_BETWEEN_RETRIES_MS	|Wait between multi-RPC calls. Default: 100 milliseconds|
|RPC_CLIENT_RPC_CALLS_ON_STARTUP_RETRIES	|Number of retries for multi-RPC call on start-up. Default: 3|
|RPC_CLIENT_RPC_GET_BLOCK_TIMEOUT_MINUTES	|Request timeout for RPC call GetBlock as stream. Default: 10 minutes|
|RPC_CLIENT_RPC_GET_RAW_MEMPOOL_TIMEOUT_MINUTES	|Request timeout for RPC call GetRawMempool. Default: 2 minutes|
    | **ZMQ** | |
|ZMQ_CONNECTION_RPC_RESPONSE_TIMEOUT_SEC	|Timeout for ZMQ subscription service RPC request calls. Default: 5 seconds|
|ZMQ_STATS_LOG_PERIOD_MIN	|Periodically log ZMQ statistics about nodes and subscriptions every n minutes. Default: 10 minutes|
|ZMQ_CONNECTION_TEST_INTERVAL_SEC	|How often the ZMQ subscription service tests that the connection with the node is still alive. Default: 60 seconds|
    | **Logging** | |
    | LOG_LEVEL_DEFAULT | Log levels 0..6 (Trace, Debug, Information, Warning, Error, Critical, None) defined here: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-6.0#log-level |
    | LOG_LEVEL_MICROSOFT | The log level for the general Microsoft components |
    | LOG_LEVEL_MICROSOFT_HOSTING_LIFETIME | The log level for the Microsoft.Hosting.Lifetime component |
    | LOG_LEVEL_HTTPCLIENT | The log level for the System.Net.Http.HttpClient component |
    | **Blockchain** | |
    | MAX_BLOCK_CHAIN_LENGTH_FOR_FORK | Verify block chain and parse blocks up to this limit. Default: 432 |
    | ENABLE_MISSING_PARENTS_RESUBMISSION | If true, and a transaction is reported to having missing inputs, then the program will resubmit any missing input transactions it holds. Node must have `txindex=1` in bitcoin.conf. Default: false |
| **MinerId** | |
|WIF_PRIVATEKEY	|Private key that is used to sign responses (must be omitted if miner ID settings are specified, and vice versa)|
|MINERID_SERVER_URL	|URL pointing to the MinerID REST endpoint|
|MINERID_SERVER_ALIAS	|Alias to be used when communicating with the endpoint|
|MINERID_SERVER_AUTHENTICATION	|HTTP authentication header that will be used to when communicating with the endpoint, this should include the Bearer authentication keyword, for example:Bearer 2b4a73f333b0aa1a1dfb52….421d78e2efe183df9|
|MINERID_SERVER_REQUEST_TIMEOUT_SEC	REST |request timeout for minerId. Default: 100 seconds|
    | **Mempool Checker** | |
|MEMPOOL_CHECKER_DISABLED	|Disable mempoolChecker service. Default: false|
|MEMPOOL_CHECKER_INTERVAL_SEC	|Interval when mempoolChecker will check and resubmit missing transactions if successful on previous try (errors `Missing inputs` and `Already known` are treated as success). Default: 60 (minimum 10)|
|MEMPOOL_CHECKER_UNSUCCESSFUL_INTERVAL_SEC	|Interval when mempoolChecker will check and resubmit transactions if previous try was terminated with an error (timeout, database exception) or not all of them were successfully submitted. Default: 10|
|MEMPOOL_CHECKER_BLOCKPARSER_QUEUED_MAX	|Force submitting of transactions, even if some blocks are not parsed yet. Default:0|
|MEMPOOL_CHECKER_MISSING_INPUTS_RETRIES	|How often transactions with missing inputs should be resubmitted. Default: 5|
    | **Double Spends** | |
|DELTA_BLOCKHEIGHT_FOR_DOUBLESPENDCHECK	|Number of old blocks that are checked for double spends|
|DS_HOST_BAN_TIME_SEC	|See below|
|DS_MAX_NUM_OF_TX_QUERIES	|See below|
|DS_CACHED_TX_REQUESTS_COOLDOWN_PERIOD_SEC	|See below|
|DS_MAX_NUM_OF_UNKNOWN_QUERIES	|Maximum number of queries for unknown transaction ids allowed before a host will be banned|
|DS_UNKNOWN_TX_QUERY_COOLDOWN_PERIOD_SEC	|How long unknown transactions queries will be stored, before being discarded|
|DS_SCRIPT_VALIDATION_TIMEOUT_SEC	|Total time for script validation when nodes RPC method verifyScript will be called|
   | **Database** | |
|DBCONNECTION_STARTUP_TEST_CONNECTION_MAX_RETRIES	|On start-up try to connect to mAPI Reference Implementation database for the max specified number of retries. Default: 10|
|DBCONNECTION_STARTUP_COMMAND_TIMEOUT_MINUTES	|If not null, override command timeout for the start-up database scripts execution - if null, the default command timeout is 30 seconds. Default: null|
|DBCONNECTION_OPEN_CONNECTION_TIMEOUT_SEC	|Database open connection timeout. Default: 30 seconds|
|DBCONNECTION_OPEN_CONNECTION_MAX_RETRIES	|Database open connection max retries - unless timeout is exceeded. Default: 3|
|CLEAN_UP_TX_AFTER_MEMPOOL_EXPIRED_DAYS	|Number of days mempool transactions and blocks that are not on the active blockchain are kept. Must be the same as node’s config mempoolexpiry. Default: 14 days|
|CLEAN_UP_TX_AFTER_DAYS	|Number of days transactions and blocks that are on the active blockchain are kept in the database. Default: 3 days|
|CLEAN_UP_TX_PERIOD_SEC	|Time period of transactions clean-up check. Default: 1 hour|
  | **Notifications** | |
|NOTIFICATION_NOTIFICATION_INTERVAL_SEC	|Period when background service will retry sending notifications with an error|
|NOTIFICATION_INSTANT_NOTIFICATION_TASKS	|Maximum number of concurrent tasks for sending notifications to callback endpoints (must be between 2-100)|
|NOTIFICATION_INSTANT_NOTIFICATIONS_QUEUE_SIZE	|Maximum number of notifications waiting in instant queue before any new notifications will be scheduled for (slower) background delivery|
|NOTIFICATION_MAX_NOTIFICATIONS_IN_BATCH	|Maximum number of notifications per host being processed by delivery task at any one time|
|NOTIFICATION_SLOW_HOST_THRESHOLD_MS	|Callback response time threshold that determines whether host is deemed slow or fast|
|NOTIFICATION_INSTANT_NOTIFICATIONS_SLOW_TASK_PERCENTAGE	|Percent of notification tasks from NOTIFICATION_INSTANT_NOTIFICATION_TASKS that will be reserved for slow hosts|
|NOTIFICATION_NO_OF_SAVED_EXECUTION_TIMES	|Maximum number of callback response times saved for each host. Used to calculate average response time for a host|
|NOTIFICATION_NOTIFICATIONS_RETRY_COUNT	|Maximum number of retries for failed notifications, before abandoning retries|
|NOTIFICATION_SLOW_HOST_RESPONSE_TIMEOUT_MS	|Callback response timeout for slow host|
|NOTIFICATION_FAST_HOST_RESPONSE_TIMEOUT_MS	|Callback response timeout for fast host|
  | **Policy Quotes** | |
|CALLBACK_IP_ADDRESSES	|An array of IP addresses, separated by commas, which are sent to the merchant in response to GET PolicyQuote|
|QUOTE_EXPIRY_MINUTES	|Fee quote expiry period|
|CHECK_FEE_DISABLED	|Disable fee check|
 | **Transaction Outputs** | |
|ALLOWED_TXOUT_FIELDS|Comma separated field names that may be used in the “returnField” parameter of the Query Transaction Outputs endpoint: scriptPubKey, scriptPubKeyLen, value, isStandard and confirmations|

#### Banning Persistent Hosts

DS_CACHED_TX_REQUESTS_COOLDOWN_PERIOD_SEC is how long the count of requests (queries or submits) for the same transaction id per host is accumulated, before being reset to 0.
If the request count for the same transaction Id exceeds DS_MAX_NUM_OF_TX_QUERIES during this period, the host will be banned and removed from the whitelist. The host will have to desist from sending requests for the same transaction id for at least the cool-down period DS_HOST_BAN_TIME_SEC, before it will become acceptable (un-banned) and can successfully try again.

5. Run this command in the target folder to start the mAPI Reference Implementation application:

    ```bash
    docker-compose up -d
    ```

The docker images are automatically pulled from the Docker Hub. Database updates are triggered when the application starts or when tests are run.

# Development

## Development environment

For development, the following will be needed:

1. [.NET 5.0](https://dotnet.microsoft.com/download/dotnet/5.0) installed in your environment
2. an instance of PostgreSQL database. Download it from [here](https://www.postgresql.org/download/) or use a [Docker image](https://hub.docker.com/_/postgres)
3. access to an instance of a running [BSV node](https://github.com/bitcoin-sv/bitcoin-sv/releases) with both RPC interface and ZMQ notifications enabled
4. optional Prometheus and Grafana tools

## Building docker images

To run the build script you must have git and docker installed. Get the source code with the git clone command. Build docker images for **MerchantAPI App** by running this command in the folder `src/Deploy/`:

```console
On Linux: ./build.sh
On Windows: build.bat
```
Upon a successful build, a new subfolder `src/Deploy/Build` is created, where the `.env` and `docker-compose.yml` files will be found. The `.env` file must be edited to enable deployment, as described above.

## Set up

Perform the following set up steps:

1. Update `DBConnectionString` (the connection string used by mAPI Reference Implementation), `DBConnectionStringDDL` (the same as DBConnectionString, but with a user that is the owner of the database - it is used to upgrade the database) and `DBConnectionStringMaster` (the same as DBConnectionString, but with a user that has admin privileges - it is used to create the database) settings in `src/MerchantAPI/APIGateway/APIGateway.Rest/appsettings.Development.json` and `src/MerchantAPI/APIGateway/APIGateway.Test.Functional/appsettings.Development.json` so that they point to your PostgreSQL server
2. Update `BitcoindFullPath` in `src/MerchantAPI/APIGateway/APIGateway.Test.Functional/appsettings.Development.json` so that it points to the bitcoind executable used during functional tests
3. Run scripts from `src/MerchantAPI/APIGateway.Database/APIGateway/Database/scripts` to create a database

## Load docker images

Issue this command in the `src/Deploy/Build` folder:
```console
docker load -i merchantapiapp.tar
```

## Run

```console
cd src/MerchantAPI/APIGateway/APIGateway.Rest
dotnet run
```

## Run with **Prometheus** and **Grafana**

To run mAPI reference implementation with Prometheus and Grafana issue this command in the `src/Deploy/Build` folder:
```console
docker-compose -f docker-compose.yml -f docker-compose-dev.yml up
```
NOTE: `docker-compose-dev.yml` will only be created when using the build script in section [Building docker images](#Building_docker_images)

## Test

Run individual tests or run all tests with:

```console
cd src/MerchantAPI/APIGateway/APIGateway.Test.Functional/
dotnet test
```

## Configuration

Set all the environment variables, as described in `Populate all environment variables` above.

The following table lists additional configuration connection strings in docker-compose.yml:

  | Setting | Description |
  | ------- | ----------- |
  | **ConnectionStrings section** |
  | DBConnectionString | connection string for CRUD access to PostgreSQL database |
  | DBConnectionStringDDL | is the same as DBConnectionString, but with a database owner |
  | DBConnectionStringMaster | is the same as DBConnectionString, but with a database owner that has admin privileges (usually postgres) |

## Configuration with standalone database server

mAPI Reference Implementation can be configured to use a standalone Postgres database instead of mapi-db Docker container by updating the following connection strings in docker-compose.yml:

  | Setting | Description |
  | ------- | ----------- |
  | ConnectionStrings:DBConnectionString | connection string to a user who has mapi_crud role granted |
  | ConnectionStrings:DBConnectionStringDDL | connection string to a user who has DDL privileges |

An additional requirement is the existence of a mapi_crud role.

### Example

To execute commands from this example, connect to the database created for mAPI Reference Implementation with admin privileges. 

In this example we will create the mapi_crud role and two user roles. One user role (myddluser) has DDL priveleges and the other (mycruduser), has CRUD privileges.

1. Create pa_crud role
```
  CREATE ROLE "mapi_crud" WITH
    NOLOGIN
    NOSUPERUSER
    INHERIT
    NOCREATEDB
    NOCREATEROLE
    NOREPLICATION;
```

2. Create a DDL user and make it the owner of the public schema
```
  CREATE ROLE myddluser LOGIN
    PASSWORD 'mypassword'
	NOSUPERUSER INHERIT NOCREATEDB NOCREATEROLE NOREPLICATION;

  ALTER SCHEMA public OWNER TO myddluser;
```

3. Create CRUD user and grant mapi_crud role
```
  CREATE ROLE mycruduser LOGIN
    PASSWORD 'mypassword'
    NOSUPERUSER INHERIT NOCREATEDB NOCREATEROLE NOREPLICATION;

  GRANT mapi_crud TO mycruduser;
```


## Configuration with Prometheus

Prometheus is configured to run on http://localhost:9080/.
Check whether endpoints with metrics are healthy on http://localhost:9080/targets. 
Observe the mAPI reference implementation operation on http://localhost:9080/graph.
Data is scraped from https://localhost:5051/metrics every 15s. 
This is where all the metrics that are generated during the execution of mAPI reference implementation are available.

Available metrics include:

|Metric | Description |
| ----------- | ----------- |
| <a name="block_parser"></a>**Block Parser** |
| merchantapi_blockparser_bestblockheight | best block height |
| merchantapi_blockparser_blockparsed_counter | number of parsed blocks |
| merchantapi_blockparser_blockparsingqueue | number of unparsed blocks/blocks in queue for parsing |
| merchantapi_blockparser_blockparsing_duration_seconds | total time spent parsing blocks |
| <a name="transaction_submission"></a>**Transaction Submissions** |
| merchantapi_mapi_any_bitcoind_responding | status 1 if any bitcoind is responding |
| http_requests_received_total{controller="Mapi",action="SubmitTx"} | total number of transactions submitted by client |
| http_requests_received_total{controller="Mapi",action="SubmitTxs"} | total number of batches submitted by client |
| merchantapi_mapi_tx_authenticated_user_counter | total number of transactions submitted by authenticated users |
| merchantapi_mapi_tx_anonymous_user_counter | total number of transactions submitted by anonymous users |
| merchantapi_mapi_tx_sent_to_node_counter | total number of transactions send to node |
| merchantapi_mapi_tx_accepted_by_node_counter | total number of transactions accepted by node |
| merchantapi_mapi_tx_rejected_by_node_counter | total number of transactions rejected by node |
| merchantapi_mapi_tx_submit_exception_counter | total number of transactions with submit exception |
| merchantapi_mapi_tx_response_success_counter | total number of success responses. |
| merchantapi_mapi_tx_response_failure_counter | total number of failure responses |
| merchantapi_mapi_tx_response_failure_retryable_counter | total number of retryable failure responses |
| merchantapi_mapi_tx_missing_inputs_counter | number of sent transactions with missing inputs that were not accepted because of missing inputs |
| merchantapi_mapi_tx_resent_missing_inputs_counter | number of sent transactions with missing inputs that were accepted for which the mAPI had transactions and resubmited those missing inputs |
| merchantapi_mapi_tx_was_mined_missing_inputs_counter | number of sent transactions with missing inputs that were accepted but not sent to the node since they are already mined into the block |
| merchantapi_mapi_tx_invalid_block_missing_inputs_counter | number of sent transactions with missing inputs that were not accepted because they were already mined but into invalid block |
| merchantapi_rpcmulticlient_gettxouts_duration_seconds | total time spent waiting for gettxouts response from node |
| merchantapi_rpcmulticlient_sendrawtxs_duration_seconds | total time spent waiting for sendrawtransactions response from node |
| http_request_duration_seconds_sum{controller="Mapi",action="SubmitTx"} | total response time for client requests - SubmitTx |
| http_request_duration_seconds_sum{controller="Mapi",action="SubmitTxs"} | total response time for client requests - SubmitTxs |
| http_requests_received_total{code=~"5.."} | total number of 5XX errors returned to customer |
| <a name="transaction_callbacks"></a>**Transaction Callbacks** |
| merchantapi_notificationshandler_successful_callbacks_counter | number of successful callbacks |
| merchantapi_notificationshandler_failed_callbacks_counter | number of failed callbacks |
| merchantapi_notificationshandler_callback_duration_seconds | total duration of callbacks (how long did clients take to respond) |
| merchantapi_notificationshandler_notification_in_queue | queued notifications |
| merchantapi_notificationshandler_notification_with_error | notifications with error that are not queued, but processed separately. |
| <a name="mempool_checker"></a>**Mempool Checker** |
| merchantapi_mempoolchecker_successful_resubmit_counter | number of all successful resubmits |
| merchantapi_mempoolchecker_unsuccessful_resubmit_counter | number of all unsuccessful or interrupted resubmits |
| merchantapi_mempoolchecker_exceptions_resubmit_counter| number of resubmits that interrupted with exception |
| merchantapi_mempoolchecker_getrawmempool_duration_seconds | total time spent waiting for getrawmempool response from node |
| merchantapi_mempoolchecker_getmissingtransactions_duration_seconds | database execution total time for the query which transactions must be resubmitted |
| merchantapi_mempoolchecker_min_tx_in_mempool | minumum number of transactions in mempool per node |
| merchantapi_mempoolchecker_max_tx_in_mempool | maximum number of transactions in mempool per node |
| merchantapi_mempoolchecker_tx_missing_counter | number of missing transactions, that are resent to node |
| merchantapi_mempoolchecker_tx_response_success_counter | number of transactions with success response |
| merchantapi_mempoolchecker_tx_response_failure_counter | number of transactions with failure response |
| merchantapi_mempoolchecker_tx_missing_inputs_max_counter | number of transactions that reached MempoolCheckerMissingInputsRetries |

## Configuration with Grafana

mAPI reference implementation is configured to use Prometheus on http://host.docker.internal:9080.
Check Grafana's datasources on http://localhost:3000/datasources. 
There are these predefined dashboards: Block parser, Transactions submission, Callbacks and Mempool checker, which can be accessed at http://localhost:3000/dashboards.
Note: if running mAPI reference implementation on Windows and localhost is unreachable, try accessing 'host.docker.internal' instead.

### Block Parser Dashboard
This dashboard displays statistical data for [Block Parser](#block_parser)

### Transaction Submissions Dashboard
This dashboard displays statistical data for [Transaction Submissions](#transaction_submission)

### Transaction Callbacks Dashboard
This dashboard displays statistical data for [Transaction Callbacks](#transaction_callbacks)

### Mempool Checker Dashboard
This dashboard displays statistical data for [Mempool Checker](#mempool_checker)

| Default credentials |  |
| --------- | ----- |
| Username: | admin |
| Password: | admin |


