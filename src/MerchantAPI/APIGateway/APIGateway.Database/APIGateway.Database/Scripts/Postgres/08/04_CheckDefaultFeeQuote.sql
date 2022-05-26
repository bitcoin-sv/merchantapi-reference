-- Copyright (c) 2022 Bitcoin Association.
-- Distributed under the Open BSV software license, see the accompanying file LICENSE

-- default FeeQuote should always exist, but we check just for any case
-- we reference the default FeeQuote on tx resilience upgrade in 05_SetDefaultFeeQuoteId

do $$
declare
  cnt integer;
  feeQuoteId integer;
  standardFeeId integer;
  dataFeeId integer;
begin
  SELECT count(*) INTO cnt FROM feequote WHERE identity IS NULL and identityprovider IS NULL;
  if cnt = 0 then
    INSERT INTO public.feequote(createdat, validfrom, identity, identityprovider, policies) 
    VALUES (now() at time zone 'utc', now() at time zone 'utc', null, null, null) 
    returning id INTO feeQuoteId;

    INSERT INTO public.Fee(feeQuote, feeType)
    VALUES(feeQuoteId, 'standard')
    returning id INTO standardFeeId;
	
	INSERT INTO public.FeeAmount (fee, satoshis, bytes, feeamounttype) 
	VALUES(standardFeeId, 100, 200, 'MiningFee');
	INSERT INTO public.FeeAmount (fee, satoshis, bytes, feeamounttype) 
	VALUES(standardFeeId, 100, 200, 'RelayFee');
	
	INSERT INTO public.Fee(feeQuote, feeType)
    VALUES(feeQuoteId, 'data')
    returning id INTO dataFeeId;
	
	INSERT INTO public.FeeAmount (fee, satoshis, bytes, feeamounttype) 
	VALUES(dataFeeId, 100, 200, 'MiningFee');
	INSERT INTO public.FeeAmount (fee, satoshis, bytes, feeamounttype) 
	VALUES(dataFeeId, 100, 200, 'RelayFee');
  end if;
end $$;
