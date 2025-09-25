# Multi-stage Dockerfile for EV Range Analyzer
# Stage 1: Build the backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /app/backend

# Copy backend project files
COPY backend/EVRangeAnalyzer.csproj .
COPY backend/stylecop.ruleset .
COPY backend/.editorconfig .

# Restore backend dependencies
RUN dotnet restore

# Copy backend source code
COPY backend/src/ ./src/

# Build backend application
RUN dotnet publish -c Release -o /app/backend/publish

# Stage 2: Build the frontend
FROM node:20-alpine AS frontend-build
WORKDIR /app/frontend

# Copy frontend package files
COPY frontend/package.json ./
COPY frontend/package-lock.json ./

# Install frontend dependencies
RUN npm ci --only=production

# Copy frontend source code
COPY frontend/ ./

# Build frontend application
RUN npm run build

# Stage 3: Runtime image with both applications
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install Node.js for serving frontend (if needed)
RUN apt-get update && apt-get install -y \
    curl \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y nodejs \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Create directory structure
RUN mkdir -p /app/backend /app/frontend

# Copy backend application
COPY --from=backend-build /app/backend/publish /app/backend/

# Copy frontend built files
COPY --from=frontend-build /app/frontend/dist /app/frontend/dist/
COPY --from=frontend-build /app/frontend/package.json /app/frontend/
COPY --from=frontend-build /app/frontend/node_modules /app/frontend/node_modules/

# Create startup script
COPY docker-entrypoint.sh /app/
RUN chmod +x /app/docker-entrypoint.sh

# Expose ports
EXPOSE 5000 3000

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000

# Start both applications
ENTRYPOINT ["/app/docker-entrypoint.sh"]