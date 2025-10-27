#!/bin/bash
set -euo pipefail

# Configuration
IMAGE_NAME="taxxor-docling-service"
TAG="local"
DOCKERFILE="docling-service/Dockerfile"
BUILD_CONTEXT="docling-service"

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Error handler
error_exit() {
    echo -e "${RED}✗ Error: $1${NC}" >&2
    exit 1
}

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    error_exit "Docker is not running. Please start Docker Desktop and try again."
fi

# Check if Dockerfile exists
if [ ! -f "${DOCKERFILE}" ]; then
    error_exit "Dockerfile not found at: ${DOCKERFILE}"
fi

# Check if requirements.txt exists
if [ ! -f "${BUILD_CONTEXT}/requirements.txt" ]; then
    error_exit "requirements.txt not found in ${BUILD_CONTEXT}/"
fi

echo -e "${BLUE}Building docling-service Docker image...${NC}"
echo "Image: ${IMAGE_NAME}:${TAG}"
echo "Context: ${BUILD_CONTEXT}"
echo "Platform: linux/amd64"
echo ""

# Build the image
echo -e "${YELLOW}Building image (this may take a few minutes on first build)...${NC}"
if docker build \
    --platform linux/amd64 \
    --tag "${IMAGE_NAME}:${TAG}" \
    --file "${DOCKERFILE}" \
    "${BUILD_CONTEXT}"; then

    echo ""
    echo -e "${GREEN}✓ Build complete!${NC}"
    echo ""
    echo "Image: ${IMAGE_NAME}:${TAG}"
    echo ""
    echo "To use this image:"
    echo "  1. Start services: npm start"
    echo "  2. Access docling-service at: http://localhost:4808"
    echo ""
    echo "To rebuild after dependency changes:"
    echo "  ./build-docling.sh"
    echo ""
else
    error_exit "Docker build failed. Check the error messages above."
fi
