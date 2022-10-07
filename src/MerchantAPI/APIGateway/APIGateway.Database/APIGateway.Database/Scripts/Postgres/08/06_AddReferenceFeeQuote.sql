-- Copyright (c) 2022 Bitcoin Association.
-- Distributed under the Open BSV software license, see the accompanying file LICENSE

ALTER TABLE Tx ALTER COLUMN policyQuoteId DROP DEFAULT;

SELECT create_pg_constraint_if_not_exists(
        'tx_policyquoteid_fk',
        'ALTER TABLE Tx ADD CONSTRAINT tx_policyquoteid_fk FOREIGN KEY (policyQuoteId) REFERENCES feeQuote (id);');