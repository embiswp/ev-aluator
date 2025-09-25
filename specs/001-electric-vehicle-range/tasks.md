# Tasks: Electric Vehicle Range Analyzer

**Input**: Design documents from `/specs/001-electric-vehicle-range/`
**Prerequisites**: plan.md (required), research.md, data-model.md, contracts/

## Format: `[ID] [P?] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- Include exact file paths in descriptions

## Path Conventions
**Web app**: `backend/src/`, `frontend/src/` (per plan.md structure)

## Phase 3.1: Setup
- [ ] T001 Create project structure with backend/ and frontend/ directories per implementation plan
- [ ] T002 Initialize backend C# .NET 8.0 project with ASP.NET Core dependencies in backend/src/
- [ ] T003 Initialize frontend Vue.js 3.4 project with TypeScript and dependencies in frontend/src/
- [ ] T004 [P] Configure backend linting with EditorConfig and StyleCop in backend/
- [ ] T005 [P] Configure frontend linting with ESLint and Prettier in frontend/
- [ ] T006 Configure Docker containerization with multi-stage build for both backend and frontend

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3
**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**

### Contract Tests [P] - Can run in parallel
- [ ] T007 [P] Contract test auth endpoints in backend/tests/contract/AuthApiTests.cs (login, callback, user, logout)
- [ ] T008 [P] Contract test upload endpoints in backend/tests/contract/UploadApiTests.cs (location-history, status, summary, delete)
- [ ] T009 [P] Contract test analysis endpoints in backend/tests/contract/AnalysisApiTests.cs (ev-compatibility, daily-distances, statistics, recommendations)

### Integration Tests [P] - Can run in parallel  
- [ ] T010 [P] Integration test complete user journey in backend/tests/integration/UserJourneyTests.cs (Scenario 1 from quickstart)
- [ ] T011 [P] Integration test large file processing in backend/tests/integration/LargeFileProcessingTests.cs (Scenario 2 from quickstart)
- [ ] T012 [P] Integration test error handling in backend/tests/integration/ErrorHandlingTests.cs (Scenario 3 from quickstart)
- [ ] T013 [P] Integration test mobile responsiveness in frontend/tests/e2e/ResponsivenessTests.spec.ts (Scenario 4 from quickstart)
- [ ] T014 [P] Integration test data privacy and security in backend/tests/integration/SecurityTests.cs (Scenario 5 from quickstart)

## Phase 3.3: Core Implementation (ONLY after tests are failing)

### Backend Models [P] - Can run in parallel
- [ ] T015 [P] UserSession model in backend/src/Models/UserSession.cs with validation rules and state transitions
- [ ] T016 [P] LocationHistoryData model in backend/src/Models/LocationHistoryData.cs with file size and date validation
- [ ] T017 [P] LocationPoint model in backend/src/Models/LocationPoint.cs with GPS coordinate validation
- [ ] T018 [P] DailyTripSummary model in backend/src/Models/DailyTripSummary.cs with distance aggregation logic
- [ ] T019 [P] EVRangeAnalysis model in backend/src/Models/EVRangeAnalysis.cs with compatibility calculations
- [ ] T020 [P] TransportModeClassification enum in backend/src/Models/TransportMode.cs with Google activity mapping

### Backend Services [P] - Can run in parallel initially
- [ ] T021 [P] SessionService for OAuth and session management in backend/src/Services/SessionService.cs
- [ ] T022 [P] FileProcessingService for Google Takeout JSON parsing in backend/src/Services/FileProcessingService.cs
- [ ] T023 [P] LocationAnalysisService for transport mode filtering in backend/src/Services/LocationAnalysisService.cs
- [ ] T024 [P] DistanceCalculationService with Haversine formula in backend/src/Services/DistanceCalculationService.cs
- [ ] T025 [P] EVCompatibilityService for range analysis in backend/src/Services/EVCompatibilityService.cs
- [ ] T026 CacheService for Redis session storage in backend/src/Services/CacheService.cs

### API Controllers - Sequential (shared middleware dependencies)
- [ ] T027 AuthController with Google OAuth PKCE endpoints in backend/src/Controllers/AuthController.cs
- [ ] T028 UploadController with multipart file handling in backend/src/Controllers/UploadController.cs
- [ ] T029 DataController with summary and deletion endpoints in backend/src/Controllers/DataController.cs
- [ ] T030 AnalysisController with EV compatibility calculations in backend/src/Controllers/AnalysisController.cs

### Frontend Components [P] - Can run in parallel
- [ ] T031 [P] Landing page component in frontend/src/components/LandingPage.vue with Google sign-in
- [ ] T032 [P] Dashboard component in frontend/src/components/Dashboard.vue with upload area
- [ ] T033 [P] FileUpload component in frontend/src/components/FileUpload.vue with progress tracking
- [ ] T034 [P] DataSummary component in frontend/src/components/DataSummary.vue with statistics display
- [ ] T035 [P] AnalysisForm component in frontend/src/components/AnalysisForm.vue for EV range input
- [ ] T036 [P] ResultsDisplay component in frontend/src/components/ResultsDisplay.vue with charts
- [ ] T037 [P] DailyBreakdown component in frontend/src/components/DailyBreakdown.vue with sortable table

