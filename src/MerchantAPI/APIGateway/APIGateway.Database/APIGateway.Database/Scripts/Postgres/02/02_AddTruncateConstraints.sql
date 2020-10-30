SELECT create_pg_constraint_if_not_exists(
        'txMempoolDoubleSpendAttempt_txInternalId_fk',
        'ALTER TABLE TxMempoolDoubleSpendAttempt ADD CONSTRAINT txMempoolDoubleSpendAttempt_txInternalId_fk FOREIGN KEY (txInternalId) REFERENCES Tx(txInternalId) ON DELETE CASCADE;');
ALTER TABLE TxMempoolDoubleSpendAttempt DROP CONSTRAINT IF EXISTS txMempoolDoubleSpendAttempt_txInternalId_fkey;

SELECT create_pg_constraint_if_not_exists(
        'txBlockDoubleSpend_txInternalId_fk',
        'ALTER TABLE TxBlockDoubleSpend ADD CONSTRAINT txBlockDoubleSpend_txInternalId_fk FOREIGN KEY (txInternalId) REFERENCES Tx(txInternalId) ON DELETE CASCADE;');
ALTER TABLE TxBlockDoubleSpend DROP CONSTRAINT IF EXISTS txBlockDoubleSpend_txInternalId_fkey;

SELECT create_pg_constraint_if_not_exists(
        'txBlockDoubleSpend_blockInternalId_fk',
        'ALTER TABLE TxBlockDoubleSpend ADD CONSTRAINT txBlockDoubleSpend_blockInternalId_fk FOREIGN KEY (blockInternalId) REFERENCES Block(blockInternalId) ON DELETE CASCADE;');
ALTER TABLE TxBlockDoubleSpend DROP CONSTRAINT IF EXISTS txBlockDoubleSpend_blockInternalId_fkey;

SELECT create_pg_constraint_if_not_exists(
        'txBlock_txInternalId_fk',
        'ALTER TABLE TxBlock ADD CONSTRAINT txBlock_txInternalId_fk FOREIGN KEY (txInternalId) REFERENCES Tx(txInternalId) ON DELETE CASCADE;');
ALTER TABLE TxBlock DROP CONSTRAINT IF EXISTS txBlock_txInternalId_fkey;

SELECT create_pg_constraint_if_not_exists(
        'txBlock_blockInternalId_fk',
        'ALTER TABLE TxBlock ADD CONSTRAINT txBlock_blockInternalId_fk FOREIGN KEY (blockInternalId) REFERENCES Block(blockInternalId) ON DELETE CASCADE;');
ALTER TABLE TxBlock DROP CONSTRAINT IF EXISTS txBlock_blockInternalId_fkey;

SELECT create_pg_constraint_if_not_exists(
        'txInput_txInternalId_fk',
        'ALTER TABLE TxInput ADD CONSTRAINT txInput_txInternalId_fk FOREIGN KEY (txInternalId) REFERENCES Tx(txInternalId) ON DELETE CASCADE;');
ALTER TABLE TxInput DROP CONSTRAINT IF EXISTS txInput_txInternalId_fkey;
