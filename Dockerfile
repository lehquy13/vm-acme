FROM mcr.microsoft.com/dotnet/sdk:10.0 AS source
WORKDIR /source
COPY Directory.Build.props ./
COPY src/ src/
COPY tools/ tools/
COPY migrations/ migrations/
RUN dotnet restore src/VmAcme.WebApi/VmAcme.WebApi.csproj \
    && dotnet restore tools/VmAcme.DbMigrator/VmAcme.DbMigrator.csproj

FROM source AS api-build
ARG RELEASE_VERSION=1.0.0
RUN dotnet publish src/VmAcme.WebApi/VmAcme.WebApi.csproj \
    --configuration Release \
    --no-restore \
    --output /app/api \
    -p:Version=${RELEASE_VERSION} \
    -p:InformationalVersion=${RELEASE_VERSION}

FROM source AS migrator-build
ARG RELEASE_VERSION=1.0.0
ARG MIGRATION_RELEASE=2
RUN dotnet publish tools/VmAcme.DbMigrator/VmAcme.DbMigrator.csproj \
    --configuration Release \
    --no-restore \
    --output /app/migrator \
    -p:MigrationRelease=${MIGRATION_RELEASE} \
    -p:Version=${RELEASE_VERSION} \
    -p:InformationalVersion=${RELEASE_VERSION}

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS migrator
WORKDIR /app
ARG APP_UID=1654
COPY --from=migrator-build /app/migrator .
USER $APP_UID
ENTRYPOINT ["dotnet", "VmAcme.DbMigrator.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ARG APP_UID=1654
COPY --from=api-build /app/api .
USER $APP_UID
ENTRYPOINT ["dotnet", "VmAcme.WebApi.dll"]
