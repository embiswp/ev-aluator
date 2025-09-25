# Claude Code Context - EV-aluator

## Project Overview
Electric Vehicle Range Analyzer - A web application that analyzes Google location history data to help users determine EV compatibility based on historical driving patterns.

## Current Development Focus
**Feature**: 001-electric-vehicle-range  
**Phase**: Implementation planning complete  
**Next**: Task generation and implementation

## Technical Stack
**Languages**: C# (.NET 8.0), Vue.js 3.4, TypeScript  
**Backend**: ASP.NET Core, Entity Framework Core, Google OAuth  
**Frontend**: Vue Router, Axios, responsive design  
**Storage**: In-memory temporary storage during user session, no persistent database  
**Testing**: xUnit (backend), Vitest (frontend), Playwright (E2E)  
**Deployment**: Docker containerized web application, Linux deployment

## Architecture
**Project Type**: Web application (frontend + backend)  
**Structure**: 
```
backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── components/
│   ├── pages/
│   └── services/
└── tests/
```

## Key Requirements
- **Performance**: API responses <200ms, page loads <2s, file processing <30s for 100MB
- **Security**: Google OAuth, session-only data storage, input validation, no persistent data
- **Scale**: Single-user analysis sessions, 10-100MB JSON file processing
- **Accessibility**: WCAG 2.1 AA compliance, responsive design

## Constitutional Principles
✅ **Code Quality Standards**: Strong typing (C#/TypeScript), linting, comprehensive testing  
✅ **Test-First Development**: TDD with xUnit, Vitest, Playwright  
✅ **User Experience**: Vue.js components, responsive design, accessibility  
✅ **Performance Requirements**: <200ms APIs, <2s page loads, optimized processing  
✅ **Security Standards**: OAuth, input validation, secure sessions

## Current Feature Status
- [x] Specification complete (spec.md)
- [x] Research complete (research.md) 
- [x] Data model defined (data-model.md)
- [x] API contracts defined (contracts/*.yaml)
- [x] Integration tests planned (quickstart.md)
- [ ] Tasks generated (next: /tasks command)
- [ ] Implementation
- [ ] Testing & validation

## Key Entities
- **UserSession**: OAuth-authenticated user with temporary data storage
- **LocationHistoryData**: Parsed Google Takeout JSON files
- **LocationPoint**: Individual GPS coordinates with transport mode detection
- **DailyTripSummary**: Aggregated daily driving distances (motorized only)
- **EVRangeAnalysis**: Compatibility results for specified EV ranges

## API Endpoints
**Authentication**: `/auth/login`, `/auth/callback`, `/auth/user`, `/auth/logout`  
**File Upload**: `/upload/location-history`, `/upload/status`, `/data/summary`, `/data` (DELETE)  
**Analysis**: `/analysis/ev-compatibility`, `/analysis/daily-distances`, `/analysis/statistics`, `/analysis/ev-recommendations`

## Recent Changes
- 2025-09-25: Implementation plan completed with full technical research
- 2025-09-25: Data model and API contracts defined
- 2025-09-25: Quickstart integration test scenarios created
- Next: Generate detailed task breakdown for implementation

---
*Auto-updated by .specify workflow system*