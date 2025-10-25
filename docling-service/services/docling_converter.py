"""Docling document conversion service."""

import os
import logging
from pathlib import Path
from typing import Tuple, Optional
from fastapi import UploadFile

# Note: Docling import will be added after confirming the actual API
# For now, this is a placeholder implementation that will be completed in Phase 1.2

logger = logging.getLogger(__name__)


class DoclingConverter:
    """Handles document conversion using Docling library."""

    def __init__(self):
        """Initialize the converter."""
        self.supported_extensions = {'.pdf', '.docx', '.doc'}
        self.supported_formats = ['docbook', 'html', 'markdown']

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

            # TODO: Actual Docling conversion will be implemented here
            # For now, create a placeholder output for testing
            logger.info(f"Converting {input_file_path} to {output_format}")

            # This is a placeholder - real implementation will use Docling library
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
