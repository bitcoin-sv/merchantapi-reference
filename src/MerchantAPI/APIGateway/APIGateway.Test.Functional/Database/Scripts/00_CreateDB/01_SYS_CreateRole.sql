-- Copyright (c) 2020 Bitcoin Association.
-- Distributed under the Open BSV software license, see the accompanying file LICENSE

do $$
begin

  IF NOT EXISTS (
    SELECT FROM pg_catalog.pg_roles WHERE rolname = 'mapi_crud') THEN

    CREATE ROLE "mapi_crud" WITH
      NOLOGIN
      NOSUPERUSER
      INHERIT
      NOCREATEDB
      NOCREATEROLE
      NOREPLICATION;
   END IF;

  DROP ROLE IF EXISTS merchanttest;

  CREATE ROLE merchanttest LOGIN
	PASSWORD 'merchant'
	NOSUPERUSER INHERIT NOCREATEDB NOCREATEROLE NOREPLICATION;
  
  GRANT mapi_crud TO merchanttest;
 
  DROP ROLE IF EXISTS merchanttestddl;

  CREATE ROLE merchanttestddl LOGIN
	PASSWORD 'merchant'
	NOSUPERUSER INHERIT NOCREATEDB NOCREATEROLE NOREPLICATION;

end $$;
