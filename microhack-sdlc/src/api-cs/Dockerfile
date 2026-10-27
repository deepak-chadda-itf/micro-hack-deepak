# Build context is the `src/` folder so the SQL migrations/seed files can be
# copied next to the app (the API resolves them at ../database from its cwd).
#   docker build -f api-cs/Dockerfile -t octocat-api .   (run from src/)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY api-cs/OctoSupply.Api.csproj api-cs/
RUN dotnet restore api-cs/OctoSupply.Api.csproj
COPY api-cs/ api-cs/
RUN dotnet publish api-cs/OctoSupply.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app/api
COPY --from=build /out .
COPY database/ /app/database/

# SQLite lives on the container filesystem (ephemeral by design, see infra/README.md).
ENV PORT=3000 \
    DB_FILE=/app/data/app.db
RUN mkdir -p /app/data && chown -R app:app /app/data
USER app

EXPOSE 3000
ENTRYPOINT ["dotnet", "OctoSupply.Api.dll"]
