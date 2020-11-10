CREATE TABLE IF NOT EXISTS ServiceLevelFee (
  id                        SERIAL          NOT NULL,
  serviceLevelId            BIGINT          NOT NULL,
  feeType                   VARCHAR(256)    NOT NULL,

  PRIMARY KEY (id),
  FOREIGN KEY (serviceLevelId) REFERENCES ServiceLevel (serviceLevelId)
);

