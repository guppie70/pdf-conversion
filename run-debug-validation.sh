#!/bin/bash

echo "Triggering round-trip validation for project ar24-3..."

# Use curl to trigger the validation via the API or directly through the container
docker exec taxxor-pdfconversion-1 dotnet exec /app/PdfConversion.dll --run-validation ar24-3 2>/dev/null || {
    echo "Direct execution not available, triggering via HTTP..."

    # Alternative: Call the debug page via curl if it's accessible
    curl -X POST http://localhost:8085/api/validation/roundtrip/ar24-3 2>/dev/null || {
        echo "API endpoint not available."
        echo ""
        echo "Please navigate to http://localhost:8085 and run the validation manually."
        echo "Or navigate to http://localhost:8085/debug-validation to use the debug page."
    }
}

echo ""
echo "Checking for debug files..."
sleep 2

DEBUG_DIR="/Users/jthijs/Documents/my_projects/taxxor/tdm/_utils/pdf-conversion/data/output/optiver/projects/ar24-3/debug"

if [ -d "$DEBUG_DIR" ]; then
    echo "Debug files found in: $DEBUG_DIR"
    echo ""
    ls -lh "$DEBUG_DIR"
    echo ""
    echo "File sizes:"
    for file in "$DEBUG_DIR"/*.xml; do
        if [ -f "$file" ]; then
            size=$(wc -l "$file" | awk '{print $1}')
            echo "  $(basename "$file"): $size lines"
        fi
    done
else
    echo "Debug directory not found. Please run validation manually from the UI."
fi