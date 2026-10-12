# Stage 1: Build & Publish using the .NET SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies first to leverage Docker layer cache
COPY ["Katalog-Backend/Katalog-Backend.csproj", "Katalog-Backend/"]
RUN dotnet restore "Katalog-Backend/Katalog-Backend.csproj"

# Copy source files and compile/publish in Release mode
COPY Katalog-Backend/ Katalog-Backend/
WORKDIR "/src/Katalog-Backend"
RUN dotnet publish "Katalog-Backend.csproj" -c Release -o /app/publish --no-restore

# Stage 2: Minimal runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Expose HTTP port 8080 (default unprivileged port for ASP.NET Core 8+)
EXPOSE 8080

# Copy only the compiled binaries from the build stage
COPY --from=build /app/publish .

# Run as non-root user (built into official .NET 8+ container images, UID 1654)
USER $APP_UID

ENTRYPOINT ["dotnet", "Katalog-Backend.dll"]
