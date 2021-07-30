-- Add new nullable column
ALTER TABLE FeeQuote ADD COLUMN IF NOT EXISTS policies TEXT;
