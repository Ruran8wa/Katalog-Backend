FROM golang:1.25-alpine AS healthcheck-build
WORKDIR /src
COPY healthcheck.go .
RUN CGO_ENABLED=0 GOOS=linux GOARCH=amd64 go build -trimpath -ldflags="-s -w" -o /healthcheck healthcheck.go

FROM mcr.microsoft.com/azure-messaging/servicebus-emulator:latest
COPY --from=healthcheck-build /healthcheck /ServiceBus_Emulator/healthcheck
HEALTHCHECK --interval=10s --timeout=5s --retries=12 --start-period=30s CMD ["/ServiceBus_Emulator/healthcheck"]
