-- Copyright (c) 2022 Bitcoin Association.
-- Distributed under the Open BSV software license, see the accompanying file LICENSE

-- update FeeQuoteId to default if value is NULL

do $$
declare
  cnt integer;
  feeQuoteId integer;
begin
  SELECT count(*) INTO cnt FROM public.tx WHERE policyQuoteId IS NULL;
  if cnt > 0 then
    SELECT MIN(id) INTO feeQuoteId FROM feequote;

    UPDATE tx
    SET policyQuoteId = feeQuoteId
    WHERE policyQuoteId IS NULL;
  end if;
end $$;