### Frontend Pages and Routing
- [ ] T038 Vue Router configuration in frontend/src/router/index.ts with authentication guards
- [ ] T039 Main App component in frontend/src/App.vue with responsive layout
- [ ] T040 API service layer in frontend/src/services/ApiService.ts for backend communication

## Phase 3.4: Integration
- [ ] T041 Configure Google OAuth client credentials and PKCE in backend/src/Configuration/
- [ ] T042 Redis cache connection and session middleware in backend/src/Middleware/
- [ ] T043 CORS configuration for frontend-backend communication in backend/src/Program.cs  
- [ ] T044 Request/response logging middleware in backend/src/Middleware/LoggingMiddleware.cs
- [ ] T045 Error handling middleware with structured responses in backend/src/Middleware/ErrorHandlingMiddleware.cs
- [ ] T046 File upload size limits and security validation in backend/src/Middleware/
- [ ] T047 Session timeout and cleanup background service in backend/src/Services/BackgroundServices/

## Phase 3.5: Polish
- [ ] T048 [P] Backend unit tests for distance calculations in backend/tests/unit/DistanceCalculationTests.cs
- [ ] T049 [P] Backend unit tests for transport mode filtering in backend/tests/unit/TransportModeTests.cs
- [ ] T050 [P] Frontend unit tests for components in frontend/tests/unit/Components.spec.ts
- [ ] T051 [P] Performance tests for API response times <200ms in backend/tests/performance/ApiPerformanceTests.cs
- [ ] T052 [P] Performance tests for file processing <30s in backend/tests/performance/FileProcessingTests.cs
- [ ] T053 [P] Accessibility tests for WCAG 2.1 AA compliance in frontend/tests/accessibility/AccessibilityTests.spec.ts
- [ ] T054 Dockerfile optimization and production configuration
- [ ] T055 OpenTelemetry metrics and monitoring integration in backend/src/
- [ ] T056 Documentation updates in README.md with setup and deployment instructions

## Dependencies
- Setup (T001-T006) before everything
- Contract tests (T007-T009) before any implementation
- Integration tests (T010-T014) before implementation
- Models (T015-T020) before services (T021-T026)
- Services before controllers (T027-T030)
- Core backend before frontend API service (T040)
- T026 (CacheService) blocks T042 (Redis middleware)
- T027-T030 (Controllers) block T043-T047 (Middleware integration)
- Implementation before polish (T048-T056)

## Parallel Execution Examples

### Contract Tests (Phase 3.2)
```bash
# Launch T007-T009 together:
Task: "Contract test auth endpoints in backend/tests/contract/AuthApiTests.cs"
Task: "Contract test upload endpoints in backend/tests/contract/UploadApiTests.cs"  
Task: "Contract test analysis endpoints in backend/tests/contract/AnalysisApiTests.cs"
```

### Backend Models (Phase 3.3)
```bash
# Launch T015-T020 together:
Task: "UserSession model in backend/src/Models/UserSession.cs"
Task: "LocationHistoryData model in backend/src/Models/LocationHistoryData.cs"
Task: "LocationPoint model in backend/src/Models/LocationPoint.cs"
Task: "DailyTripSummary model in backend/src/Models/DailyTripSummary.cs"
Task: "EVRangeAnalysis model in backend/src/Models/EVRangeAnalysis.cs"
Task: "TransportModeClassification enum in backend/src/Models/TransportMode.cs"
```

### Frontend Components (Phase 3.3)
```bash
# Launch T031-T037 together after backend models/services are complete:
Task: "Landing page component in frontend/src/components/LandingPage.vue"
Task: "Dashboard component in frontend/src/components/Dashboard.vue"
Task: "FileUpload component in frontend/src/components/FileUpload.vue"
Task: "DataSummary component in frontend/src/components/DataSummary.vue"
Task: "AnalysisForm component in frontend/src/components/AnalysisForm.vue"
Task: "ResultsDisplay component in frontend/src/components/ResultsDisplay.vue"
Task: "DailyBreakdown component in frontend/src/components/DailyBreakdown.vue"
```

## Notes
- [P] tasks = different files, no dependencies
- Verify tests fail before implementing (TDD requirement)
- Commit after each task completion
- All file paths are absolute from repository root
- Backend and frontend can develop in parallel after models/services are ready

## Validation Checklist
- [x] All contracts have corresponding tests (T007-T009)
- [x] All entities have model tasks (T015-T020)  
- [x] All tests come before implementation (Phase 3.2 before 3.3)
- [x] Parallel tasks truly independent (different files)
- [x] Each task specifies exact file path
- [x] No task modifies same file as another [P] task
- [x] Integration test scenarios from quickstart.md covered (T010-T014)
- [x] All 12 API endpoints have implementation tasks (T027-T030)
- [x] Constitutional requirements addressed (performance tests T051-T052, accessibility T053)