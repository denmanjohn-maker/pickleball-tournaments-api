FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/PickleballTournaments.Api/PickleballTournaments.Api.csproj \
    -c Release -o /app/publish --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:$PORT
ENTRYPOINT ["dotnet", "PickleballTournaments.Api.dll"]
