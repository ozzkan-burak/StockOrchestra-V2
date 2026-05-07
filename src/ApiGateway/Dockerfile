# ==============================================================================
# StockOrchestra - API Gateway Dockerfile (Production-Ready)
# ==============================================================================
# Proje: StockOrchestra-MCP
# Faz: 8 - Cloud Native & Kubernetes
# Açıklama: Multi-stage, güvenlik odaklı, Alpine tabanlı production build
# ==============================================================================

# ----------------------------------------------------------------------------
# Build Stage - Build .NET application
# ----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY src/ApiGateway/ApiGateway.csproj ./src/ApiGateway/
COPY shared/src/Shared/Shared.csproj ./shared/src/Shared/
RUN dotnet restore src/ApiGateway/ApiGateway.csproj

# Copy all source files
COPY src/ApiGateway/ ./src/ApiGateway/
COPY shared/src/Shared/ ./shared/src/Shared/

# Build the application
WORKDIR /src/src/ApiGateway
RUN dotnet publish ApiGateway.csproj -c Release -o /app/publish

# ----------------------------------------------------------------------------
# Runtime Stage - Minimal Alpine image with non-root user
# ----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime

# Security: Create non-root user
# ----------------------------------------------------------------------------
# alpine'de default kullanıcı root'tur. Güvenlik için app user oluşturuyoruz.
RUN addgroup -g 1000 appgroup && \
    adduser -u 1000 -G appgroup -s /bin/sh -D appuser

WORKDIR /app

# Copy published files from build stage
COPY --from=build /app/publish ./

# Security: Set file permissions for non-root user
RUN chown -R appuser:appgroup /app

# Switch to non-root user
USER appuser

# Expose HTTP port
EXPOSE 8080

# Health check endpoint
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -q -O /dev/null http://localhost:8080/health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "ApiGateway.dll"]