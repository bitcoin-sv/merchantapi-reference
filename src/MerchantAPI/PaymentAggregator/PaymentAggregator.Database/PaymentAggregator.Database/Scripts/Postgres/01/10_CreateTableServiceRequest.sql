CREATE TABLE IF NOT EXISTS ServiceRequest(
		serviceRequestId        SERIAL          NOT NULL,
		subscriptionId          INT             NOT NULL,
		created                 TIMESTAMP       NOT NULL,
		responseCode            INT             NOT NULL,
		executionTimeMs         BIGINT          NOT NULL,
		
		PRIMARY KEY(serviceRequestId),
		FOREIGN KEY(subscriptionId) REFERENCES Subscription(subscriptionId)
);
