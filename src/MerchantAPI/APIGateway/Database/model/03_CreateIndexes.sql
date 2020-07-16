
CREATE INDEX IFeeQuote_Id ON FeeQuote (id);
CREATE INDEX IFeeQuote_CreatedAt_ValidFrom ON FeeQuote (createdAt, validFrom);

CREATE INDEX ITx_TxInternalId ON Tx (txInternalId);
CREATE INDEX ITx_CallbackUrl ON Tx (callbackUrl);
CREATE INDEX ITx_DsCheck ON Tx (dsCheck);

CREATE INDEX IBlock_BlockInternalId ON Block (blockInternalId);
CREATE INDEX IBlock_BlockHeight ON Block (blockHeight);

CREATE INDEX ITxInput_TxInternalId ON TxInput (txInternalId);
CREATE INDEX ITxInput_PrevTxId_PrevN ON TxInput (prevTxId, prev_n);

CREATE INDEX ITxBlock_txInternalId ON TxBlock (txInternalId);

CREATE INDEX ITxMempoolDoubleSpendAttempt_sentDsNotificationAt ON TxMempoolDoubleSpendAttempt (sentDsNotificationAt);

CREATE INDEX ITxBlockDoubleSpend_txInternalId ON TxBlockDoubleSpend (txInternalId);
CREATE INDEX ITxBlockDoubleSpend_sentDsNotificationAt ON TxBlockDoubleSpend (sentDsNotificationAt);

CREATE INDEX ITxBlock_sentMerkleProofAt ON TxBlock (sentMerkleProofAt);