#!/bin/bash

cd "$(dirname $0)"

VERSION="${1}"
STATS=""

if [ "${VERSION}" == "" ] ; then
  echo "Usage: ${0} <version>"
  exit 1
fi


function die() {
  echo $1

  echo "---"
  echo "${STATS}"
  echo "---"

  exit -1
}

if [ ! -e nuget.config ] ; then
  echo "NuGet config file nuget.config missing. Create one from template file template.nuget.config"
  exit -1
fi

echo "* Will publish NuGet packages"
ls -1 Build/MerchantAPI.Common.${VERSION}.nupkg

read -p "Continue if all is correct or terminate job and get latest files."


echo "* Publishing NuGet packages"
dotnet nuget push "Build/MerchantAPI.Common.${VERSION}.nupkg" \
  --source crea_nuget \
  --no-service-endpoint || STATS+="Error while publishing NuGet packages.\n"





