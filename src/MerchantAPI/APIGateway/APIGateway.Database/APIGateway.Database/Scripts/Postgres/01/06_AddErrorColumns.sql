-- Copyright (c) 2020 Bitcoin Association.
-- Distributed under the Open BSV software license, see the accompanying file LICENSE

ALTER TABLE TxBlock
ADD COLUMN IF NOT EXISTS lastErrorDescription VARCHAR(256),
ADD COLUMN IF NOT EXISTS LastErrorAt TIMESTAMP,
ADD COLUMN IF NOT EXISTS ErrorCount INT;

ALTER TABLE TxBlockDoublespend 
ADD COLUMN IF NOT EXISTS lastErrorDescription VARCHAR(256),
ADD COLUMN IF NOT EXISTS LastErrorAt TIMESTAMP,
ADD COLUMN IF NOT EXISTS ErrorCount INT;

ALTER TABLE TxMempoolDoublespendAttempt 
ADD COLUMN IF NOT EXISTS lastErrorDescription VARCHAR(256),
ADD COLUMN IF NOT EXISTS LastErrorAt TIMESTAMP,
ADD COLUMN IF NOT EXISTS ErrorCount INT;
