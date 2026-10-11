ARG SDK_BASE=mcr.microsoft.com/dotnet/sdk:10.0
ARG API_BASE=mcr.microsoft.com/dotnet/aspnet:10.0
ARG MIGRATOR_BASE=mcr.microsoft.com/dotnet/aspnet:10.0
ARG BUNDLE_BASE=mcr.microsoft.com/dotnet/runtime-deps:10.0
FROM ${SDK_BASE} AS source
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

FROM ${MIGRATOR_BASE} AS migrator
WORKDIR /app
ARG APP_UID=1654
COPY --from=migrator-build /app/migrator .
USER $APP_UID
ENTRYPOINT ["dotnet", "VmAcme.DbMigrator.dll"]

# Company migration artifacts: one self-contained bundle per database provider.
FROM source AS bundle-build
ARG BUNDLE_RUNTIME=linux-x64
RUN dotnet tool install dotnet-ef --version 10.0.12 --tool-path /ef-tools
RUN dotnet restore migrations/VmAcme.Migrations.PostgreSql --runtime ${BUNDLE_RUNTIME} \
    && Secrets__Mode=Environment ConnectionStrings__DefaultConnection='Host=localhost;Database=bundle' \
    /ef-tools/dotnet-ef migrations bundle --project migrations/VmAcme.Migrations.PostgreSql \
    --startup-project migrations/VmAcme.Migrations.PostgreSql --self-contained --target-runtime ${BUNDLE_RUNTIME} \
    --configuration Release --output /app/efbundle-postgresql
RUN dotnet restore migrations/VmAcme.Migrations.SqlServer --runtime ${BUNDLE_RUNTIME} \
    && Secrets__Mode=Environment ConnectionStrings__DefaultConnection='Server=localhost;Database=bundle;Integrated Security=True' \
    /ef-tools/dotnet-ef migrations bundle --project migrations/VmAcme.Migrations.SqlServer \
    --startup-project migrations/VmAcme.Migrations.SqlServer --self-contained --target-runtime ${BUNDLE_RUNTIME} \
    --configuration Release --output /app/efbundle-sqlserver

FROM ${BUNDLE_BASE} AS bundle-postgresql
WORKDIR /app
COPY --from=bundle-build /app/efbundle-postgresql /app/efbundle
USER 1654
ENTRYPOINT ["/app/efbundle"]

FROM ${BUNDLE_BASE} AS bundle-sqlserver
WORKDIR /app
COPY --from=bundle-build /app/efbundle-sqlserver /app/efbundle
USER 1654
ENTRYPOINT ["/app/efbundle"]

FROM ${API_BASE} AS api
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ARG APP_UID=1654
COPY --from=api-build /app/api .
USER $APP_UID
ENTRYPOINT ["dotnet", "VmAcme.WebApi.dll"]
