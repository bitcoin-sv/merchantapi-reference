*** Smoke program ***

Please check the list of available [command] with:
--help
You can also check the [argument] short info for every command with:
[command] --help

*** How to run ***

Windows:
MerchantAPI.APIGateway.Test.SmokeTest.exe [command] [argument]

Linux:
sudo dotnet ./MerchantAPI.APIGateway.Test.SmokeTest.dll [command] [argument]

Running findutxos configFileName
    Returns top 10 UTxO's and saves a whole list in utxos.csv file. The file contains TxId, Vout, Address, Amount and ScriptPubKey.

    configFileName - path to Smoke configuration file


Running submittxs uTxId outputsNum chainLength unconfirmedAncestor batchSize configFileName
    Creates transactions based on outputsNum and chainLength where unspent outputs are taken from uTxId. To test unconfirmed parent(s) then chainLength must be greater then 1.
    After all transactions are generated, they are then sent to mapi whether transaction by transaction (through mapi/tx), or send in batches if batchSize is greater then 1 (through txs).

    uTxId - Id of an unspent transaction.
    numOfOutputs - Number of outputs.
    chainLength - Length of a chain of transactions [0-n].
    unconfirmedAncestor - Test unconfirmed ancestor [0-1].
    batchSize - call tx if 1 (send tx by tx), otherwise call txs (send in batches).
    configFileName - path to SmokeConfig configuration file.


Running generate n configFileName
    Generate next n block(s).

    configFileName - path to Smoke configuration file


*** SmokeConfig options ***
- node:
    - host: Node name or IP to register to MAPI and execute RPCs 
    - port: RPC port
    - username: RPC username
    - password: RPC password
    - ZMQ: ZMQ notifications endpoint

- mapiConfig:
    - mapiUrl: Used for registrating node and submitting transactions. Example: "http://localhost:5000/".
    - adminauthorization: API-Key used for accessing mApi admin endpoint. The admin endpoint is used to automatically register bitcoind with mAPI. 

- callback: 
    - url: Url that will process double spend and merkle proof notifications.
    - token: Full authorization header that mAPI should use when performing callbacks.
    - encryption: Encryption parameters used when performing callbacks.
    - merkleProof: Send Merkel proof (true/false).
    - merkleFormat: Merkel proof format (default is TSC).
    - dsCheck: Check if double spent atempt (true/false).
    