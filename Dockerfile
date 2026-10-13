# syntax=docker/dockerfile:1

# ── Stage 1: restore, build and publish with the .NET SDK ──────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore first to leverage Docker layer cache
COPY ["Katalog-Backend/Katalog-Backend.csproj", "Katalog-Backend/"]
RUN dotnet restore "Katalog-Backend/Katalog-Backend.csproj"

# Copy source and publish in Release mode
COPY Katalog-Backend/ Katalog-Backend/
WORKDIR "/src/Katalog-Backend"
RUN dotnet publish "Katalog-Backend.csproj" -c Release -o /app/publish --no-restore

# ── Stage 2: build the EF Core migration bundle (SDK needed, never shipped) ────
FROM build AS migrations-build
ARG TARGETARCH
# Pin dotnet-ef to the major version of your EF Core packages
RUN dotnet tool install --global dotnet-ef --version 10.*
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN RID=$([ "$TARGETARCH" = "arm64" ] && echo "linux-arm64" || echo "linux-x64") \
    && dotnet ef migrations bundle \
        --project Katalog-Backend.csproj \
        --self-contained -r "$RID" \
        --output /app/efbundle \
        --force

# ── Stage 3: one-off migrator image (no SDK, no source) ────────────────────────
# Build with: docker build --target migrator .
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrator
WORKDIR /app
COPY --from=migrations-build /app/efbundle .
USER $APP_UID
ENTRYPOINT ["./efbundle"]

# ── Stage 4: minimal runtime image for the API and consumer (default target) ───
# Keep this stage LAST so a plain `docker build .` produces the app image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Unprivileged port (ASP.NET Core 8+ default)
EXPOSE 8080

# Copy only the published output: no SDK, source code or test projects
COPY --from=build /app/publish .

# Built-in non-root user in official .NET 8+ images (UID 1654)
USER $APP_UID

ENTRYPOINT ["dotnet", "Katalog-Backend.dll"]