#!/bin/bash

read -r VERSIONPREFIX<version_mapi.txt

dev="false"

while getopts 'd' flag
do
    case "${flag}" in
        d) dev="true"
    esac
done

if [ $dev != true ]
then

  git remote update
  git pull
  git status -uno

fi

COMMITID=$(git rev-parse --short HEAD)

APPVERSIONMAPI="$VERSIONPREFIX-$COMMITID"

echo "***************************"
echo "***************************"
echo "Building docker image for MerchantAPI version $APPVERSIONMAPI"

if [ $dev != true ]
then

  read -p "Continue if you have latest version (commit $COMMITID) or terminate job and get latest files."

fi

mkdir -p Build

sed s/{{VERSION}}/$VERSIONPREFIX/ < template-docker-compose.yml > Build/docker-compose.yml


docker build  --build-arg APPVERSION=$APPVERSIONMAPI -t bitcoinsv/mapi:$VERSIONPREFIX -f ../MerchantAPI/APIGateway/APIGateway.Rest/Dockerfile ..

if [ $dev != true ]
then
  cp template.env Build/.env  
  docker save bitcoinsv/mapi:$VERSIONPREFIX > Build/merchantapiapp.tar

else
  cp -i template.env Build/.env
  cp template-docker-compose-dev.yml Build/docker-compose-dev.yml
fi