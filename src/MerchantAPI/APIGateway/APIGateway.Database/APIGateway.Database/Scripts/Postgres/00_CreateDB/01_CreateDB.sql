do $$
declare
  cnt integer;
begin
  SELECT count(*)INTO cnt FROM pg_roles WHERE rolname='merchant';
  if cnt = 0 then
	CREATE ROLE merchant LOGIN
	PASSWORD 'merchant'
	NOSUPERUSER INHERIT NOCREATEDB NOCREATEROLE NOREPLICATION;
  end if;
end $$;

CREATE DATABASE merchant_gateway
  WITH OWNER = merchant
  ENCODING = 'UTF8'
  TABLESPACE = pg_default
  CONNECTION LIMIT = -1;
