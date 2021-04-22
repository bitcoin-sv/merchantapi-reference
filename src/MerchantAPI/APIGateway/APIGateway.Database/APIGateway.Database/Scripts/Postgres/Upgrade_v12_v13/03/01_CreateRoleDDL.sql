-- Copyright (c) 2020 Bitcoin Association.
-- Distributed under the Open BSV software license, see the accompanying file LICENSE

DO $$
BEGIN

  IF NOT EXISTS (
    SELECT FROM pg_catalog.pg_roles  
    WHERE rolname = 'merchantddl') THEN
      CREATE ROLE merchantddl LOGIN
      PASSWORD 'merchant'
        NOSUPERUSER INHERIT NOCREATEDB NOCREATEROLE NOREPLICATION;
  END IF;


END
$$;