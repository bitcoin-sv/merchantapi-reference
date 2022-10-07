-- Copyright (c) 2022 Bitcoin Association.
-- Distributed under the Open BSV software license, see the accompanying file LICENSE

-- Add txStatus column
ALTER TABLE Tx ADD COLUMN IF NOT EXISTS txStatus SMALLINT NOT NULL DEFAULT 0;

ALTER TABLE Tx ALTER COLUMN txStatus DROP DEFAULT;
