"""Pydantic schemas for request and response models."""

from pydantic import BaseModel, Field
from typing import Optional


class HealthResponse(BaseModel):
    """Health check response."""
    status: str = Field(..., description="Health status (healthy/unhealthy)")
    version: str = Field(..., description="Service version")


class ConversionRequest(BaseModel):
    """Request model for document conversion (form data, not used directly but documents the API)."""
    project_id: str = Field(..., description="Project ID for organizing output")
    output_format: str = Field(default="docbook", description="Output format (docbook, html, markdown)")


class ConversionResponse(BaseModel):
    """Response model for conversion result."""
    success: bool = Field(..., description="Whether conversion succeeded")
    output_file: str = Field(..., description="Path to output file relative to /app/data")
    page_count: Optional[int] = Field(None, description="Number of pages in document")
    message: Optional[str] = Field(None, description="Success or error message")


class SupportedFormatsResponse(BaseModel):
    """Response listing supported conversion formats."""
    formats: list[str] = Field(..., description="List of supported output formats")
