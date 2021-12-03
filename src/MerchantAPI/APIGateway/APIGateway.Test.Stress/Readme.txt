*** Stress program ***

How to run

Windows:
MerchantAPI.APIGateway.Test.Stress.exe send sendConfig.json
(It is possible, that antivirus blocks proper start of bitcoind.exe - then you should add exception to your antivirus.)

Linux:
sudo dotnet ./MerchantAPI.APIGateway.Test.Stress.dll send sendConfig.json

If you have mAPI running in container on localhost and bitcoind also running on the same machine, 
you may need to change the docker-compose.yml to make it work.
One way to access bitcoind from mAPI container is to use docker.internal.host (you can also try IP of your machine), another way is to use network_mode: host, see https://www.cloudsavvyit.com/14114/how-to-connect-to-localhost-within-a-docker-container/.
(on Linux you need to provide the following run flag: --add-host=host.docker.internal:host-gateway, check https://stackoverflow.com/questions/48546124/what-is-linux-equivalent-of-host-docker-internal/61001152).

Also if you want to run -truncateTables you must add port mapping for merchant-gateway-database (ports).

You can override zmqEndpoint set on bitcoind when adding/updating node through REST API, by setting the zmqnotificationsendpoint.
For the local node (from bitcoindConfig) you can set it with -bitcoindZmqEndpointIp.

SendConfig options
- filename: File containing transactions to send.
- txIndex: Specifies a zero based index of column that contains hex encoded transaction in a file.
- limit: Only submit up to specified number of transactions from transaction file.
- batchSize: Number of transactions submitted in one call.
- threads: Number of concurrent threads that will be used to submitting transactions. When using multiple threads, make sure that transactions in the file are not dependent on each other.
- csvComment: Fill column comment in csv file (optional).
- mapiConfig:
  - authorization: Authorization header used when submitting transactions.
  - mapiUrlURL: Used for submitting transactions. Example: "http://localhost:5000/".
  - truncateTables: If true, truncate data in all tables (except feeQuote) and stops program. After this restart of mAPI is needed to reinitialize cache.
  - mapiDBConnectionString: Required if -truncateTables is true.
  - rearrangeNodes: Delete local node (from bitcoindConfig) on mAPI (if exists) and add it again / if false user has to take care for it by himself.
  - bitcoindHost: Use if local node (from bitcoindConfig) is unreachable from mAPI (override default "127.0.0.1").
  - bitcoindZmqEndpointIp: Use when you need to override local node's default zmqEndpointIp.
  - callback: Specify, if you want to trigger callbacks
    - url: Url that will process double spend and merkle proof notifications. When present, transactions will be submitted with MerkleProof and DsCheck set to true. Example: "http://localhost:2000/callbacks".
    - addRandomNumberToHost: When specified, a random number between 1 and  AddRandomNumberToHost will be appended to host name specified in Url when submitting each batch of transactions. This is useful for testing callbacks toward different hosts.
    - callbackToken: Full authorization header that mAPI should use when performing callbacks.
    - callbackEncryption: Encryption parameters used when performing callbacks.
    - startListener: Start a listener that will listen to callbacks on port specified by Url. When specified, error will be reported if not all callbacks are received.
    - hosts: 
      - hostName: Name of host to which configuration applies to. Use empty string for default setting.
      - minCallbackDelayMs: For slow host increase value.
      - maxCallbackDelayMs: For slow host increase value.
      - callbackFailurePercent: Set 0 for host that never returns errors.
 - bitcoindConfig: Specify, if you want to run bitcoind locally
    - bitcoindPath: Full path to bitcoind executable. Used when starting new node if -templateData is specified.
    - templateData: Template directory containing snapshot of data directory that will be used as initial state of new node that is started up. If specified mapiAdminAuthorization must also be specified.
    - mapiAdminAuthorization: Full authorization header used for accessing mApi admin endpoint. The admin endpoint is used to automatically register bitcoind with mAPI. 
    - zmqEndpointIp: Override default "127.0.0.1" zmqEndpoint ip.