# syntax=docker/dockerfile:1

# ---- Build -------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Restore first, using only project files, so the dependency layer is cached between builds.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ServicePortal.Domain/*.csproj src/ServicePortal.Domain/
COPY src/ServicePortal.Application/*.csproj src/ServicePortal.Application/
COPY src/ServicePortal.Infrastructure/*.csproj src/ServicePortal.Infrastructure/
COPY src/ServicePortal.Api/*.csproj src/ServicePortal.Api/
RUN dotnet restore src/ServicePortal.Api/ServicePortal.Api.csproj

COPY src/ src/
RUN dotnet publish src/ServicePortal.Api/ServicePortal.Api.csproj \
      --configuration Release --no-restore --output /app \
 && mkdir /data

# ---- Runtime -----------------------------------------------------------------------------------
# Chiseled image: no shell or package manager, runs as a non-root user by default.
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled AS runtime
WORKDIR /app
COPY --from=build /app ./
# The chiseled image has no shell, so the writable data directory is prepared in the build stage.
COPY --from=build --chown=1654:1654 /data /data
USER 1654

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    Storage__FilePath=/data/service-requests.json

# Persist data outside the container's writable layer.
VOLUME ["/data"]
EXPOSE 8080

ENTRYPOINT ["dotnet", "ServicePortal.Api.dll"]
