# mAPI Reference Implementation

Readme v.1.4.0.

The details of the BRFC mAPI Specification are available in [BRFC mAPI Specification](https://github.com/bitcoin-sv-specs/brfc-merchantapi).  

> The golang (v1.1) implementation is no longer being maintained and has been moved to the [golang-v1.1 branch](https://github.com/bitcoin-sv/merchantapi-reference/tree/golang-v1.1).

## Swagger UI

The REST API can also be seen in the [Swagger UI](https://bitcoin-sv.github.io/merchantapi-reference).

## Support

For support and general discussion of both the mAPI standard and the reference implementation, please join the following [telegram group](https://t.me/joinchat/JB6ZzktqwaiJX_5lzQpQIA).

## Requirements

mAPI requires access to Bitcoin SV node version 1.0.10 or newer. See [Managing nodes](#Managing-nodes) for details how to connect to a bitcoin node.

For running in production, use Docker. Docker images can be [downloaded](#Download-docker-images) from docker hub, or created as part of the [build](#Building-docker-images). See [Deploying docker images](#Deploying-docker-images) for details on how to run them.

A SSL server certificate is required for installation. You can obtain the certificate from your IT support team. There are also services that issue free SSL certificates, such as letsencrypt.org.  The certificate must be issued for the host with a fully qualified domain name. To use the server side certificate, you need to export it (including its corresponding private key) in PFX file format (*.pfx).

For setting up a development environment see [development](#Development).

## REST API Interfaces

The reference implementation exposes different **REST API** interfaces:

* a public interface for submitting and querying transactions
* an administrator interface for managing connections to bitcoin nodes and policy quotes

It also provides a JWT Manager to enable authenticated users to obtain special policy rates.

## Public Interface

The public interface can be used to submit transactions and query transaction status. It is accessible to both authenticated and unauthenticated users, but authenticated users might get special fee rates.

The endpoints are implemented in accordance with the BRFC mAPI Specification and are summarised below:

### 1. Get Policy Quote

```
GET /mapi/policyQuote
```

Responds with a policy quotation. This is a superset of the fee quotation (below).

#### Special Policy Quotes

The administrator may wish to offer special policy quotes to specific customers. The reference implementation supports JSON Web Tokens (JWT) issued to authenticated users. The authenticated users include the JWT in their HTTP header and, as a result, receive special policy quotes.

If no JWT is supplied, then the call is anonymous (unauthenticated), and the default policy quote is supplied. If a JWT (created by the mAPI JWT Manager user or other JWT provider) is supplied, then the caller will receive the corresponding special policy quote. For this version of the merchant API reference implementation, the JWT must be created by the JWT manager and issued to the customer manually.

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
GET /mapi/tx/{hash:[0-9a-fA-F]+}
```

Responds with the status of the transaction.

### 5. Submit Multiple Transactions

```
POST /mapi/txs
```

This is similar to Submit Transaction, but an array of transactions may be sent.


## Administrator Interface

The Administrator Interface of the reference implementation manages policy quotes, nodes and special policy fee rates for the Public API.

These services are only available to the administrator. Authentication is performed through the Api-Key HTTP header. The value provided must match the one stored in the configuration variable `RestAdminAPIKey`.


### Managing Policy Quotes

Policy Quotes may be specified for the unauthenticated users, or specific authenticated users.

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
                  \"miningFee\": \
                     { \"satoshis\": 100, \"bytes\": 200 }, \
                  \"relayFee\": \
                     { \"satoshis\": 100, \"bytes\": 200 } }, \
               { \"feeType\": \"data\", \
                  \"miningFee\": \
                     { \"satoshis\": 100, \"bytes\": 200 }, \
                  \"relayFee\": \
                     { \"satoshis\": 100, \"bytes\": 200 } \
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
                \"dustrelayfee\": 150, \
                \"maxstdtxvalidationduration\": 99, \
                \"maxnonstdtxvalidationduration\": 100, \
                \"minconsolidationfactor\": 10, \
                \"maxconsolidationinputscriptsize\": 100, \
                \"minconfconsolidationinput\": 10, \
                \"acceptnonstdconsolidationinput\": false, \
                \"dustlimitfactor\": 500 \
            } \
          }"
```

The parameters above are:
    | Parameter | Description |
    | ----------- | ----------- |
    | validFrom | the timestamp from when the policy is valid. Only one policy should be valid for each identity (or the anonymous user) at any one time |
    | identity | the identity of the user, or null for the anonymous user |
    | identityProvider | the identity of the JWT authority, or null for the anonymous user |
    | fees | fees charged by the miner (see [feeSpec BRFC](https://github.com/bitcoin-sv-specs/brfc-misc/tree/master/feespec)) |
    | callbacks | IP addresses of DSNT servers (see [specification](https://github.com/bitcoin-sv-specs/protocol/blob/master/updates/double-spend-notifications.md)) such as this mAPI reference implementation |
    | policies | values of miner policies as configured by the administrator (below) |

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


To get a specific policy quote by `identity` use:

```
GET api/v1/PolicyQuote/{identity}
```

Note: it is not possible to delete or update a policy quote once it is published, but it can be made obsolete by publishing a new policy quote.

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

For each transaction that is submitted to mAPI it is possible for the submitter to include a DSNT output in the transaction according to the [DSNT specification](https://github.com/bitcoin-sv-specs/protocol/blob/master/updates/double-spend-notifications.md).
The submitter may later receive a notification of a double spend or merkle proof via a callback URL that they included with the submit transaction request.
mAPI processes all requested notifications and sends them out as described below:

* all notifications are sent out in batches
* each batch contains a limited number of notifications for a single host (configurable with `NOTIFICATION_MAX_NOTIFICATIONS_IN_BATCH`)
* the response time for each host is tracked and two separate pools of tasks are used for delivering notifications: One pool for fast hosts and a second pool for slow hosts. (The threshold for slow/fast hosts can be configured with `NOTIFICATION_SLOW_HOST_THRESHOLD_MS`)
* when an event is received from the node, an attempt is made to insert a notification into the queue for instant delivery
* if a callback fails or if the instant delivery queue is full, the notification is scheduled for delivery in the background
* a background delivery queue is used for periodically processing failed notifications. A single task is used for background delivery

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
    | CALLBACK_IP_ADDRESSES | An array of DSNT server IP addresses, separated by commas, which are sent to the merchant in response to GET PolicyQuote |
    | HTTPSPORT | Https port where the application will listen/run |
    | CERTIFICATEPASSWORD | password of the *.pfx file in the config folder |
    | CERTIFICATEFILENAME | *<certificate_file_name.pfx>* |
    | QUOTE_EXPIRY_MINUTES | policy quote expiry period |
    | ZMQ_CONNECTION_TEST_INTERVAL_SEC | How frequently the ZMQ subscription service tests that the connection with the node is still alive. Default: 60 seconds |
    | RESTADMIN_APIKEY | Authorization key for accessing administration interface |
    | DELTA_BLOCKHEIGHT_FOR_DOUBLESPENDCHECK | Number of old blocks that are checked for double spends |
    | CLEAN_UP_TX_AFTER_DAYS | Number of days transactions and blocks are kept in the database. Default: 3 days |
    | CLEAN_UP_TX_PERIOD_SEC | Time period of transactions cleanup check. Default: 1 hour |
    | CHECK_FEE_DISABLED | Disable fee check |
    | WIF_PRIVATEKEY | Private key that is used to sign responses (must be omitted if miner ID settings are specified, and vice versa) |
    | DS_HOST_BAN_TIME_SEC | See Banning Persistent Hosts below |
    | DS_MAX_NUM_OF_TX_QUERIES | See Banning Persistent Hosts below |
    | DS_CACHED_TX_REQUESTS_COOLDOWN_PERIOD_SEC | See Banning Persistent Hosts below |
    | DS_MAX_NUM_OF_UNKNOWN_QUERIES | Maximum number of queries for unknown transaction ids allowed before a host will become banned |
    | DS_UNKNOWN_TX_QUERY_COOLDOWN_PERIOD_SEC | How long unknown transactions queries will be stored, before being discarded |
    | DS_SCRIPT_VALIDATION_TIMEOUT_SEC | Total time for script validation when nodes RPC method verifyScript will be called |
    | ENABLEHTTP | Enables requests through HTTP when set to True. This should only be used for testing and must be set to False in the production environment in order to maintain security. |
    | HTTPPORT | HTTP port where the application will listen/run. Default: port 80 |
    | NOTIFICATION_NOTIFICATION_INTERVAL_SEC | Period when the background service will retry sending notifications with an error |
    | NOTIFICATION_INSTANT_NOTIFICATION_TASKS | Maximum number of concurrent tasks for sending notifications to callback endpoints (must be between 2-100) |
    | NOTIFICATION_INSTANT_NOTIFICATIONS_QUEUE_SIZE | Maximum number of notifications waiting in the instant queue before any new notifications will be scheduled for (slower) background delivery |
    | NOTIFICATION_MAX_NOTIFICATIONS_IN_BATCH | Maximum number of notifications per host being processed by the delivery task at any one time |
    | NOTIFICATION_SLOW_HOST_THRESHOLD_MS | Callback response time threshold that determines which host is deemed slow or fast |
    | NOTIFICATION_INSTANT_NOTIFICATIONS_SLOW_TASK_PERCENTAGE | Percent of notification tasks from NOTIFICATION_INSTANT_NOTIFICATION_TASKS that will be reserved for slow hosts |
    | NOTIFICATION_NO_OF_SAVED_EXECUTION_TIMES | Maximum number of callback response times saved for each host. Used for calculating the average response time for a host |
    | NOTIFICATION_NOTIFICATIONS_RETRY_COUNT | Number of retries for failed notifications, before abandoning retries |
    | NOTIFICATION_SLOW_HOST_RESPONSE_TIMEOUT_MS | Callback response timeout for slow host |
    | NOTIFICATION_FAST_HOST_RESPONSE_TIMEOUT_MS | Callback response timeout for fast host |
    | MINERID_SERVER_URL | URL pointing to MinerID REST endpoint |
    | MINERID_SERVER_ALIAS | Alias to be used when communicating with the endpoint |
    | MINERID_SERVER_AUTHENTICATION | HTTP authentication header that will be used when communicating with the endpoint, this should include the `Bearer` keyword, for example `Bearer 2b4a73....183df9` |

#### Banning Persistent Hosts

DS_CACHED_TX_REQUESTS_COOLDOWN_PERIOD_SEC is how long the count of requests (queries or submits) for the same transaction id per host is accumulated, before being reset to 0.
If the request count for the same transaction Id exceeds DS_MAX_NUM_OF_TX_QUERIES during this period, the host will be banned and removed from the whitelist. The host will have to desist from sending requests for the same transaction id for at least the cool-down period DS_HOST_BAN_TIME_SEC, before it will become acceptable (un-banned) and can successfully try again.

5. Run this command in the target folder to start the mAPI application:

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

## Building docker images

To run the build script you must have git and docker installed. Get the source code with the git clone command. Build docker images for **MerchantAPI App** by running this command in the folder `src/Deploy/`:

```console
On Linux: ./build.sh
On Windows: build.bat
```
Upon a successful build, a new subfolder `src/Deploy/Build` is created, where the `.env` and `docker-compose.yml` files will be found. The `.env` file must be edited to enable deployment, as described above.

## Set up

Perform the following set up steps:

1. Update `DBConnectionString` (the connection string used by mAPI), `DBConnectionStringDDL` (the same as DBConnectionString, but with a user that is the owner of the database - it is used to upgrade the database) and `DBConnectionStringMaster` (the same as DBConnectionString, but with a user that has admin privileges - it is used to create the database) settings in `src/MerchantAPI/APIGateway/APIGateway.Rest/appsettings.Development.json` and `src/MerchantAPI/APIGateway/APIGateway.Test.Functional/appsettings.Development.json` so that they point to your PostgreSQL server
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

## Test

Run individual tests or run all tests with:

```console
cd src/MerchantAPI/APIGateway/APIGateway.Test.Functional/
dotnet test
```

## Configuration

The following table lists all the configuration settings with mappings to the environment variables. For a description of each setting see `Populate all environment variables` above.

  | Application Setting | Environment variable |
  | ------------------- | -------------------- |
  | QuoteExpiryMinutes | QUOTE_EXPIRY_MINUTES |
  | RestAdminAPIKey | RESTADMIN_APIKEY |
  | DeltaBlockHeightForDoubleSpendCheck | DELTA_BLOCKHEIGHT_FOR_DOUBLESPENDCHECK |
  | CleanUpTxAfterDays| CLEAN_UP_TX_AFTER_DAYS |
  | CleanUpTxPeriodSec| CLEAN_UP_TX_PERIOD_SEC |
  | WifPrivateKey | WIF_PRIVATEKEY |
  | ZmqConnectionTestIntervalSec | ZMQ_CONNECTION_TEST_INTERVAL_SEC |
  | **Notification section**|
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
  | **MinerIdServer section** |
  | Url | MINERID_SERVER_URL |
  | Alias | MINERID_SERVER_ALIAS |
  | Authentication | MINERID_SERVER_AUTHENTICATION |

The following table lists additional configuration settings:

  | Setting | Description |
  | ------- | ----------- |
  | **ConnectionStrings section** |
  | DBConnectionString | connection string for CRUD access to PostgreSQL database |
  | DBConnectionStringDDL | is the same as DBConnectionString, but with a user that is owner of the database |
  | DBConnectionStringMaster | is the same as DBConnectionString, but with a user that has admin privileges (usually postgres) |

## Configuration with standalone database server

mAPI can be configured to use a standalone Postgres database instead of mapi-db Docker container by updating the following connection strings in docker-compose.yml:

  | Setting | Description |
  | ------- | ----------- |
  | ConnectionStrings:DBConnectionString | connection string to a user that has mapi_crud role granted |
  | ConnectionStrings:DBConnectionStringDDL | connection string to a user that has DDL privileges |

An additional requirement is the existence of a mapi_crud role.

## Example

To execute commands from this example, connect to the database created for mAPI with admin privileges. 

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

