CREATE TABLE IF NOT EXISTS ServiceLevelFeeAmount (
  id                        SERIAL          NOT NULL,
  serviceLevelFeeId         BIGINT          NOT NULL,
  feeAmountType             VARCHAR(50)     NOT NULL,
  satoshis                  INT             NOT NULL,
  bytes                     INT             NOT NULL,

  PRIMARY KEY (id),
  FOREIGN KEY (serviceLevelFeeId) REFERENCES ServiceLevelFee (id)
);
