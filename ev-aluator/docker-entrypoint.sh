#!/bin/bash
set -e

echo "Starting EV Range Analyzer..."

# Start backend API in background
echo "Starting backend API on port 5000..."
cd /app/backend
dotnet EVRangeAnalyzer.dll &
BACKEND_PID=$!

# Wait for backend to be ready
echo "Waiting for backend to start..."
for i in {1..30}; do
    if curl -f http://localhost:5000/health 2>/dev/null; then
        echo "Backend is ready!"
        break
    fi
    if [ $i -eq 30 ]; then
        echo "Backend failed to start within 30 seconds"
        exit 1
    fi
    sleep 1
done

# Start frontend server
echo "Starting frontend server on port 3000..."
cd /app/frontend
npm run preview -- --port 3000 --host 0.0.0.0 &
FRONTEND_PID=$!

# Function to cleanup on exit
cleanup() {
    echo "Shutting down..."
    kill $BACKEND_PID $FRONTEND_PID 2>/dev/null || true
    wait $BACKEND_PID $FRONTEND_PID 2>/dev/null || true
    exit 0
}

# Set trap to cleanup on exit
trap cleanup SIGTERM SIGINT

# Wait for both processes
echo "EV Range Analyzer is running!"
echo "Backend API: http://localhost:5000"
echo "Frontend UI: http://localhost:3000"

# Keep the script running
wait $BACKEND_PID $FRONTEND_PID