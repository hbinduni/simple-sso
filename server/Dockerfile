# .NET Server Dockerfile - multi-stage build for a minimal production image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first and restore — keeps the restore layer cached.
COPY server/Server.csproj ./server/
RUN dotnet restore ./server/Server.csproj

# Copy the rest of the server source and publish.
COPY server/ ./server/
RUN dotnet publish ./server/Server.csproj -c Release -o /app/publish /p:UseAppHost=false

# Production image - minimal Alpine ASP.NET runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

# Runtime dependencies (wget is used by the healthcheck)
RUN apk --no-cache add ca-certificates tzdata wget

# Create non-root user
RUN addgroup -g 1001 -S appgroup && \
    adduser -u 1001 -S appuser -G appgroup

WORKDIR /app
COPY --from=build --chown=appuser:appgroup /app/publish ./

USER appuser

ENV ASPNETCORE_URLS=http://+:3000 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    PORT=3000

EXPOSE 3000

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:3000/health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "server.dll"]
