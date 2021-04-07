-- Remove not null from payload
ALTER TABLE Tx ALTER COLUMN txPayload DROP NOT NULL;

-- Add unconfirmed ancestor flag
ALTER TABLE Tx ADD COLUMN IF NOT EXISTS unconfirmedAncestor BOOLEAN DEFAULT false;

-- Update existing records
UPDATE Tx SET unconfirmedAncestor = false WHERE unconfirmedAncestor IS NULL;

-- Add not null constraint
ALTER TABLE Tx ALTER COLUMN unconfirmedAncestor SET NOT NULL;