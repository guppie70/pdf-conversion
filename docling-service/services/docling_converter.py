"""Docling document conversion service."""

import os
import json
import logging
from pathlib import Path
from typing import Tuple, Optional, Callable, Awaitable
from fastapi import UploadFile

# Initialize logger first
logger = logging.getLogger(__name__)

try:
    from docling.document_converter import DocumentConverter, PdfFormatOption
    from docling.datamodel.base_models import DocumentStream, PipelineOptions, InputFormat
    from docling.datamodel.pipeline_options import PdfPipelineOptions
    from docling_core.types.doc import ImageRefMode
    DOCLING_AVAILABLE = True
except ImportError as e:
    logger.warning(f"Docling not available: {e}")
    DOCLING_AVAILABLE = False


class DoclingConverter:
    """Handles document conversion using Docling library."""

    def __init__(self):
        """Initialize the converter."""
        self.supported_extensions = {'.pdf', '.docx', '.doc'}
        self.supported_formats = ['docbook', 'html', 'markdown']

        # Don't initialize converter yet - will be lazy-loaded on first use
        # This avoids downloading models during service startup
        self.converter = None
        self._models_downloaded = False

        if DOCLING_AVAILABLE:
            logger.info("Docling library available - will initialize on first use")
        else:
            logger.warning("Docling library not available - will use placeholder mode")

    def _ensure_converter_initialized(self):
        """
        Lazily initialize the Docling converter and download models if needed.

        This is called on first conversion attempt to avoid blocking service startup.
        """
        if not DOCLING_AVAILABLE:
            return False

        if self.converter is not None:
            return True

        try:
            logger.info("Initializing Docling converter (first use)...")

            # Download models if not already cached
            if not self._models_downloaded:
                logger.info("Downloading Docling models from HuggingFace (this may take a moment)...")
                try:
                    DocumentConverter.download_models_hf()
                    self._models_downloaded = True
                    logger.info("Models downloaded successfully")
                except Exception as e:
                    logger.warning(f"Model download failed, will try direct initialization: {e}")

            # Configure pipeline options for image extraction
            pipeline_options = PdfPipelineOptions()
            pipeline_options.images_scale = 2.0  # 144 DPI (2.0 * 72 DPI)
            pipeline_options.generate_page_images = False  # Don't need full page images
            pipeline_options.generate_picture_images = True  # Extract figures/pictures

            # Initialize converter with image extraction enabled
            self.converter = DocumentConverter(
                format_options={
                    InputFormat.PDF: PdfFormatOption(pipeline_options=pipeline_options)
                }
            )
            logger.info("Docling converter initialized successfully with image extraction enabled")
            return True

        except Exception as e:
            logger.error(f"Failed to initialize Docling converter: {e}", exc_info=True)
            return False

    def validate_file(self, filename: str) -> bool:
        """
        Validate if the file type is supported.

        Args:
            filename: Name of the file to validate

        Returns:
            True if file type is supported, False otherwise
        """
        ext = Path(filename).suffix.lower()
        return ext in self.supported_extensions



    async def convert_with_progress(
        self,
        input_file_path: Path,
        output_format: str,
        progress_callback: Optional[Callable[[float, int, int, str], Awaitable[None]]] = None
    ) -> Tuple[Optional[str], Optional[int], Optional[str]]:
        """
        Convert document with progress tracking for background jobs.

        Args:
            input_file_path: Path to input file
            output_format: Target format (html, markdown, docbook)
            progress_callback: Async callback for progress updates
                              Accepts (progress, current_page, total_pages, message)

        Returns:
            Tuple of (output_content, page_count, error_message)
            output_content contains HTML/XML with base64-embedded images
        """
        try:
            # Report starting
            if progress_callback:
                await progress_callback(0.05, 0, 0, "Initializing conversion...")

            # Try to initialize converter if not already done
            converter_ready = self._ensure_converter_initialized()

            if progress_callback:
                await progress_callback(0.10, 0, 0, "Converter initialized")

            # Perform conversion
            if converter_ready and self.converter:
                logger.info(f"Converting {input_file_path} with Docling to {output_format}")

                if progress_callback:
                    await progress_callback(0.15, 0, 0, "Starting Docling conversion...")

                content, page_count = await self._convert_with_docling_async(
                    input_file_path,
                    output_format,
                    progress_callback
                )
            else:
                # Fallback to placeholder for testing
                logger.warning("Using placeholder conversion (Docling not available)")

                if progress_callback:
                    await progress_callback(0.20, 0, 0, "Using placeholder conversion...")

                content, page_count = self._create_placeholder_output(
                    input_file_path,
                    output_format
                )

            # Report completion
            if progress_callback:
                await progress_callback(1.0, page_count, page_count, "Conversion complete")

            logger.info(f"Conversion completed: {page_count} pages, {len(content)} bytes")

            return content, page_count, None

        except Exception as e:
            logger.error(f"Conversion failed: {str(e)}", exc_info=True)
            return None, None, str(e)

    async def _convert_with_docling_async(
        self,
        input_path: Path,
        output_format: str,
        progress_callback: Optional[Callable[[float, int, int, str], Awaitable[None]]] = None
    ) -> Tuple[str, int]:
        """
        Convert document using Docling library with progress tracking.

        Args:
            input_path: Path to input file
            output_format: Target format (html, markdown, docbook)
            progress_callback: Optional async callback for progress updates

        Returns:
            Tuple of (content, page_count)
            content contains HTML/XML with base64-embedded images
        """
        try:
            logger.info(f"Starting Docling conversion: {input_path}")

            if progress_callback:
                await progress_callback(0.20, 0, 0, "Processing PDF pages (typically takes 5-10 minutes)...")

            # Convert document with Docling (this is CPU-intensive and blocking)
            # We'll run it in a thread pool to avoid blocking the event loop
            import asyncio
            import threading
            import time

            loop = asyncio.get_event_loop()

            # Start heartbeat thread to provide periodic updates
            heartbeat_active = threading.Event()
            heartbeat_active.set()

            def send_heartbeat():
                elapsed = 0
                while heartbeat_active.is_set():
                    time.sleep(30)  # Every 30 seconds
                    if heartbeat_active.is_set():
                        elapsed += 30
                        asyncio.run_coroutine_threadsafe(
                            progress_callback(0.20, 0, 0,
                                f"Processing PDF pages... ({elapsed}s elapsed)"),
                            loop
                        )

            heartbeat_thread = threading.Thread(target=send_heartbeat, daemon=True)
            heartbeat_thread.start()

            try:
                # Run conversion in executor
                result = await loop.run_in_executor(
                    None,
                    self.converter.convert,
                    str(input_path)
                )
            finally:
                # Stop heartbeat thread
                heartbeat_active.clear()

            # Get page count from result
            page_count = len(result.document.pages) if hasattr(result.document, 'pages') else 1
            logger.info(f"Docling conversion complete: {page_count} pages")

            if progress_callback:
                await progress_callback(0.60, page_count, page_count, f"Processed {page_count} pages, exporting with base64 images...")

            # Count images
            image_count = len(result.document.pictures) if hasattr(result.document, 'pictures') and result.document.pictures else 0
            logger.info(f"Found {image_count} images to embed as base64")

            if progress_callback:
                await progress_callback(0.70, page_count, page_count, f"Embedding {image_count} images as base64...")

            # Export based on format with EMBEDDED base64 images
            if output_format == "markdown":
                content = result.document.export_to_markdown(image_mode=ImageRefMode.EMBEDDED)
            elif output_format == "doctags":
                # DocTags format - structured for LLMs
                content = result.document.export_to_doctags()
            elif output_format == "json":
                # JSON format - lossless representation
                content = json.dumps(result.document.export_to_dict(), indent=2)
            else:  # html or xml - always return HTML (Blazor will convert to XML if needed)
                content = result.document.export_to_html(image_mode=ImageRefMode.EMBEDDED)

            if progress_callback:
                await progress_callback(0.90, page_count, page_count, "Finalizing output...")

            logger.info(f"Generated output: {len(content)} bytes with {image_count} embedded images")
            return content, page_count

        except Exception as e:
            logger.error(f"Docling conversion failed: {str(e)}", exc_info=True)
            raise

    def _create_placeholder_output(
        self,
        input_path: Path,
        output_format: str
    ) -> Tuple[str, int]:
        """
        Create placeholder output for testing.
        This will be replaced with actual Docling conversion.

        Args:
            input_path: Path to input file
            output_format: Target format

        Returns:
            Tuple of (content, page_count)
        """
        # Create a simple DocBook XML placeholder
        placeholder_content = """<?xml version="1.0" encoding="UTF-8"?>
<book xmlns="http://docbook.org/ns/docbook" version="5.0">
    <info>
        <title>Placeholder Document</title>
        <subtitle>Converted by Docling Service</subtitle>
    </info>
    <chapter>
        <title>Placeholder Chapter</title>
        <para>
            This is a placeholder output from the Docling service.
            The actual Docling conversion will be implemented in Phase 1.2.
        </para>
        <para>
            Source file: {}
        </para>
        <para>
            Output format: {}
        </para>
    </chapter>
</book>""".format(input_path.name, output_format)

        # Return placeholder content and page count
        return placeholder_content, 1
