# Data Model: Electric Vehicle Range Analyzer

## Core Entities

### UserSession
**Purpose**: Manages authenticated user state and temporary data storage  
**Lifecycle**: Created on Google OAuth login, destroyed on logout or timeout

**Fields**:
- `SessionId: string` - Unique session identifier (GUID)
- `UserId: string` - Google user ID from OAuth token
- `UserEmail: string` - User email from Google profile
- `CreatedAt: DateTime` - Session creation timestamp
- `LastAccessedAt: DateTime` - Last activity timestamp (for TTL)
- `ExpiresAt: DateTime` - Absolute expiration time
- `IsActive: bool` - Session validity flag

**Validation Rules**:
- SessionId must be unique and non-empty
- UserId must match Google OAuth user ID format
- UserEmail must be valid email format
- Session lifetime maximum 2 hours, idle timeout 30 minutes

**State Transitions**:
- Created → Active (on successful OAuth)
- Active → Expired (on timeout or manual logout)
- Active → Processing (during file upload/analysis)

### LocationHistoryData
**Purpose**: Parsed Google Takeout JSON containing raw location points  
**Lifecycle**: Created during file upload, destroyed on session end

**Fields**:
- `SessionId: string` - Foreign key to UserSession
- `OriginalFileName: string` - Uploaded file name
- `FileSizeMB: decimal` - File size for processing metrics
- `TotalLocationPoints: int` - Total GPS coordinates in dataset
- `ProcessedLocationPoints: int` - Successfully parsed coordinates
- `DateRangeStart: DateTime` - Earliest timestamp in data
- `DateRangeEnd: DateTime` - Latest timestamp in data
- `ProcessingStatus: ProcessingStatus` - Upload/parsing status
- `UploadedAt: DateTime` - File upload timestamp

**Validation Rules**:
- File size must be ≤ 100MB
- OriginalFileName must have .json extension
- DateRange must be logically consistent (start ≤ end)
- ProcessedLocationPoints ≤ TotalLocationPoints

**Relationships**:
- Belongs to one UserSession (1:1)
- Contains multiple LocationPoint records (1:N)

### LocationPoint
**Purpose**: Individual GPS coordinate with timestamp and activity data  
**Lifecycle**: Created during JSON parsing, used for trip calculations

**Fields**:
- `Id: long` - Auto-increment primary key
- `SessionId: string` - Foreign key to UserSession
- `Timestamp: DateTime` - GPS recording time (UTC)
- `Latitude: double` - GPS latitude (-90 to +90)
- `Longitude: double` - GPS longitude (-180 to +180)
- `Accuracy: int?` - GPS accuracy in meters (nullable)
- `ActivityType: TransportMode` - Detected transport mode
- `ActivityConfidence: int?` - Google's confidence score (0-100)
- `Velocity: double?` - Speed in km/h (calculated or provided)

**Validation Rules**:
- Latitude must be between -90 and +90
- Longitude must be between -180 and +180
- ActivityConfidence must be between 0 and 100 if provided
- Velocity must be ≥ 0 if provided
- Timestamp must be valid UTC datetime

**Relationships**:
- Belongs to one UserSession (N:1)
- Used to calculate DailyTripSummary (N:N aggregation)

### DailyTripSummary
**Purpose**: Aggregated daily driving distances for analysis  
**Lifecycle**: Calculated from LocationPoint data, cached for performance

**Fields**:
- `SessionId: string` - Foreign key to UserSession
- `Date: DateOnly` - Analysis date (local timezone)
- `TotalDistanceKm: decimal` - Total motorized vehicle distance
- `MotorizedTrips: int` - Number of separate vehicle trips
- `LongestTripKm: decimal` - Distance of longest single trip
- `AverageSpeedKmh: decimal` - Average speed across all trips
- `TransportModes: TransportMode[]` - Detected modes for this day
- `CalculatedAt: DateTime` - Computation timestamp

**Validation Rules**:
- Date must be within LocationHistoryData date range
- All distance values must be ≥ 0
- AverageSpeedKmh must be ≥ 0 and ≤ 200 (reasonable driving speeds)
- MotorizedTrips must be ≥ 0

