do $$
declare
  cnt integer;
begin
  SELECT count(*)INTO cnt FROM pg_roles WHERE rolname='merchant';
  if cnt = 0 then
	CREATE ROLE merchant LOGIN
	PASSWORD 'merchant'
	NOSUPERUSER INHERIT NOCREATEDB NOCREATEROLE NOREPLICATION;
  else
	ALTER TABLE IF EXISTS Node owner to merchant;
	ALTER TABLE IF EXISTS Tx owner to merchant;
	ALTER TABLE IF EXISTS Block owner to merchant;
	ALTER TABLE IF EXISTS TxMempoolDoubleSpendAttempt owner to merchant;
	ALTER TABLE IF EXISTS TxBlockDoubleSpend owner to merchant;
	ALTER TABLE IF EXISTS TxBlock owner to merchant;
	ALTER TABLE IF EXISTS TxInput owner to merchant;
	ALTER TABLE IF EXISTS FeeQuote owner to merchant;
	ALTER TABLE IF EXISTS Fee owner to merchant;
	ALTER TABLE IF EXISTS FeeAmount owner to merchant;
  end if;
    ALTER TABLE IF EXISTS Version owner to merchant;
end $$;