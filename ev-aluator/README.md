# EV Range Analyzer

A web application that analyzes Google location history data to help users determine electric vehicle compatibility based on their historical driving patterns.

## Architecture

- **Backend**: ASP.NET Core 8.0 Web API with C#
- **Frontend**: Vue.js 3.4 with TypeScript
- **Caching**: Redis for session storage
- **Containerization**: Docker with multi-stage builds

## Quick Start with Docker

### Prerequisites

- Docker and Docker Compose installed
- At least 2GB RAM available
- Ports 3000, 5000, and 6379 available

### Development Setup

1. **Clone and navigate to the project:**
   ```bash
   cd ev-aluator
   ```

2. **Build and run with Docker Compose:**
   ```bash
   docker-compose up --build
   ```

3. **Access the applications:**
   - Frontend UI: http://localhost:3000
   - Backend API: http://localhost:5000
   - Health Check: http://localhost:5000/health

### Production Setup

For production deployment:

```bash
# Build the production image
docker build -t ev-range-analyzer .

# Run with environment variables
docker run -d \
  -p 3000:3000 \
  -p 5000:5000 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__Redis=your-redis-connection \
  --name ev-analyzer \
  ev-range-analyzer
```

### Development with Redis Commander (Optional)

To access Redis data during development:

```bash
docker-compose --profile debug up
```

Then access Redis Commander at http://localhost:8081 (admin/admin)

## Local Development

### Backend (.NET 8.0)

```bash
cd backend
dotnet restore
dotnet build
dotnet run
```

### Frontend (Vue.js 3.4)

```bash
cd frontend
npm install
npm run dev
```

## Configuration

### Environment Variables

- `ASPNETCORE_ENVIRONMENT`: Set to `Development` or `Production`
- `ASPNETCORE_URLS`: Backend URLs (default: `http://+:5000`)
- `ConnectionStrings__Redis`: Redis connection string

### Google OAuth Setup

1. Create a Google OAuth 2.0 application in Google Cloud Console
2. Configure authorized redirect URIs:
   - Development: `http://localhost:5000/auth/callback`
   - Production: `https://your-domain.com/auth/callback`
3. Add client credentials to `appsettings.json`:
   ```json
   {
     "Authentication": {
       "Google": {
         "ClientId": "your-client-id",
         "ClientSecret": "your-client-secret"
       }
     }
   }
   ```

## API Endpoints

### Authentication
- `GET /auth/login` - Initiate Google OAuth login
- `GET /auth/callback` - OAuth callback handler
- `GET /auth/user` - Get current user info
- `POST /auth/logout` - End user session

### File Upload
- `POST /upload/location-history` - Upload Google Takeout JSON
- `GET /upload/status` - Check processing status
- `GET /data/summary` - Get data overview
- `DELETE /data` - Delete user data

### Analysis
- `POST /analysis/ev-compatibility` - Analyze EV range compatibility
- `GET /analysis/daily-distances` - Get daily distance breakdown
- `GET /analysis/statistics` - Get driving statistics
- `GET /analysis/ev-recommendations` - Get EV range recommendations

### Health
- `GET /health` - Application health check

## Testing

### Backend Tests
```bash
cd backend
dotnet test
```

### Frontend Tests
```bash
cd frontend
npm run test:unit      # Unit tests with Vitest
npm run test:e2e       # E2E tests with Playwright
```

### Linting and Formatting
```bash
# Backend
cd backend
dotnet build  # StyleCop checks included

# Frontend
cd frontend
npm run lint    # ESLint
npm run format  # Prettier
```

## Performance Requirements

- API response times: < 200ms
- File processing: < 30s for 100MB files
- Page load times: < 2s on 3G connections
- Memory usage: < 512MB per session

## Security Features

- Google OAuth 2.0 with PKCE
- Session-only data storage (no persistent user data)
- Input validation and sanitization
- HTTPS enforcement in production
- Secure session cookies (HttpOnly, Secure, SameSite)

## Troubleshooting

### Common Issues

1. **Redis connection failed**
   - Ensure Redis is running: `docker-compose up redis`
   - Check connection string in configuration

2. **Google OAuth errors**
   - Verify client credentials in `appsettings.json`
   - Check authorized redirect URIs in Google Console

3. **File upload fails**
   - Check file size (max 100MB)
   - Ensure file is valid Google Takeout JSON format

4. **Frontend build errors**
   - Run `npm install` to ensure dependencies are installed
   - Check Node.js version compatibility (20+)

### Logs

- Backend logs: Console output or configured logging provider
- Frontend logs: Browser developer console
- Docker logs: `docker-compose logs -f ev-range-analyzer`

## Contributing

1. Follow the existing code style (StyleCop for C#, ESLint/Prettier for TypeScript)
2. Write tests for new features
3. Update API documentation for endpoint changes
4. Test Docker builds before submitting PRs

## License

[Add your license information here]