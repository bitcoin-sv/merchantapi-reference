#!/bin/bash

read -r VERSIONPREFIX<version_mapi-common.txt

git remote update
git pull
git status -uno

COMMITID=$(git rev-parse --short HEAD)

APPVERSIONMAPICOMMON="$VERSIONPREFIX-$COMMITID"

mkdir -p Build

echo "* Building NuGet MerchantAPI.Common package"

dotnet restore -p:Configuration=Release ..
VERSION=${VERSIONPREFIX} dotnet build -c Release -o Build ../MerchantAPI.Common.sln
VERSION=${VERSIONPREFIX} dotnet pack -c Release -o Build ../MerchantAPI.Common.sln

