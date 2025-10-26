"""Job status models for async PDF conversion."""

from datetime import datetime
from enum import Enum
from typing import Optional
from pydantic import BaseModel, Field


class JobStatus(str, Enum):
    """Job processing status."""
    QUEUED = "queued"
    PROCESSING = "processing"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"


class JobInfo(BaseModel):
    """Job information and status."""
    job_id: str = Field(..., description="Unique job identifier")
    status: JobStatus = Field(..., description="Current job status")
    progress: float = Field(0.0, ge=0.0, le=1.0, description="Progress from 0.0 to 1.0")
    current_page: Optional[int] = Field(None, description="Current page being processed")
    total_pages: Optional[int] = Field(None, description="Total pages in document")
    message: str = Field("", description="Status message")
    created_at: datetime = Field(default_factory=datetime.utcnow, description="Job creation timestamp")
    started_at: Optional[datetime] = Field(None, description="Processing start timestamp")
    completed_at: Optional[datetime] = Field(None, description="Completion timestamp")
    error: Optional[str] = Field(None, description="Error message if failed")

    # Request metadata
    filename: Optional[str] = Field(None, description="Original filename")
    output_format: Optional[str] = Field(None, description="Output format")

    # Conversion result (stored when completed)
    output_content: Optional[str] = Field(None, description="Converted content with base64-embedded images")


class JobStartResponse(BaseModel):
    """Response when starting a new job."""
    job_id: str = Field(..., description="Unique job identifier for tracking")
    message: str = Field(..., description="Status message")


class JobResultResponse(BaseModel):
    """Response containing job result."""
    job_id: str = Field(..., description="Job identifier")
    success: bool = Field(..., description="Whether conversion succeeded")
    output_content: Optional[str] = Field(None, description="Converted HTML/XML content with base64-embedded images")
    page_count: Optional[int] = Field(None, description="Number of pages processed")
    error: Optional[str] = Field(None, description="Error message if failed")
