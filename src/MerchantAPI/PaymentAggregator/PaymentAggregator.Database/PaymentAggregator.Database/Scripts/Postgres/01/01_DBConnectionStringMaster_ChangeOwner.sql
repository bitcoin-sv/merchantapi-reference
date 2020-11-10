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
    ALTER TABLE IF EXISTS Version owner to merchant;
end $$;