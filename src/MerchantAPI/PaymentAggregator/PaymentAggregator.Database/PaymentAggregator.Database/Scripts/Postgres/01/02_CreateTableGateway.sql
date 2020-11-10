CREATE TABLE IF NOT EXISTS Gateway (
        gatewayId           SERIAL         NOT NULL,
        url                 VARCHAR(128)    NOT NULL,
        minerRef            VARCHAR(128)    NOT NULL,
        email               VARCHAR(128)    NOT NULL,
        organisationName    VARCHAR(128)    NOT NULL,
        contactFirstName    VARCHAR(128)    NOT NULL,
        contactLastName     VARCHAR(128)    NOT NULL,
        createdAt           TIMESTAMP      NOT NULL,
        remarks             VARCHAR(1024),
        lastError           VARCHAR(256),
        lastErrorAt         TIMESTAMP,
        disabledAt          TIMESTAMP,
        deletedAt           TIMESTAMP,

        PRIMARY KEY (gatewayId)    
);
CREATE UNIQUE INDEX IF NOT EXISTS IGateway_urlAndNullDeletedAt ON Gateway (url, (deletedAt IS NULL)) WHERE deletedAt IS NULL;

