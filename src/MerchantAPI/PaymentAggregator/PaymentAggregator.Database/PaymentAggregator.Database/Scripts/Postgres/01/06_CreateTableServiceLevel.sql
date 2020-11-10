CREATE TABLE IF NOT EXISTS ServiceLevel (
  serviceLevelId            SERIAL          NOT NULL,
  level                     INT             NOT NULL,
  description               VARCHAR(256)    NOT NULL,
  validTo                   TIMESTAMP,

  PRIMARY KEY (serviceLevelId)
);
