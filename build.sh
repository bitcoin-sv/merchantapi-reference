#!/bin/sh

PROG_NAME=$(awk -F'"' '/^const progname =/ {print $2}' main.go)
VER=$(git describe --tags)
GIT_COMMIT=$(git rev-parse HEAD)

rm -rf build
mkdir -p build
mkdir -p build/windows
mkdir -p build/linux
mkdir -p build/raspian

env GOOS=darwin GOARCH=amd64 go build -o build/darwin/${PROG_NAME}_${VER} -ldflags="-s -w -X main.commit=${GIT_COMMIT} -X github.com/bitcoin-sv/merchantapi-reference/handler.version=${VER}"
env GOOS=linux GOARCH=amd64 go build -o build/linux/${PROG_NAME}_${VER} -ldflags="-s -w -X main.commit=${GIT_COMMIT} -X github.com/bitcoin-sv/merchantapi-reference/handler.version=${VER}"
env GOOS=linux GOARCH=arm go build -o build/raspian/${PROG_NAME}_${VER} -ldflags="-s -w -X main.commit=${GIT_COMMIT} -X github.com/bitcoin-sv/merchantapi-reference/handler.version=${VER}"
env GOOS=windows GOARCH=386 go build -o build/windows/${PROG_NAME}_${VER} -ldflags="-s -w -X main.commit=${GIT_COMMIT} -X github.com/bitcoin-sv/merchantapi-reference/handler.version=${VER}"
