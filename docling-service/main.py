"""
Docling Conversion Service - FastAPI application

Converts PDF and Word documents to structured XML formats using the Docling library.
"""

import logging
from fastapi import FastAPI, UploadFile, File, Form, HTTPException
from fastapi.responses import JSONResponse
from models.schemas import (
    HealthResponse,
    ConversionResponse,
    SupportedFormatsResponse
)
from services.docling_converter import DoclingConverter

# Configure logging
logging.basicConfig(
    level=logging.INFO,
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


@app.post("/convert", response_model=ConversionResponse, tags=["Conversion"])
async def convert_document(
    file: UploadFile = File(..., description="PDF or Word document to convert"),
    project_id: str = Form(..., description="Project ID (e.g., 'ar24-3')"),
    output_format: str = Form(default="docbook", description="Output format (docbook, html, markdown)")
):
    """
    Convert PDF or Word document to structured XML.

    **Process:**
    1. Validates file type (PDF, DOCX, DOC)
    2. Saves file to project input directory
    3. Converts document using Docling library
    4. Saves output as `docling-output.xml` in project directory
    5. Returns output file path and page count

    **File Location:**
    - Input: `data/input/optiver/projects/{project_id}/{filename}`
    - Output: `data/input/optiver/projects/{project_id}/docling-output.xml`

    **Example:**
    ```bash
    curl -X POST http://localhost:4807/convert \\
      -F "file=@annual-report.pdf" \\
      -F "project_id=ar24-3" \\
      -F "output_format=docbook"
    ```
    """
    logger.info(f"Conversion request: file={file.filename}, project={project_id}, format={output_format}")

    try:
        # Validate file type
        if not converter.validate_file(file.filename):
            raise HTTPException(
                status_code=400,
                detail=f"Unsupported file type. Supported extensions: {', '.join(converter.supported_extensions)}"
            )

        # Convert document
        output_file, page_count, error = await converter.convert(
            file=file,
            project_id=project_id,
            output_format=output_format
        )

        if error:
            raise HTTPException(status_code=500, detail=error)

        return ConversionResponse(
            success=True,
            output_file=output_file,
            page_count=page_count,
            message=f"Successfully converted {file.filename} to {output_format}"
        )

    except ValueError as e:
        logger.error(f"Validation error: {str(e)}")
        raise HTTPException(status_code=400, detail=str(e))

    except Exception as e:
        logger.error(f"Conversion failed: {str(e)}", exc_info=True)
        raise HTTPException(
            status_code=500,
            detail=f"Conversion failed: {str(e)}"
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


# Startup event
@app.on_event("startup")
async def startup_event():
    """Log startup information."""
    logger.info("=" * 60)
    logger.info("Docling Conversion Service Starting")
    logger.info("Version: 1.0.0")
    logger.info("Swagger UI: http://localhost:4808/swagger-ui")
    logger.info("=" * 60)


# Shutdown event
@app.on_event("shutdown")
async def shutdown_event():
    """Log shutdown information."""
    logger.info("Docling Conversion Service Shutting Down")
