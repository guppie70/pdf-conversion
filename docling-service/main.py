"""
Docling Conversion Service - FastAPI application

Converts PDF and Word documents to structured XML formats using the Docling library.
"""

import logging
from pathlib import Path
from fastapi import FastAPI, UploadFile, File, Form, HTTPException
from fastapi.responses import JSONResponse
from models.schemas import (
    HealthResponse,
    SupportedFormatsResponse
)
from models.job_models import (
    JobInfo,
    JobStartResponse,
    JobResultResponse
)
from services.docling_converter import DoclingConverter
from services.job_manager import get_job_manager

# Configure logging
# Enable DEBUG level to capture Docling's internal processing stages
logging.basicConfig(
    level=logging.DEBUG,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

# Create FastAPI app with metadata for Swagger
app = FastAPI(
    title="Docling Conversion Service",
    description="""
    Convert PDF and Word documents to structured XML using Docling library.

    **Features:**
    - PDF to DocBook XML conversion
    - Word (DOCX/DOC) to DocBook XML conversion
    - Support for multiple output formats
    - Hot-reload during development

    **Workflow:**
    1. Upload PDF or Word document
    2. Specify project ID for organization
    3. Select output format (default: DocBook XML)
    4. Receive converted file location and metadata
    """,
    version="1.0.0",
    docs_url="/swagger-ui",
    redoc_url="/redoc"
)

# Initialize converter
converter = DoclingConverter()


@app.get("/health", response_model=HealthResponse, tags=["Health"])
async def health_check():
    """
    Health check endpoint.

    Returns service status and version information.
    """
    return HealthResponse(
        status="healthy",
        version="1.0.0"
    )


@app.get("/formats", response_model=SupportedFormatsResponse, tags=["Info"])
async def get_supported_formats():
    """
    List supported output formats.

    Returns all formats that can be used for document conversion.
    """
    return SupportedFormatsResponse(
        formats=converter.supported_formats
    )


@app.get("/", tags=["Info"])
async def root():
    """
    Root endpoint - redirects to Swagger UI.
    """
    return {
        "service": "Docling Conversion Service",
        "version": "1.0.0",
        "docs": "/swagger-ui",
        "health": "/health"
    }


# Async job endpoints

@app.post("/convert-async", response_model=JobStartResponse, tags=["Conversion"])
async def convert_document_async(
    file: UploadFile = File(..., description="PDF or Word document to convert"),
    output_format: str = Form(default="html", description="Output format (html, markdown, docbook)")
):
    """
    Start async conversion job - returns immediately with job ID.

    **Process:**
    1. Validates file type (PDF, DOCX, DOC)
    2. Saves file to temporary directory
    3. Creates background job and returns job ID
    4. Client polls /jobs/{job_id} for status and progress
    5. Upon completion, result contains HTML/XML with base64-embedded images

    **Example:**
    ```bash
    curl -X POST http://localhost:4808/convert-async \\
      -F "file=@annual-report.pdf" \\
      -F "output_format=html"
    ```

    Returns job_id for tracking progress via /jobs/{job_id}
    """
    logger.info(f"Async conversion request: file={file.filename}, format={output_format}")

    try:
        # Validate file type
        if not converter.validate_file(file.filename):
            raise HTTPException(
                status_code=400,
                detail=f"Unsupported file type. Supported extensions: {', '.join(converter.supported_extensions)}"
            )

        # Save file to temporary directory
        temp_dir = Path("/app/data/temp")
        temp_dir.mkdir(parents=True, exist_ok=True)

        input_file_path = temp_dir / file.filename
        logger.info(f"Saving uploaded file to {input_file_path}")

        contents = await file.read()
        with open(input_file_path, "wb") as f:
            f.write(contents)

        # Create job
        job_manager = get_job_manager()
        job_id = job_manager.create_job(
            filename=file.filename,
            output_format=output_format
        )

        # Define conversion function for background worker
        async def conversion_task(job_id: str, progress_callback):
            return await converter.convert_with_progress(
                input_file_path=input_file_path,
                output_format=output_format,
                progress_callback=progress_callback
            )

        # Enqueue job
        await job_manager.enqueue_job(job_id, conversion_task)

        return JobStartResponse(
            job_id=job_id,
            message=f"Conversion job started for {file.filename}"
        )

    except ValueError as e:
        logger.error(f"Validation error: {str(e)}")
        raise HTTPException(status_code=400, detail=str(e))

    except Exception as e:
        logger.error(f"Failed to start conversion job: {str(e)}", exc_info=True)
        raise HTTPException(
            status_code=500,
            detail=f"Failed to start conversion: {str(e)}"
        )


@app.get("/jobs/{job_id}", response_model=JobInfo, tags=["Jobs"])
async def get_job_status(job_id: str):
    """
    Get status and progress of a conversion job.

    **Returns:**
    - job_id: Job identifier
    - status: queued, processing, completed, failed, cancelled
    - progress: 0.0 to 1.0
    - current_page: Current page being processed
    - total_pages: Total page count
    - message: Status message
    - created_at, started_at, completed_at: Timestamps

    **Example:**
    ```bash
    curl http://localhost:4808/jobs/{job_id}
    ```

    Poll this endpoint every 2 seconds to get real-time progress.
    """
    job_manager = get_job_manager()
    job = job_manager.get_job(job_id)

    if not job:
        raise HTTPException(status_code=404, detail=f"Job {job_id} not found")

    return job


@app.get("/jobs/{job_id}/result", response_model=JobResultResponse, tags=["Jobs"])
async def get_job_result(job_id: str):
    """
    Get final result of completed conversion job.

    **Only works for completed jobs.** Returns HTML/XML content with base64-embedded images.

    **Example:**
    ```bash
    curl http://localhost:4808/jobs/{job_id}/result
    ```

    **Response:** JSON with output_content containing full HTML/XML with embedded images
    """
    job_manager = get_job_manager()
    job = job_manager.get_job(job_id)

    if not job:
        raise HTTPException(status_code=404, detail=f"Job {job_id} not found")

    if job.status == "processing" or job.status == "queued":
        raise HTTPException(status_code=425, detail="Job not yet completed")

    if job.status == "failed":
        return JobResultResponse(
            job_id=job_id,
            success=False,
            error=job.error
        )

    if job.status == "cancelled":
        return JobResultResponse(
            job_id=job_id,
            success=False,
            error="Job was cancelled"
        )

    # Job completed successfully - return stored content
    return JobResultResponse(
        job_id=job_id,
        success=True,
        output_content=job.output_content,
        page_count=job.total_pages
    )


@app.delete("/jobs/{job_id}", tags=["Jobs"])
async def cancel_job(job_id: str):
    """
    Cancel a running job.

    **Only works for queued or processing jobs.**

    **Example:**
    ```bash
    curl -X DELETE http://localhost:4808/jobs/{job_id}
    ```
    """
    job_manager = get_job_manager()
    cancelled = job_manager.cancel_job(job_id)

    if not cancelled:
        job = job_manager.get_job(job_id)
        if not job:
            raise HTTPException(status_code=404, detail=f"Job {job_id} not found")
        else:
            raise HTTPException(
                status_code=400,
                detail=f"Job cannot be cancelled (status: {job.status})"
            )

    return {"message": f"Job {job_id} cancelled"}


# Startup event
@app.on_event("startup")
async def startup_event():
    """Log startup information and start job manager."""
    logger.info("=" * 60)
    logger.info("Docling Conversion Service Starting")
    logger.info("Version: 1.0.0")
    logger.info("Swagger UI: http://localhost:4808/swagger-ui")
    logger.info("=" * 60)

    # Start job manager background worker
    job_manager = get_job_manager()
    await job_manager.start_worker()
    logger.info("Job manager worker started")


# Shutdown event
@app.on_event("shutdown")
async def shutdown_event():
    """Log shutdown information and stop job manager."""
    logger.info("Docling Conversion Service Shutting Down")

    # Stop job manager background worker
    job_manager = get_job_manager()
    await job_manager.stop_worker()
    logger.info("Job manager worker stopped")
