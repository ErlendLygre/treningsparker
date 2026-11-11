# Build context: repository root (needs data/parks.seed.json)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY data/ data/
COPY backend/Treningsparker.Api/Treningsparker.Api.csproj backend/Treningsparker.Api/
RUN dotnet restore backend/Treningsparker.Api/Treningsparker.Api.csproj -r linux-x64 -p:PublishReadyToRun=true
COPY backend/Treningsparker.Api/ backend/Treningsparker.Api/
# ReadyToRun precompiles the code, which roughly halves cold start time on low-CPU containers.
RUN dotnet publish backend/Treningsparker.Api/Treningsparker.Api.csproj -c Release -o /app --no-restore \
    -r linux-x64 --self-contained false -p:PublishReadyToRun=true

# Chiseled: minimal Ubuntu without shell or package manager; runs as a non-root user and listens on 8080.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Treningsparker.Api.dll"]
