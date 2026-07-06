# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first with just the project file, so this layer caches across source-only changes.
COPY global.json ./
COPY src/PickleballTournaments.Api/PickleballTournaments.Api.csproj src/PickleballTournaments.Api/
RUN dotnet restore src/PickleballTournaments.Api/PickleballTournaments.Api.csproj

COPY src/PickleballTournaments.Api/ src/PickleballTournaments.Api/
RUN dotnet publish src/PickleballTournaments.Api/PickleballTournaments.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PickleballTournaments.Api.dll"]
