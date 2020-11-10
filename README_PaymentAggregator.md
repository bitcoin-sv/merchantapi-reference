# mAPI

More details available in the [BRFC Spec](https://github.com/bitcoin-sv-specs/brfc-merchantapi) for Merchant API.  

### [Swagger UI](https://bitcoin-sv.github.io/merchantapi-reference)  

## Support

For support and general discussion of both standards and reference implementations please join the following [telegram group](https://t.me/joinchat/JB6ZzktqwaiJX_5lzQpQIA).

## Requirements

TODO: remove this? mAPI requires access to Bitcoin SV node version 1.0.6 or newer.

For running in production, you should should use Docker. Docker images are created as part of the [build](#build-and-deploy). See [Deploying docker images](#Deploying-docker-images) for details how to run them.

A SSL server certificate is required for installation. You can obtain the certificate from your IT support team. There are are also services that issue free SSL certificates such as letsencrypt.org.  The certificate must be issued for the host with fully qualified domain name. To use the server side certificate, you need to export it (including corresponding private key) it in PFX file format (*.pfx).

For setting up development environment see [bellow](#setting-up-a-development-environment)


## REST interface

The reference implementation exposes different **REST API** interfaces

* an interface for TODO: update... submitting transactions implemented according [BRFC Spec](https://github.com/bitcoin-sv-specs/brfc-merchantapi)
* an admin interface for TODO: update... managing connections to bitcoin nodes and fee quotes


## Public interface

Public interface can be used to submit transactions and query transactions status. It is accessible only to authenticated users.
TODO: check and update entire section...

### 1. getAllFeeQuotes

Get all fee quotes from miners running mAPI together with calculated SLA-s. Authenticated user must have an active subscription with serviceType `allfeequotes`.

```
GET /api/v1/allfeequotes
```

### 2. submitTransaction

```
POST /api/v1/tx
```

To submit a transaction in JSON format use `Content-Type: application/json` with the following request body:

```json
{
  "rawtx":        "[transaction_hex_string]",
  "callbackUrl":  "https://your.service.callback/endpoint",
  "callbackToken" : "<your_authorization_header>",
  "callbackEncryption": "<ecription_parameters>",
  "merkleProof" : true,
  "dsCheck" : true
}
```

To submit transaction in binary format use `Content-Type: application/octet-stream` with the binary serialized transaction in the request body. You can specify `callbackUrl`, `callbackToken`, `callbackEncryption`, `merkleProof` and `dsCheck` in the query string.

### 3. submitTransactions


```
POST /api/v1/txs
```

To submit a list of transactions in JSON format use `Content-Type: application/json` with the following request body:

```json
[
  {

    "rawtx":        "[transaction_hex_string]",
    "callbackUrl":  "https://your.service.callback/endpoint",
    "callbackToken" : "<your_authorization_header>",
    "callbackEncryption": "<ecription_parameters>",
    "merkleProof" : true,
    "dsCheck" : true
  },
  ....
]
```

To submit transactions in binary format use `Content-Type: application/octet-stream` with the binary serialized transactions in the request body. Use query string to specify the remaining parameters.

### 4. queryTransactionStatus

Authenticated user must have an active subscription with serviceType `querytx`.

```
GET /api/v1/tx/{hash:[0-9a-fA-F]+}
```

### Managing subscriptions

To create a new subscription use:

``` HTTP
POST /api/v1/account/subscription
```

To create a new subscription, JSON request with following body must be used:

``` JSON
{
  "serviceType": "[ServiceType]"
}
```

To get a list of all active subscriptions for a specified account use:

``` HTTP
GET /api/v1/account/subscription
```

To get a list of all subscriptions (including inactive ones) use:

``` HTTP
GET /api/v1/account/subscription?onlyActive=false
```

To get specific subscription use following:

``` HTTP
GET /api/v1/account/subscription/{subscriptionId}
```

To cancel a subscription use:

``` HTTP
DELETE /api/v1/account/subscription/{subscriptionId}
```

## Admin interface  

Admin interface can be used to add, update or remove connections to this node. It is only accessible to authenticated users. Authentication is performed through `Api-Key` HTTP header. The provided value must match the one provided in configuration variable `RestAdminAPIKey`.

### Managing gateways

To create a new gateway use the following:

```
POST api/v1/Gateway
```


Example with curl - add gateway that will be disabled from 01/10/2021:

```console
$ curl -H "Api-Key: [RestAdminAPIKey]" -H "Content-Type: application/json" -X POST https://localhost:5052/api/v1/Gateway -d "{ \"url\": \"https://host:port/\", \"minerRef\": \"minerRef\", \"email\": \"email\", \"organisationName\": \"organisationName\", \"contactFirstName\": \"contactFirstName\", \"contactLastName\": \"contactLastName\", \"remarks\": \"remarks\", \"disabledAt\": \"2021-10-01T00:00:00\" }"
```

To update parameters for an existing gateway use:

```
PUT api/v1/Gateway/{gatewayId}
```

To update gateway created with curl before, so that it will be enabled again (or if still active - to cancel disabling it) use `Content-Type: application/json` and authorization `Api-Key: [RestAdminAPIKey]` with the following JSON request body:

```json
{
    "url": "[https://host:port/]",
    "minerRef": "[minerRef]",
    "email": "[email]",
    "organisationName": "[organisationName]",
    "contactFirstName": "[contactFirstName]",
    "contactLastName": "[contactLastName]",
    "remarks": "[remarks]",
    "disabledAt": null
}
```

You can update all fields except `id` and `createdAt`.

To remove gateway use:

```bash
DELETE api/v1/Gateway/{gatewayId}
```

To get list of all gateways, matching one or more criterias use the following

```
GET api/v1/Gateway
```

You can filter gateways by providing additional optional criteria in query string:

* `onlyActive` - return only gateways that are currently active

To get list of all gateways (including disabled ones) use GET api/v1/Gateway without filters.


To get a specific gateway by id use:

```
GET api/v1/Gateway/{gatewayId}
```

### Managing Accounts

To create a new account use:

``` HTTP
POST api/v1/account
```

To create a new account, JSON request with following body must be sent:

``` JSON
{
  "organisationName": "[Organisation]",
  "contactFirstName": "[FirstName]",
  "contactLastName": "[LastName]",
  "email": "[Email]",
  "identity": "[Identity]",
  "identityProvider": "[IdentityProvider]"
}
```

To update account parameters for an existing account use following endpoint with same structure that is used for create:

``` HTTP
PUT api/v1/account/{accountId}
```

To get a list of all accounts use:

``` HTTP
GET api/v1/account/
```

To get only a specific account use:

``` HTTP
GET api/v1/account/{accountId}
```

### Managing service levels

To create a new group of service levels use:

```
POST api/v1/ServiceLevel
```

To create two service levels, JSON request with following body must be sent:

```json
{
    "serviceLevels": [
        {
            "level": 0,
            "description": "[description0]",
            "fees": [
                {
                    "feeType": "standard",
                    "miningFee": {
                            "satoshis": [satoshis],
                            "bytes": [bytes]
                    },
                    "relayFee": {
                            "satoshis": [satoshis],
                            "bytes": [bytes]
                    }
                },
                {
                    "feeType": "data",
                    "miningFee": {
                            "satoshis": [satoshis],
                            "bytes": [bytes]
                    },
                    "relayFee": {
                            "satoshis": [satoshis],
                            "bytes": [bytes]
                    }
                }
            ]
        },
        {
            "level": 1,
            "description": "[description1]",
            "fees": null
        }
    ]
}
```

Note: the service level with the highest level must have fees equal to null.

You can not update or delete service levels, you can only POST new.

To get list of active service levels use:

```
GET api/v1/ServiceLevel
```

## Build and deploy

### Building docker images

Build docker images for **PaymentAggregator App & Data**  running this commands in folder `/src/Deploy/PaymentAggregator`

```bash
On Linux: ./build.sh
On Windows: build.bat
```

### Deploying docker images
  
1. Create `config` folder and save SSL server certificate file (*<certificate_file_name>.pfx*) into to the `config` folder. This server certificate is required to setup TLS (SSL).
2. Copy .crt files with root and intermediate CA certificates that issued SSL server certificates which are used by callback endpoint. Each certificate must be exported as a **Base-64 encoded X.509** file with a crt extension type. This step is required if callback endpoint uses SSL server certificate issued by untrusted CA (such as self signed certificate).
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
    | RESTADMIN_APIKEY | Authorization key for accessing administration interface |
    | CLEAN_UP_SERVICE_REQUEST_AFTER_DAYS | Number of days service requests are kept in database. Default: 30 days |
    | CLEAN_UP_SERVICE_REQUEST_PERIOD_SEC | Time period of service requests cleanup check. Default: 1 hour |

5. Run this command in target folder to start mAPI application:

    ```bash
    docker-compose up -d
    ```

The docker images are automatically pulled from Docker Hub. Database updates are triggered upon application start or when tests are run.

# Setting up a development environment

For development, you will need the following

1. [.NET core SDK 3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) installed in your environnement.
2. and instance of PostgreSQL database. You can download it from [here](https://www.postgresql.org/download/) or use a [Docker image](https://hub.docker.com/_/postgres).

Perform the following set up steps:

1. Update `DBConnectionString`(connection string used by mAPI) and `DBConnectionStringMaster` (same as DBConnectionString, but with user that has admin privileges - is used to upgrade database) setting in `src/MerchantAPI/PaymentAggregator/PaymentAggregator.Rest/appsettings.Development.json` and `src/MerchantAPI/PaymentAggregator/PaymentAggregator.Test.Functional/appsettings.Development.json` so that they point to your PostgreSQL server
2. Run scripts from `src/crea/merchantapi2/src/MerchantAPI/PaymentAggregator.Database/PaymentAggregator/Database/scripts` to create database.


## Run

```console
cd src/MerchantAPI/PaymentAggregator/PaymentAggregator.Rest
dotnet run
```

## Test

Run individual tests or run all tests with:

```console
cd src/MerchantAPI/PaymentAggregator/PaymentAggregator.Test.Functional/
dotnet test
```

## Configuration

Following table lists all configuration settings with mappings to environment variables. For description of each setting see `Populate all environment variables` under **Deploying docker images**

  | Application Setting | Environment variable |
  | ------------------- | -------------------- |
  | RestAdminAPIKey | RESTADMIN_APIKEY |
  | CleanUpServiceRequestAfterDays | CLEAN_UP_SERVICE_REQUEST_AFTER_DAYS |
  | CleanUpServiceRequestPeriodSec | CLEAN_UP_SERVICE_REQUEST_PERIOD_SEC |


Following table lists additional configuration settings:

  | Setting | Description |
  | ------- | ----------- |
  | **PaymentAggregatorConnectionStrings** region |
  | DBConnectionString | connection string for access to PostgreSQL database |
  | DBConnectionStringMaster | is same as DBConnectionString, but with user that has admin privileges |
