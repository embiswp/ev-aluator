# Research: Electric Vehicle Range Analyzer

## 1. Google Takeout Location History Processing

**Decision**: Use dual processing strategy for both Records.json and Semantic Location History formats  
**Rationale**: Google provides both raw GPS coordinates (Records.json) and processed timeline data with activity detection. Dual processing provides better accuracy and transport mode confidence.  
**Alternatives considered**: Raw coordinates only (lower accuracy), Semantic only (limited availability), Third-party geocoding services (privacy concerns, cost)

**Technical Implementation**:
- System.Text.Json with streaming deserialization for memory efficiency
- Process Records.json for raw GPS data with timestamp filtering
- Use Semantic Location History for activity confidence scores
- Fallback algorithms for missing transport mode data

## 2. Large File Processing (100MB)

**Decision**: Streaming JSON deserialization with System.Text.Json  
**Rationale**: Constant memory usage regardless of file size, better performance than Newtonsoft.Json for large files, built-in async support  
**Alternatives considered**: Memory mapping (complex error handling), Newtonsoft.Json (higher memory usage), Custom binary format (user friction)

**Technical Implementation**:
```csharp
// Streaming deserialization pattern
await foreach (var locationPoint in JsonSerializer.DeserializeAsyncEnumerable<LocationPoint>(stream))
{
    // Process each point without loading entire file
}
```

## 3. Transport Mode Identification

**Decision**: Leverage Google's activity recognition with confidence thresholds (75%+)  
**Rationale**: Google's ML models are pre-trained on billions of location points, higher accuracy than custom algorithms  
**Alternatives considered**: Speed-based detection (false positives), Custom ML model (training data requirements), Third-party APIs (latency, cost)

**Technical Implementation**:
- Motorized modes: "IN_VEHICLE", "IN_BUS", "ON_MOTORCYCLE"  
- Excluded modes: "WALKING", "RUNNING", "ON_BICYCLE", "IN_TRAIN", "IN_FLIGHT"
- Confidence threshold: 75% minimum for classification
- Speed analysis fallback for missing confidence data (>15 km/h for potential vehicle travel)

## 4. Distance Calculation Algorithms

**Decision**: Haversine formula with optimized trigonometric calculations  
**Rationale**: 0.5% accuracy for distances, excellent performance, well-tested implementation  
**Alternatives considered**: Vincenty formula (overkill accuracy), Great circle distance (similar performance), Third-party geocoding (API dependency)

**Technical Implementation**:
```csharp
public static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
{
    const double R = 6371; // Earth radius in km
    var dLat = ToRadians(lat2 - lat1);
    var dLon = ToRadians(lon2 - lon1);
    var a = Math.Sin(dLat/2) * Math.Sin(dLat/2) + 
            Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * 
            Math.Sin(dLon/2) * Math.Sin(dLon/2);
    return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
}
```

## 5. Session-Based Temporary Storage

**Decision**: Redis distributed cache with configurable TTL  
**Rationale**: Production-ready scaling, automatic cleanup, supports concurrent sessions, meets constitutional security requirements  
**Alternatives considered**: In-memory cache (single instance limitation), SQL Server temp tables (persistence overhead), File system (security concerns)

**Technical Implementation**:
- Redis with 30-minute active session TTL, 2-hour maximum TTL
- Session key: `ev-analysis:{userId}:{sessionId}`
- Automatic cleanup on logout or expiration
- Memory pool optimization for location point objects

## 6. Google OAuth Integration

**Decision**: Backend-for-Frontend (BFF) pattern with PKCE flow  
**Rationale**: 2025 OAuth security best practices, no token exposure to frontend JavaScript, HTTP-only secure cookies  
**Alternatives considered**: Implicit flow (deprecated), Authorization code without PKCE (less secure), Third-party auth providers (Google requirement)

**Technical Implementation**:
- PKCE challenge/verifier generation in backend
- Google OAuth 2.0 authorization endpoint with appropriate scopes
- JWT token validation and user profile extraction
- Secure session cookie with HttpOnly, Secure, SameSite flags

## 7. File Upload Handling

**Decision**: Streaming upload with MultipartReader and progress tracking  
**Rationale**: Handles 100MB files without memory pressure, real-time progress feedback, security validation at multiple layers  
**Alternatives considered**: Base64 upload (33% size overhead), Direct file system access (security risk), Client-side chunking (complexity)

**Technical Implementation**:
- ASP.NET Core MultipartReader for streaming
- SignalR for real-time upload progress
- Multi-layer validation: file extension, MIME type, JSON structure
- Virus scanning integration hook for enterprise deployments

## 8. Performance Optimization Strategies

**Decision**: Multi-level caching with spatial indexing  
**Rationale**: Meets constitutional <200ms API response requirement, scalable architecture, efficient memory usage  
**Alternatives considered**: Database caching (persistence overhead), Simple memory cache (limited scalability), No caching (performance impact)

**Technical Implementation**:
- L1 cache: In-memory for frequently accessed calculations
- L2 cache: Redis for session data and computed results
- R-Tree spatial indexing for location-based queries
- Parallel processing with Task.Parallel for CPU-intensive operations
- OpenTelemetry metrics for performance monitoring

## Constitutional Compliance Summary

✅ **Code Quality Standards**: Strong typing in C#, ESLint for TypeScript, comprehensive unit tests  
✅ **Test-First Development**: TDD with xUnit backend, Vitest frontend, Playwright E2E  
✅ **Performance Requirements**: <200ms API through caching and streaming, <2s page loads  
✅ **Security Standards**: OAuth PKCE, input validation, secure session management, no persistent storage  

All technical decisions align with constitutional principles and provide a solid foundation for implementation.