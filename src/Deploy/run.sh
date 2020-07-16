#!/bin/bash

cp ./config/*.crt /usr/local/share/ca-certificates
update-ca-certificates
cp ./config/*.json /app
cd app
dotnet MerchantAPI.APIGateway.Rest.dll