**Relationships**:
- Belongs to one UserSession (N:1)
- Aggregated from multiple LocationPoint records
- Used by EVRangeAnalysis for compatibility calculation

### EVRangeAnalysis
**Purpose**: Analysis results showing EV compatibility with historical data  
**Lifecycle**: Generated on-demand from user range input and DailyTripSummary

**Fields**:
- `SessionId: string` - Foreign key to UserSession
- `EVRangeKm: int` - User-specified EV range
- `AnalysisDate: DateTime` - When analysis was performed
- `TotalDaysAnalyzed: int` - Total driving days in dataset
- `CompatibleDays: int` - Days within EV range
- `IncompatibleDays: int` - Days exceeding EV range
- `CompatibilityPercentage: decimal` - (Compatible / Total) * 100
- `AverageDailyDistance: decimal` - Mean daily distance across all days
- `MaximumDailyDistance: decimal` - Highest single-day distance
- `RecommendedMinimumRange: int` - EV range for 95% compatibility

**Validation Rules**:
- EVRangeKm must be > 0 and ≤ 1000 (reasonable EV ranges)
- CompatibleDays + IncompatibleDays = TotalDaysAnalyzed
- CompatibilityPercentage must be between 0 and 100
- All distance values must be ≥ 0

**Relationships**:
- Belongs to one UserSession (N:1)
- Calculated from DailyTripSummary data
- Multiple analyses possible per session (different EV ranges)

### TransportModeClassification
**Purpose**: Categorization system for activity types from Google data  
**Lifecycle**: Static configuration data, referenced during processing

**Fields**:
- `GoogleActivityType: string` - Google's activity identifier
- `Category: TransportCategory` - Motorized vs Non-motorized
- `DisplayName: string` - Human-readable name
- `IsIncludedInAnalysis: bool` - Whether to include in distance calculations
- `TypicalSpeedRangeKmh: (int min, int max)` - Expected speed range

**Validation Rules**:
- GoogleActivityType must match Google's standard activity names
- TypicalSpeedRangeKmh min must be ≤ max
- DisplayName must be non-empty

**Relationships**:
- Referenced by LocationPoint.ActivityType
- Used for filtering motorized vs non-motorized activities

## Enums

### ProcessingStatus
- `Uploading` - File transfer in progress
- `Parsing` - JSON deserialization active
- `Processing` - Location point analysis
- `Completed` - Ready for analysis
- `Failed` - Error occurred during processing

### TransportMode
- `InVehicle` - Car/truck (included in analysis)
- `InBus` - Public transit bus (included in analysis)  
- `OnMotorcycle` - Motorcycle/scooter (included in analysis)
- `Walking` - On foot (excluded from analysis)
- `Running` - Jogging/running (excluded from analysis)
- `OnBicycle` - Cycling (excluded from analysis)
- `InTrain` - Rail transport (excluded from analysis)
- `InFlight` - Air travel (excluded from analysis)
- `Unknown` - Unclassified activity (excluded from analysis)

### TransportCategory
- `Motorized` - Vehicle-based transport (included in EV analysis)
- `NonMotorized` - Human-powered or rail/air (excluded from analysis)

## Relationships Summary

```
UserSession (1) ←→ (1) LocationHistoryData
UserSession (1) ←→ (N) LocationPoint
UserSession (1) ←→ (N) DailyTripSummary  
UserSession (1) ←→ (N) EVRangeAnalysis

LocationPoint (N) → (aggregated to) → DailyTripSummary (1 per date)
DailyTripSummary (N) → (analyzed by) → EVRangeAnalysis (1 per EV range)
```

## Storage Patterns

### Session Storage (Redis)
- Key pattern: `ev-analysis:{userId}:{sessionId}`
- TTL: 30 minutes idle, 2 hours absolute maximum
- Automatic cleanup on logout or expiration

### Temporary Processing (Memory)
- LocationPoint objects streamed during parsing
- No persistent database storage required
- Garbage collection optimized with object pools