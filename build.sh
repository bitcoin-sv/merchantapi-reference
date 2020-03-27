#!/bin/sh

cd $(dirname $BASH_SOURCE)

if [ -z "$(git status --porcelain)" ]; then
  # Working directory clean
  echo
else
  echo "Project must be clean before you can build"
  exit 1
fi

rm -rf build

PROG_NAME=$(awk -F'"' '/^const progname =/ {print $2}' main.go)
echo ${PROG_NAME}

OLD_VER=$(awk '/^const version =/ {print $0}' main.go)
NEW_VER=$(echo $OLD_VER | awk -F'[ .]' '/^const version =/ {print $1,$2,$3,$4"."$5"."$6+1"\""}')

sed -i "" "s/$OLD_VER/$NEW_VER/g" main.go

VER=$(awk -F'[ ."]' '/^const version =/ {print $5"."$6"."$7}' main.go)

git add main.go

git commit -m "New version - $VER"

GIT_COMMIT=$(git rev-parse HEAD)

mkdir -p build
mkdir -p build/windows
mkdir -p build/linux
mkdir -p build/raspian

go build -o build/darwin/${PROG_NAME}_${VER} -ldflags="-X main.commit=${GIT_COMMIT}"
env GOOS=linux GOARCH=amd64 go build -o build/linux/${PROG_NAME}_${VER} -ldflags="-s -w -X main.commit=${GIT_COMMIT}"
env GOOS=linux GOARCH=arm go build -o build/raspian/${PROG_NAME}_${VER} -ldflags="-s -w -X main.commit=${GIT_COMMIT}"
env GOOS=windows GOARCH=386 go build -o build/windows/${PROG_NAME}_${VER} -ldflags="-s -w -X main.commit=${GIT_COMMIT}"



