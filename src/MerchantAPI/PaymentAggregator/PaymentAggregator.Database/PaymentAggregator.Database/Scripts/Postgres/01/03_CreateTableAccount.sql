CREATE TABLE IF NOT EXISTS Account(
		accountID			SERIAL			NOT NULL,
		organisationName	VARCHAR(256)	NOT NULL,
		contactFirstName	VARCHAR(128),
		contactLastName		VARCHAR(128),
		email				VARCHAR(128) 	NOT NULL,
		identity			VARCHAR(256)	NOT NULL,
		identityProvider	VARCHAR(256)	NOT NULL,
		createdAt			TIMESTAMP,
		
		PRIMARY KEY(accountID)
);

DROP INDEX IF EXISTS IAccount_Identity;
CREATE UNIQUE INDEX IF NOT EXISTS IAccount_Identity ON Account (identity, identityProvider);
