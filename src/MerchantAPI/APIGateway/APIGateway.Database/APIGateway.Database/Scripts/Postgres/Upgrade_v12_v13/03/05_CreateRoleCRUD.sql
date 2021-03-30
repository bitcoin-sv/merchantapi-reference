-- Copyright (c) 2020 Bitcoin Association

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT FROM pg_catalog.pg_roles  
    WHERE rolname = 'mapi_crud') THEN 
      CREATE ROLE "mapi_crud" WITH
	NOLOGIN	NOSUPERUSER INHERIT NOCREATEDB NOCREATEROLE NOREPLICATION;
  END IF;

  GRANT mapi_crud TO merchant;

END
$$;