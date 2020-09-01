# Stage 1 - the build process
FROM golang:alpine AS build-env
RUN apk update && apk add git
WORKDIR /app
COPY . .
RUN VER=$(git describe --tags) && \
  GIT_COMMIT=$(git rev-parse HEAD) && \
  echo $VER && \
  go build -o mapi -ldflags="-s -w -X main.commit=${GIT_COMMIT} -X github.com/bitcoin-sv/merchantapi-reference/handler.version=${VER}"

# Stage 2 - the production environment
FROM alpine
WORKDIR /app
COPY --from=build-env /app/mapi /app/
COPY --from=build-env /app/settings.conf /app/
COPY --from=build-env /app/fees*.json /app/
EXPOSE 9004

CMD ["./mapi"]
