"""Docling document conversion service."""

import os
import logging
from pathlib import Path
from typing import Tuple, Optional
from fastapi import UploadFile

try:
    from docling.document_converter import DocumentConverter
    from docling.datamodel.base_models import InputFormat
    from docling.datamodel.pipeline_options import PdfPipelineOptions
    DOCLING_AVAILABLE = True
except ImportError as e:
    logger.warning(f"Docling not available: {e}")
    DOCLING_AVAILABLE = False

logger = logging.getLogger(__name__)


class DoclingConverter:
    """Handles document conversion using Docling library."""

    def __init__(self):
        """Initialize the converter."""
        self.supported_extensions = {'.pdf', '.docx', '.doc'}
        self.supported_formats = ['docbook', 'html', 'markdown']

        # Initialize Docling converter if available
        if DOCLING_AVAILABLE:
            self.converter = DocumentConverter()
            logger.info("Docling converter initialized successfully")
        else:
            self.converter = None
            logger.warning("Docling converter not available - using placeholder mode")

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

    async def convert(
        self,
        file: UploadFile,
        project_id: str,
        output_format: str = "docbook"
    ) -> Tuple[str, Optional[int], Optional[str]]:
        """
        Convert uploaded document to specified format.

        Args:
            file: Uploaded file
            project_id: Project ID for organizing output
            output_format: Target format (docbook, html, markdown)

        Returns:
            Tuple of (output_file_path, page_count, error_message)
            output_file_path is relative to /app/data

        Raises:
            ValueError: If file type or format is not supported
            Exception: If conversion fails
        """
        # Validate file type
        if not self.validate_file(file.filename):
            raise ValueError(
                f"Unsupported file type. Supported: {', '.join(self.supported_extensions)}"
            )

        # Validate output format
        if output_format not in self.supported_formats:
            raise ValueError(
                f"Unsupported output format. Supported: {', '.join(self.supported_formats)}"
            )

        try:
            # Create project input directory
            input_dir = Path(f"/app/data/input/optiver/projects/{project_id}")
            input_dir.mkdir(parents=True, exist_ok=True)

            # Save uploaded file temporarily
            input_file_path = input_dir / file.filename
            logger.info(f"Saving uploaded file to {input_file_path}")

            contents = await file.read()
            with open(input_file_path, "wb") as f:
                f.write(contents)

            # Prepare output path
            output_filename = "docling-output.xml"
            output_file_path = input_dir / output_filename

            # Perform real Docling conversion if available
            if self.converter and DOCLING_AVAILABLE:
                logger.info(f"Converting {input_file_path} with Docling to {output_format}")
                page_count = self._convert_with_docling(
                    input_file_path,
                    output_file_path,
                    output_format
                )
            else:
                # Fallback to placeholder for testing
                logger.warning("Using placeholder conversion (Docling not available)")
                page_count = self._create_placeholder_output(
                    input_file_path,
                    output_file_path,
                    output_format
                )

            # Return path relative to /app/data
            relative_path = f"input/optiver/projects/{project_id}/{output_filename}"
            logger.info(f"Conversion completed: {relative_path}")

            return relative_path, page_count, None

        except Exception as e:
            logger.error(f"Conversion failed: {str(e)}", exc_info=True)
            raise

    def _convert_with_docling(
        self,
        input_path: Path,
        output_path: Path,
        output_format: str
    ) -> int:
        """
        Convert document using Docling library.

        Args:
            input_path: Path to input file
            output_path: Path to output file
            output_format: Target format (docbook, html, markdown)

        Returns:
            Number of pages in document
        """
        try:
            logger.info(f"Starting Docling conversion: {input_path}")

            # Convert document with Docling
            result = self.converter.convert(str(input_path))

            # Get page count from result
            page_count = len(result.document.pages) if hasattr(result.document, 'pages') else 1
            logger.info(f"Docling conversion complete: {page_count} pages")

            # Export based on format
            if output_format == "markdown":
                content = result.document.export_to_markdown()
            elif output_format == "html":
                content = result.document.export_to_html()
            else:  # docbook format
                # Docling doesn't directly support DocBook, so we export to HTML
                # and wrap it in a basic DocBook structure
                html_content = result.document.export_to_html()
                content = self._wrap_html_in_docbook(html_content, input_path.name)

            # Save output
            with open(output_path, "w", encoding="utf-8") as f:
                f.write(content)

            logger.info(f"Output saved to {output_path}")
            return page_count

        except Exception as e:
            logger.error(f"Docling conversion failed: {str(e)}", exc_info=True)
            raise

    def _wrap_html_in_docbook(self, html_content: str, source_filename: str) -> str:
        """
        Wrap HTML content in a basic DocBook XML structure.

        Args:
            html_content: HTML content from Docling
            source_filename: Name of source file

        Returns:
            DocBook XML string
        """
        return f"""<?xml version="1.0" encoding="UTF-8"?>
<book xmlns="http://docbook.org/ns/docbook" version="5.0">
    <info>
        <title>Document Conversion</title>
        <subtitle>Converted from {source_filename}</subtitle>
    </info>
    <chapter>
        <title>Content</title>
        <section>
            <title>Document Body</title>
            {html_content}
        </section>
    </chapter>
</book>"""

    def _create_placeholder_output(
        self,
        input_path: Path,
        output_path: Path,
        output_format: str
    ) -> int:
        """
        Create placeholder output for testing.
        This will be replaced with actual Docling conversion.

        Args:
            input_path: Path to input file
            output_path: Path to output file
            output_format: Target format

        Returns:
            Placeholder page count
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

        with open(output_path, "w", encoding="utf-8") as f:
            f.write(placeholder_content)

        # Return placeholder page count
        return 1
