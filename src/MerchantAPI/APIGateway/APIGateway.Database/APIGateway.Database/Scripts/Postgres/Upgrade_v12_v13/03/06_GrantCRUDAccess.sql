-- Copyright (c) 2020 Bitcoin Association

GRANT SELECT, INSERT, UPDATE, DELETE
ON TABLE Node, Tx, Block, TxMempoolDoubleSpendAttempt, TxBlockDoubleSpend, TxBlock, TxInput, FeeQuote, Fee, FeeAmount
TO GROUP mapi_crud;

GRANT USAGE, SELECT 
ON SEQUENCE node_nodeid_seq, block_blockinternalid_seq, feequote_id_seq, fee_id_seq, feeamount_id_seq
TO GROUP mapi_crud;

GRANT USAGE, UPDATE, SELECT 
ON SEQUENCE tx_txinternalid_seq
TO GROUP mapi_crud;