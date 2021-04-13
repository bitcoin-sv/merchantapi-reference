-- Copyright (c) 2020 Bitcoin Association.
-- Distributed under the Open BSV software license, see the accompanying file LICENSE

create or replace function create_pg_constraint_if_not_exists (
    c_name text, constraint_sql text
) 
returns void AS
$$
begin
    -- Look for our constraint
    if not exists (select conname 
                   from pg_constraint 
                   where conname = LOWER(c_name)) then
        execute constraint_sql;
    end if;
end;
$$ language 'plpgsql'