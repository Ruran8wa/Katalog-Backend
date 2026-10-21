# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore first to leverage Docker layer cache
COPY ["Katalog-Backend/Katalog-Backend.csproj", "Katalog-Backend/"]
RUN dotnet restore "Katalog-Backend/Katalog-Backend.csproj"

# Copy source and publish in Release mode
COPY Katalog-Backend/ Katalog-Backend/
WORKDIR "/src/Katalog-Backend"
RUN dotnet publish "Katalog-Backend.csproj" -c Release -o /app/publish --no-restore

FROM build AS migrations-build
ARG TARGETARCH
RUN dotnet tool install --global dotnet-ef --version 10.*
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN RID=$([ "$TARGETARCH" = "arm64" ] && echo "linux-arm64" || echo "linux-x64") \
    && dotnet ef migrations bundle \
        --project Katalog-Backend.csproj \
        --self-contained -r "$RID" \
        --output /app/efbundle \
        --force

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrator
WORKDIR /app
COPY --from=migrations-build /app/efbundle .
USER $APP_UID
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ARG COMMIT_SHA=unknown
ENV APP_VERSION=$COMMIT_SHA
WORKDIR /app

EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "Katalog-Backend.dll"]