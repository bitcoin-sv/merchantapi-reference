# Stage 1 - the build process
FROM golang:alpine AS build-env
WORKDIR /app
COPY . .
RUN go build -o mapi

# Stage 2 - the production environment
FROM alpine
WORKDIR /app
COPY --from=build-env /app/mapi /app/
COPY --from=build-env /app/settings.conf /app/
COPY --from=build-env /app/fees*.json /app/
EXPOSE 9004

CMD ["./mapi"]
