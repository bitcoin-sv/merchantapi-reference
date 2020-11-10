-- insert 5 service levels
do $$
declare
  cnt integer;
  serviceLevelId integer;
  standardFeeId integer;
  dataFeeId integer;
begin
  SELECT count(*) INTO cnt FROM public.ServiceLevel;
  if cnt = 0 then
    -- begin sla0
    INSERT INTO public.ServiceLevel(level, description, validTo) 
    VALUES (0, 'No miner will mine, no double spend protection', null) 
    returning ServiceLevel.serviceLevelId INTO serviceLevelId;

    INSERT INTO public.ServiceLevelFee(serviceLevelId, feeType)
    VALUES(serviceLevelId, 'standard')
    returning id INTO standardFeeId;
	
    INSERT INTO public.ServiceLevelFeeAmount (serviceLevelFeeId, feeAmountType, satoshis, bytes) 
    VALUES(standardFeeId, 'MiningFee', 300, 1000);
    INSERT INTO public.ServiceLevelFeeAmount (serviceLevelFeeId, feeAmountType, satoshis, bytes) 
    VALUES(standardFeeId, 'RelayFee', 250, 1000);
	
    INSERT INTO public.ServiceLevelFee(serviceLevelId, feeType)
    VALUES(serviceLevelId, 'data')
    returning id INTO dataFeeId;

    INSERT INTO public.ServiceLevelFeeAmount (serviceLevelFeeId, feeAmountType, satoshis, bytes) 
    VALUES(dataFeeId, 'MiningFee', 150, 1000);
    INSERT INTO public.ServiceLevelFeeAmount (serviceLevelFeeId, feeAmountType, satoshis, bytes) 
    VALUES(dataFeeId, 'RelayFee', 100, 1000);
    -- end sla0
	
    -- begin sla1
    INSERT INTO public.ServiceLevel(level, description, validTo) 
    VALUES (1, 'Slow to mine, no double spend protection', null) 
    returning ServiceLevel.serviceLevelId INTO serviceLevelId;

    INSERT INTO public.ServiceLevelFee(serviceLevelId, feeType)
    VALUES(serviceLevelId, 'standard')
    returning id INTO standardFeeId;
	
    INSERT INTO public.ServiceLevelFeeAmount (serviceLevelFeeId, feeAmountType, satoshis, bytes) 
    VALUES(standardFeeId, 'MiningFee', 350, 1000);
    INSERT INTO public.ServiceLevelFeeAmount (serviceLevelFeeId, feeAmountType, satoshis, bytes) 
    VALUES(standardFeeId, 'RelayFee', 300, 1000);
	
    INSERT INTO public.ServiceLevelFee(serviceLevelId, feeType)
    VALUES(serviceLevelId, 'data')
    returning id INTO dataFeeId;
	
    INSERT INTO public.ServiceLevelFeeAmount (serviceLevelFeeId, feeAmountType, satoshis, bytes) 
    VALUES(dataFeeId, 'MiningFee', 200, 1000);
    INSERT INTO public.ServiceLevelFeeAmount (serviceLevelFeeId, feeAmountType, satoshis, bytes) 
    VALUES(dataFeeId, 'RelayFee', 150, 1000);
    -- end sla1
	
    -- begin sla2
    INSERT INTO public.ServiceLevel(level, description, validTo) 
    VALUES (2, 'Slow to mine, double spend protection', null) 
    returning ServiceLevel.serviceLevelId INTO serviceLevelId;

    INSERT INTO public.ServiceLevelFee(serviceLevelId, feeType)
    VALUES(serviceLevelId, 'standard')
    returning id INTO standardFeeId;
	
    INSERT INTO public.ServiceLevelFeeAmount (serviceLevelFeeId, feeAmountType, satoshis, bytes) 
    VALUES(standardFeeId, 'MiningFee', 400, 1000);
    INSERT INTO public.ServiceLevelFeeAmount (serviceLevelFeeId, feeAmountType, satoshis, bytes) 
    VALUES(standardFeeId, 'RelayFee', 350, 1000);
	
    INSERT INTO public.ServiceLevelFee(serviceLevelId, feeType)
    VALUES(serviceLevelId, 'data')
    returning id INTO dataFeeId;
	
    INSERT INTO public.ServiceLevelFeeAmount (serviceLevelFeeId, feeAmountType, satoshis, bytes) 
    VALUES(dataFeeId, 'MiningFee', 250, 1000);
    INSERT INTO public.ServiceLevelFeeAmount (serviceLevelFeeId, feeAmountType, satoshis, bytes) 
    VALUES(dataFeeId, 'RelayFee', 200, 1000);
    -- end sla2
	
    -- begin sla3
    INSERT INTO public.ServiceLevel(level, description, validTo) 
    VALUES (3, 'Fast to mine, double spend protection', null) 
    returning ServiceLevel.serviceLevelId INTO serviceLevelId;

    INSERT INTO public.ServiceLevelFee(serviceLevelId, feeType)
    VALUES(serviceLevelId, 'standard')
    returning id INTO standardFeeId;
	
    INSERT INTO public.ServiceLevelFeeAmount (serviceLevelFeeId, feeAmountType, satoshis, bytes) 
    VALUES(standardFeeId, 'MiningFee', 500, 1000);
    INSERT INTO public.ServiceLevelFeeAmount (serviceLevelFeeId, feeAmountType, satoshis, bytes) 
    VALUES(standardFeeId, 'RelayFee', 400, 1000);
	
    INSERT INTO public.ServiceLevelFee(serviceLevelId, feeType)
    VALUES(serviceLevelId, 'data')
    returning id INTO dataFeeId;
	
    INSERT INTO public.ServiceLevelFeeAmount (serviceLevelFeeId, feeAmountType, satoshis, bytes) 
    VALUES(dataFeeId, 'MiningFee', 300, 1000);
    INSERT INTO public.ServiceLevelFeeAmount (serviceLevelFeeId, feeAmountType, satoshis, bytes) 
    VALUES(dataFeeId, 'RelayFee', 250, 1000);
    -- end sla3
	
    -- begin sla4
    INSERT INTO public.ServiceLevel(level, description, validTo) 
    VALUES (4, 'All miners will mine, full double spend protection', null) 
    returning ServiceLevel.serviceLevelId INTO serviceLevelId;
    -- end sla4
	
  end if;
end $$;
