# Dockerfile for PDF Conversion Blazor Server Application
# Multi-stage build supporting ARM64 and AMD64 architectures

# Build stage - using SDK image for compilation
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG TARGETARCH
ARG BUILDPLATFORM

WORKDIR /source

# Copy csproj and restore dependencies (leverages Docker cache)
COPY *.csproj .
RUN dotnet restore -a $TARGETARCH

# Copy everything else and build
COPY . .
RUN dotnet publish -c Release -a $TARGETARCH --no-restore -o /app/publish

# Runtime stage - using smaller ASP.NET image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# Install additional dependencies for PDF generation and file operations
RUN apt-get update && apt-get install -y \
    wget \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -g 1000 appuser && \
    useradd -u 1000 -g appuser -s /bin/bash -m appuser

# Create application directories
WORKDIR /app
RUN mkdir -p /app/data/input /app/data/output /app/xslt && \
    chown -R appuser:appuser /app

# Copy published application
COPY --from=build /app/publish .

# Configure ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8085 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Health check configuration
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8085/health || exit 1

# Switch to non-root user
USER appuser

# Expose application port
EXPOSE 8085

# Start application
ENTRYPOINT ["dotnet", "PdfConversion.dll"]
