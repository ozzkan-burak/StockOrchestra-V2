# ==============================================================================
# StockOrchestra - Analytical Store Dockerfile
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build

WORKDIR /src
COPY services/analytical-store/AnalyticalStore.csproj ./services/analytical-store/
COPY shared/src/Shared/Shared.csproj ./shared/src/Shared/
RUN dotnet restore services/analytical-store/AnalyticalStore.csproj

COPY services/analytical-store/ ./services/analytical-store/
COPY shared/src/Shared/ ./shared/src/Shared/

WORKDIR /src/services/analytical-store
RUN dotnet publish AnalyticalStore.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
RUN addgroup -g 1000 appgroup && adduser -u 1000 -G appgroup -s /bin/sh -D appuser

WORKDIR /app
COPY --from=build /app/publish ./
RUN chown -R appuser:appgroup /app
USER appuser

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -q -O /dev/null http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "AnalyticalStore.dll"]