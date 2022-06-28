*** Stress program ***

Please check the list of available [command] with:
--help
You can also check the [argument] short info for every command with:
[command] --help

*** How to run ***

Windows:
MerchantAPI.APIGateway.Test.Stress.exe [command] [argument]

Linux:
sudo dotnet ./MerchantAPI.APIGateway.Test.Stress.dll [command] [argument]

Running -send with local node:
It is possible, that antivirus blocks proper start of bitcoind - then you should add exception to your antivirus.

If you have mAPI running in a Docker container on localhost and bitcoind is also running on the same machine, 
you may need to change the docker-compose.yml to make it work.
One way to access bitcoind from mAPI container is to use docker.internal.host (you can also try IP of your machine), another way is to use network_mode: host, see https://www.cloudsavvyit.com/14114/how-to-connect-to-localhost-within-a-docker-container/.
(on Linux you need to provide the following run flag: --add-host=host.docker.internal:host-gateway, check https://stackoverflow.com/questions/48546124/what-is-linux-equivalent-of-host-docker-internal/61001152).

You can override zmqEndpoint set on bitcoind when adding/updating node through REST API, by setting the zmqnotificationsendpoint.
For the local node (from bitcoindConfig) you can set it with -nodeZMQNotificationsEndpoint.

Running -clearDb:
If you want to run clearDb command you must add port mapping for merchant-gateway-database to docker-compose.yml (ports).

*** SendConfig options ***
- filename: File containing transactions to send.
- txIndex: Specifies a zero based index of column that contains hex encoded transaction in a file (default=1).
- skip: Specifies how many transactions should be skipped from the file (default=0).
- limit: Only submit up to the specified number of transactions from transaction file (optional).
- batchSize: Number of transactions submitted in one call (default=100).
- threads: Number of concurrent threads that will be used to submitting transactions. When using multiple threads, make sure that transactions in the file are not dependent on each other (default=1).
- startGenerateBlocksAtTx: Start with transactions submit and block generating when the certain number of transactions is submitted (optional).
- generateBlockPeriodMs: Periodically call generate block on node (default=500). The number of blocks in the database can be different from generate block calls (you can filter blocks inserted to the block table by column blocktime).
- getRawMempoolEveryNTxs: Call RPC GetRawMempool after approximately every N submitted transactions (optional). Results are written to statsMempool.csv. You can test mempoolChecker, if you start stressTool with database, that already contains transactions.
- csvComment: Fill column comment in csv file (optional).
- mapiConfig:
  - authorization: Authorization header used when submitting transactions.
  - mapiUrlURL: Used for submitting transactions. Example: "http://localhost:5000/".
  - rearrangeNodes: Delete local node (from bitcoindConfig) on mAPI (if exists) and add it again if set to true, otherwise user has to take care for it by himself.
  - addFeeQuotesFromJsonFile: Add feeQuotes from file (optional).
  - nodeHost: Use if local node (the one from the bitcoindConfig) is unreachable from mAPI (override default "127.0.0.1").
  - nodeZMQNotificationsEndpoint: Use when you need to override local node's default zmqEndpoint.
  - callback: Specify, if you want to trigger callbacks
    - url: Url that will process double spend and merkle proof notifications. When present, transactions will be submitted with MerkleProof and DsCheck set to true. Example: "http://localhost:2000/callbacks".
    - addRandomNumberToPort: When specified, a random number between 1 and  AddRandomNumberToPort will be appended to host port specified in Url when submitting each batch of transactions. This is useful for testing callbacks toward different hosts.
    - callbackToken: Full authorization header that mAPI should use when performing callbacks.
    - callbackEncryption: Encryption parameters used when performing callbacks.
    - startListener: Start a listener that will listen to callbacks on port specified by Url. When specified, error will be reported if not all callbacks are received.
    - idleTimeoutMS: Maximum time in milliseconds that we are willing to wait for the next callbacks - we wait until all callbacks are received or until timeout expires (default=30000).
    - hosts: 
      - hostName: Name of host to which configuration applies to. Use empty string for default setting.
      - minCallbackDelayMs: For slow host increase value.
      - maxCallbackDelayMs: For slow host increase value.
      - callbackFailurePercent: Set 0 for host that never returns errors.
 - bitcoindConfig: Specify, if you want to run bitcoind locally
    - bitcoindPath: Full path to bitcoind executable. Used when starting new node if -templateData is specified.
    - templateData: Template directory containing a snapshot of a data directory that will be used as the initial state of a new node that will be started up. If specified, then mapiAdminAuthorization must also be specified.
    - mapiAdminAuthorization: Full authorization header used for accessing mApi admin endpoint. The admin endpoint is used to automatically register bitcoind with mAPI. 
    - zmqEndpointIp: Override default "127.0.0.1" zmqEndpoint ip.