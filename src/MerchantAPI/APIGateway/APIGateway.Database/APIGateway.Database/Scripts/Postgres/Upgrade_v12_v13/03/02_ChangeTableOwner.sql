-- Copyright (c) 2020 Bitcoin Association

ALTER TABLE IF EXISTS Node owner to merchantddl;
ALTER TABLE IF EXISTS Tx owner to merchantddl;
ALTER TABLE IF EXISTS Block owner to merchantddl;
ALTER TABLE IF EXISTS TxMempoolDoubleSpendAttempt owner to merchantddl;
ALTER TABLE IF EXISTS TxBlockDoubleSpend owner to merchantddl;
ALTER TABLE IF EXISTS TxBlock owner to merchantddl;
ALTER TABLE IF EXISTS TxInput owner to merchantddl;
ALTER TABLE IF EXISTS FeeQuote owner to merchantddl;
ALTER TABLE IF EXISTS Fee owner to merchantddl;
ALTER TABLE IF EXISTS FeeAmount owner to merchantddl;
ALTER TABLE IF EXISTS Version owner to merchantddl;
ALTER SEQUENCE IF EXISTS node_nodeid_seq OWNER TO merchantddl; 
ALTER SEQUENCE IF EXISTS tx_txinternalid_seq OWNER TO merchantddl; 
ALTER SEQUENCE IF EXISTS block_blockinternalid_seq OWNER TO merchantddl; 
ALTER SEQUENCE IF EXISTS feequote_id_seq OWNER TO merchantddl; 
ALTER SEQUENCE IF EXISTS fee_id_seq OWNER TO merchantddl;
ALTER SEQUENCE IF EXISTS feeamount_id_seq OWNER TO merchantddl;




