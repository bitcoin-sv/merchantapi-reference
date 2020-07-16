FROM postgres

COPY ["src/MerchantAPI/APIGateway/Database/model/02_CreateTables.sql", "docker-entrypoint-initdb.d/"]
COPY ["src/MerchantAPI/APIGateway/Database/model/03_CreateIndexes.sql", "docker-entrypoint-initdb.d/"]
COPY ["src/MerchantAPI/APIGateway/Database/model/04_CreateUser.sql", "docker-entrypoint-initdb.d/"]
COPY ["src/MerchantAPI/APIGateway/Database/model/05_InitialData.sql", "docker-entrypoint-initdb.d/"]