# Build context: repository root (needs data/parks.seed.json)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY data/ data/
COPY backend/Treningsparker.Api/Treningsparker.Api.csproj backend/Treningsparker.Api/
RUN dotnet restore backend/Treningsparker.Api/Treningsparker.Api.csproj
COPY backend/Treningsparker.Api/ backend/Treningsparker.Api/
RUN dotnet publish backend/Treningsparker.Api/Treningsparker.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Treningsparker.Api.dll"]
