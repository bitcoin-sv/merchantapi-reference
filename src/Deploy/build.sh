#!/bin/bash

cd "$(dirname $0)"

VERSION="${1}"

if [ "${VERSION}" == "" ] ; then
  echo "Usage: ${0} <version>"
  exit 1
fi

TAG="v${VERSION}"


echo "* Checking if working directory is clean"
if [ ! -z "$(git status --untracked-files=no --porcelain)" ] ; then
  # Uncommitted changes in tracked files
  echo "Uncommited changes present. Exiting."
  exit -1
fi
# Working directory clean excluding untracked files

echo "* Fetching tags"
git remote update
git pull
git fetch --tags

# CURRENT_TAGS=$(git tag -l --contains HEAD)

echo "* Trying to checkout tag"

if git checkout ${TAG} ; then
  echo "* Checked out branch with ${TAG}."
else
  echo "* Checking out branch ${TAG} failed."
  echo "* Tagging current branch with tag: ${TAG}."
  git tag -a ${TAG} -m "Version ${VERSION}."
fi

VERSIONPREFIX="${VERSION}"

COMMITID=$(git rev-parse --short HEAD)
echo "* Using commit with id: $COMMITID"
echo "* Last commit:"

git log -n 1

echo "***************************"
echo "***************************"
echo "Building MerchantAPI.Common version $VERSIONPREFIX"
read -p "Continue if you have latest version (commit $COMMITID) or terminate job and get latest files."

git push origin ${TAG}

echo "* Building NuGet MerchantAPI.Common package"

dotnet restore -p:Configuration=Release ..
VERSION=${VERSIONPREFIX} dotnet build -c Release -o Build ../MerchantAPI.Common.sln
VERSION=${VERSIONPREFIX} dotnet pack -c Release -o Build ../MerchantAPI.Common.sln

echo "* Checking out master branch"
git checkout master