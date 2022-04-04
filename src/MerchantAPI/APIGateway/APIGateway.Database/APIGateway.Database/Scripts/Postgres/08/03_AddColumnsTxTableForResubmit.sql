-- Copyright (c) 2022 Bitcoin Association.
-- Distributed under the Open BSV software license, see the accompanying file LICENSE

-- Add nullable PolicyQuoteId column
ALTER TABLE Tx ADD COLUMN IF NOT EXISTS policyQuoteId BIGINT;

ALTER TABLE Tx ADD COLUMN IF NOT EXISTS okToMine BOOLEAN NOT NULL DEFAULT false;

ALTER TABLE Tx ALTER COLUMN okToMine DROP DEFAULT;

ALTER TABLE Tx ADD COLUMN IF NOT EXISTS setPolicyQuote BOOLEAN NOT NULL DEFAULT false;

ALTER TABLE Tx ALTER COLUMN setPolicyQuote DROP DEFAULT;