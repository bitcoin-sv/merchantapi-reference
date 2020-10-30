ALTER TABLE FeeAmount ADD COLUMN IF NOT EXISTS feeAmountType VARCHAR(50) NOT NULL DEFAULT '';
ALTER TABLE FeeAmount ALTER COLUMN feeAmountType DROP DEFAULT;

UPDATE FeeAmount
SET feeAmountType = 'MiningFee'
WHERE FeeAmount.feeAmountType='' AND FeeAmount.id in (SELECT min(id) FROM public.feeamount GROUP BY Fee);
 
UPDATE FeeAmount
SET feeAmountType = 'RelayFee'
WHERE FeeAmount.feeAmountType='' AND FeeAmount.id in (SELECT max(id) FROM public.feeamount GROUP BY Fee);

--ALTER TABLE FeeAmount DROP COLUMN feeAmountType;