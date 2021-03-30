-- Copyright (c) 2020 Bitcoin Association

CREATE INDEX IF NOT EXISTS IFeeQuote_Id ON FeeQuote (id);
CREATE INDEX IF NOT EXISTS IFeeQuote_CreatedAt_ValidFrom ON FeeQuote (createdAt, validFrom);

CREATE INDEX IF NOT EXISTS ITx_TxInternalId ON Tx (txInternalId);
CREATE INDEX IF NOT EXISTS ITx_CallbackUrl ON Tx (callbackUrl);
CREATE INDEX IF NOT EXISTS ITx_DsCheck ON Tx (dsCheck);

CREATE INDEX IF NOT EXISTS IBlock_BlockInternalId ON Block (blockInternalId);
CREATE INDEX IF NOT EXISTS IBlock_BlockHeight ON Block (blockHeight);

CREATE INDEX IF NOT EXISTS ITxInput_TxInternalId ON TxInput (txInternalId);
CREATE INDEX IF NOT EXISTS ITxInput_PrevTxId_PrevN ON TxInput (prevTxId, prev_n);

CREATE INDEX IF NOT EXISTS ITxBlock_txInternalId ON TxBlock (txInternalId);

CREATE INDEX IF NOT EXISTS ITxMempoolDoubleSpendAttempt_sentDsNotificationAt ON TxMempoolDoubleSpendAttempt (sentDsNotificationAt);

CREATE INDEX IF NOT EXISTS ITxBlockDoubleSpend_txInternalId ON TxBlockDoubleSpend (txInternalId);
CREATE INDEX IF NOT EXISTS ITxBlockDoubleSpend_sentDsNotificationAt ON TxBlockDoubleSpend (sentDsNotificationAt);

CREATE INDEX IF NOT EXISTS ITxBlock_sentMerkleProofAt ON TxBlock (sentMerkleProofAt);