-- Copyright (c) 2022 Bitcoin Association.
-- Distributed under the Open BSV software license, see the accompanying file LICENSE

-- Add submittedAt column
ALTER TABLE Tx ADD COLUMN IF NOT EXISTS submittedAt TIMESTAMP;

UPDATE Tx SET submittedAt = receivedAt WHERE submittedAt IS NULL;

ALTER TABLE Tx ALTER COLUMN submittedAt SET NOT NULL;
