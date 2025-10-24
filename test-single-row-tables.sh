#!/bin/bash

echo "Testing single-row table transformation..."
echo ""
echo "=========================================="
echo "TRANSFORMATION OUTPUT:"
echo "=========================================="

curl -s http://localhost:8085/transform-test

echo ""
echo "=========================================="
echo "Test complete!"
echo "=========================================="
