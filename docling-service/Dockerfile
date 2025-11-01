# Multi-stage build for optimized docling-service image
FROM python:3.11-slim AS base

# Set platform explicitly for ML library compatibility
ARG TARGETPLATFORM=linux/amd64

# Install system dependencies in a single layer
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        libgl1 \
        libglib2.0-0 \
        libgomp1 \
        wget \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN useradd -m -u 1000 -s /bin/bash appuser

# Set working directory
WORKDIR /app

# Copy requirements first for better layer caching
COPY requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Grant appuser write permissions to site-packages for runtime model downloads
# This allows rapidocr and other packages to download models at runtime
RUN chown -R appuser:appuser /usr/local/lib/python3.11/site-packages

# Copy application code
COPY . .

# Change ownership to non-root user
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 4808

# Health check (optional, but good practice)
HEALTHCHECK --interval=30s --timeout=3s --start-period=30s --retries=3 \
    CMD wget --no-verbose --tries=1 -O /dev/null http://localhost:4808/health || exit 1

# Default command with auto-reload for development
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "4808", "--reload"]
