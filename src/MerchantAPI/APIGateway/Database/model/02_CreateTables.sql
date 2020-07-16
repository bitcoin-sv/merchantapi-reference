
CREATE TABLE Node (
         nodeId              SERIAL         NOT NULL,
         host                VARCHAR(50)    NOT NULL,
         port                INT            NOT NULL,
         username            VARCHAR(50)    NOT NULL,
         password            VARCHAR(50)    NOT NULL,
         Remarks             VARCHAR(1024),
         nodeStatus          INT            NOT NULL,
         lastError           VARCHAR(256),
         lastErrorAt         TIMESTAMP,

         PRIMARY KEY (nodeId)    
);

ALTER TABLE Node ADD CONSTRAINT node_hostAndPort UNIQUE (host,port);

CREATE TABLE Tx (
		txInternalId		BIGSERIAL		NOT NULL,
		txExternalId		BYTEA			NOT NULL,
		txPayload			BYTEA			NOT NULL,
		
		receivedAt			TIMESTAMP,
		callbackUrl			VARCHAR(1024),
		callbackToken		VARCHAR(256),
		callbackEncryption  VARCHAR(1024),
		merkleProof			BOOLEAN			NOT NULL,
		dsCheck				BOOLEAN			NOT NULL,
		
		PRIMARY KEY (txInternalId)
);

ALTER TABLE Tx ADD CONSTRAINT tx_txExternalId UNIQUE (txExternalId);

CREATE TABLE Block (
		blockInternalId		BIGSERIAL		NOT NULL,
		blockTime			TIMESTAMP		NOT NULL,
		blockHash			BYTEA			NOT NULL,
		prevBlockHash		BYTEA			NOT NULL,
	
		blockHeight			BIGINT,
		onActiveChain		BOOLEAN			NOT NULL,
		parsedForMerkleAt	TIMESTAMP,
		parsedForDSAt		TIMESTAMP,
		
		PRIMARY KEY(blockInternalId)
);

ALTER TABLE Block ADD CONSTRAINT block_blockhash UNIQUE (blockHash);

CREATE TABLE TxMempoolDoubleSpendAttempt (
		txInternalId		BIGINT			NOT NULL,
		dsTxId				BYTEA			NOT NULL,
		dsTxPayload			BYTEA			NOT NULL,
		
		sentDsNotificationAt TIMESTAMP,
		
		PRIMARY KEY (txInternalId, dsTxId),
		FOREIGN KEY (txInternalId) REFERENCES Tx(txInternalId)
);

CREATE TABLE TxBlockDoubleSpend (
		txInternalId 		BIGINT			NOT NULL,
		blockInternalId 	BIGINT			NOT NULL,
		dsTxId				BYTEA			NOT NULL,
		dsTxPayload			BYTEA			NULL,
		
		sentDsNotificationAt TIMESTAMP,
		
		PRIMARY KEY (txInternalId, blockInternalId, dsTxId),
		FOREIGN KEY (txInternalId) REFERENCES Tx(txInternalId),
		FOREIGN KEY (blockInternalId) REFERENCES Block(blockInternalId)
);

CREATE TABLE TxBlock (
		txInternalId 		BIGINT			NOT NULL,
		blockInternalId 	BIGINT			NOT NULL,
		
		sentMerkleProofAt TIMESTAMP,

		PRIMARY KEY (txInternalId, blockInternalId),
		FOREIGN KEY (txInternalId) REFERENCES Tx(txInternalId),
		FOREIGN KEY (blockInternalId) REFERENCES Block(blockInternalId)
);

CREATE TABLE TxInput (
		txInternalId 		BIGINT			NOT NULL,
		n					BIGINT			NOT NULL,
		prevTxId			BYTEA			NOT NULL,
		prev_n				BIGINT			NOT NULL,
		
		PRIMARY KEY (txInternalId, n),
		FOREIGN KEY (txInternalId) REFERENCES Tx(txInternalId)
);


CREATE TABLE FeeQuote (
  id                        SERIAL          NOT NULL,
  createdAt                 TIMESTAMP       NOT NULL,
  validFrom                 TIMESTAMP       NOT NULL,
  identity                  VARCHAR(256),
  identityProvider          VARCHAR(256),

  PRIMARY KEY (id)
);

ALTER TABLE FeeQuote ADD CONSTRAINT feeQuote_validFrom_vs_createdAt CHECK (createdAt <= validFrom);


CREATE TABLE Fee (
  id                        SERIAL          NOT NULL,
  feeQuote                  BIGINT          NOT NULL,
  feeType                   VARCHAR(256)    NOT NULL,

  PRIMARY KEY (id),
  FOREIGN KEY (feequote) REFERENCES FeeQuote (id)
);

CREATE TABLE FeeAmount (
  id                        SERIAL          NOT NULL,
  fee                       BIGINT          NOT NULL,
  satoshis                  INT             NOT NULL,
  bytes                     INT             NOT NULL,

  PRIMARY KEY (id),
  FOREIGN KEY (fee) REFERENCES Fee (id)
);



/*
DROP TABLE TxInput;
DROP TABLE TxBlock;
DROP TABLE TxBlockDoubleSpend;
DROP TABLE TxMempoolDoubleSpendAttempt;
DROP TABLE Block;
DROP TABLE Tx;
DROP TABLE Node;
DROP TABLE FeeAmount;
DROP TABLE Fee;
DROP TABLE FeeQuote;
*/