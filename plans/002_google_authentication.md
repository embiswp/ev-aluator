# 002 - Google Authentication Integration

## Overview
Extend the EvAluator application to support user authentication via Google OAuth 2.0, enabling personalized experiences, data persistence, and secure access to evaluation history.

## Architecture Approach
- **Backend**: JWT-based authentication with Google OAuth 2.0 integration
- **Frontend**: Google Sign-In SDK with automatic token refresh
- **Security**: HTTPS-only cookies, CSRF protection, secure token storage
- **Data**: User-specific evaluation storage and history management

## Google OAuth 2.0 Flow
```
1. User clicks "Sign in with Google" → Frontend redirects to Google
2. User authenticates with Google → Google returns authorization code
3. Frontend sends code to backend → Backend exchanges for access token
4. Backend validates token with Google → Creates/updates user record
5. Backend issues JWT → Frontend stores token securely
6. Subsequent requests include JWT → Backend validates and authorizes
```

## Backend Implementation

### 1. Authentication Infrastructure
```
src/EvAluator.Infrastructure/
├── Authentication/
│   ├── GoogleAuthService.cs         # Google token validation
│   ├── JwtTokenService.cs           # JWT generation/validation
│   └── UserAuthRepository.cs        # User data persistence
├── Configuration/
│   └── GoogleAuthOptions.cs         # Google OAuth settings
```

### 2. Domain Extensions
```
src/EvAluator.Domain/
├── Entities/
│   └── User.cs                      # User domain entity
├── ValueObjects/
│   ├── UserId.cs                    # Strong-typed user identifier
│   └── GoogleProfile.cs             # Google user profile data
├── Services/
│   └── IUserAuthenticationService.cs # Authentication domain service
```

### 3. Application Layer
```
src/EvAluator.Application/
├── Auth/
│   ├── Commands/
│   │   ├── GoogleSignInCommand.cs   # Handle Google sign-in
│   │   └── RefreshTokenCommand.cs   # Token refresh logic
│   ├── Queries/
│   │   └── GetUserProfileQuery.cs   # Retrieve user profile
│   └── DTOs/
│       ├── GoogleSignInRequest.cs   # Google auth request
│       ├── AuthenticationResponse.cs # Auth response with tokens
│       └── UserProfileDto.cs        # User profile data
```

### 4. API Endpoints
```
src/EvAluator.Api/
├── Controllers/
│   └── AuthController.cs            # Authentication endpoints
├── Middleware/
│   ├── JwtAuthenticationMiddleware.cs # JWT validation
│   └── UserContextMiddleware.cs     # User context injection
├── Attributes/
│   └── RequireAuthAttribute.cs      # Authorization requirement
```

### 5. Required NuGet Packages
```xml
<!-- Google Authentication -->
<PackageReference Include="Google.Apis.Auth" Version="1.68.0" />

<!-- JWT Handling -->
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="7.0.3" />

<!-- Security -->
<PackageReference Include="Microsoft.AspNetCore.DataProtection" Version="8.0.0" />
```

## Frontend Implementation

### 1. Authentication Services
```
frontend/src/services/
├── authService.js                   # Authentication API calls
├── googleAuth.js                    # Google SDK integration
└── tokenManager.js                  # Token storage & refresh
```

### 2. Authentication Components
```
frontend/src/components/auth/
├── GoogleSignInButton.vue           # Google sign-in component
├── UserProfile.vue                  # User profile display
├── AuthGuard.vue                    # Route protection
└── SignOutButton.vue                # Sign-out functionality
```

### 3. Authentication Composables
```
frontend/src/composables/
├── useAuth.js                       # Authentication state management
├── useGoogleAuth.js                 # Google-specific auth logic
└── useTokenRefresh.js               # Automatic token refresh
```

### 4. Route Protection
```
frontend/src/router/
└── authGuard.js                     # Route-level authentication
```

### 5. Required Dependencies
```json
{
  "dependencies": {
    "vue-google-oauth2": "^1.5.8",
    "js-cookie": "^3.0.5"
  }
}
```

## Database Schema Extensions

### User Table
```sql
CREATE TABLE Users (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    GoogleId NVARCHAR(100) NOT NULL UNIQUE,
    Email NVARCHAR(320) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    PictureUrl NVARCHAR(500),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastLoginAt DATETIME2,
    IsActive BIT NOT NULL DEFAULT 1
);

CREATE INDEX IX_Users_GoogleId ON Users(GoogleId);
CREATE INDEX IX_Users_Email ON Users(Email);
```

### Update Evaluations Table
```sql
ALTER TABLE Evaluations 
ADD UserId UNIQUEIDENTIFIER REFERENCES Users(Id);

CREATE INDEX IX_Evaluations_UserId ON Evaluations(UserId);
```

## Configuration Requirements

### Backend Configuration (appsettings.json)
```json
{
  "GoogleAuth": {
    "ClientId": "your-google-client-id",
    "ClientSecret": "your-google-client-secret"
  },
  "Jwt": {
    "SecretKey": "your-jwt-secret-key",
    "Issuer": "ev-aluator",
    "Audience": "ev-aluator-users",
    "ExpiryMinutes": 60,
    "RefreshExpiryDays": 30
  }
}
```

### Frontend Environment (.env)
```
VITE_GOOGLE_CLIENT_ID=your-google-client-id
VITE_API_BASE_URL=http://localhost:5000
```

## Implementation Steps

### Phase 1: Backend Authentication Core
1. **Setup Google OAuth configuration**
   - Add Google.Apis.Auth package
   - Configure GoogleAuthOptions
   - Add JWT authentication middleware

2. **Create domain entities and value objects**
   - User entity with Google profile integration
   - UserId and GoogleProfile value objects
   - Authentication domain service interface

3. **Implement authentication services**
   - GoogleAuthService for token validation
   - JwtTokenService for token management
   - UserAuthRepository for persistence

4. **Create application layer**
   - GoogleSignInCommand and handler
   - RefreshTokenCommand and handler
   - Authentication DTOs

5. **Add API endpoints**
   - POST /api/auth/google-signin
   - POST /api/auth/refresh-token
   - GET /api/auth/profile
   - POST /api/auth/signout

### Phase 2: Frontend Authentication Integration
1. **Install and configure Google SDK**
   - Add vue-google-oauth2 dependency
   - Configure Google Client ID
   - Initialize Google Auth in main.js

2. **Create authentication services**
   - API integration for auth endpoints
   - Token management with automatic refresh
   - Google SDK wrapper

3. **Build authentication components**
   - Google Sign-In button with error handling
   - User profile component
   - Sign-out functionality

4. **Implement authentication state**
   - useAuth composable for global state
   - Token refresh logic
   - Route protection guards

### Phase 3: Integration and Security
1. **Database migrations**
   - Create Users table
   - Add UserId to existing tables
   - Create necessary indexes

2. **Update existing features**
   - Associate evaluations with users
   - Add user context to API calls
   - Implement user-specific data filtering

3. **Security hardening**
   - HTTPS-only cookie configuration
   - CSRF protection implementation
   - Input validation and sanitization
   - Rate limiting on auth endpoints

### Phase 4: User Experience Enhancements
1. **Personalization features**
   - User dashboard with evaluation history
   - Saved vehicle configurations
   - Personal preferences storage

2. **Data management**
   - User data export functionality
   - Account deletion with data cleanup
   - Privacy settings management

## Security Considerations

### Token Security
- JWT tokens stored in HTTP-only cookies
- Secure flag enabled for HTTPS
- Short-lived access tokens (1 hour)
- Long-lived refresh tokens (30 days)
- Token rotation on refresh

### API Security
- All authenticated endpoints require valid JWT
- User context injection for authorization
- Rate limiting on authentication endpoints
- Input validation on all auth-related data
- CORS configuration for frontend domain

### Data Protection
- User data encryption at rest
- Secure transmission (HTTPS only)
- GDPR compliance for EU users
- Clear privacy policy implementation
- User consent for data collection

## Testing Strategy

### Unit Tests
```csharp
// Authentication service tests
GoogleAuthServiceTests.cs
JwtTokenServiceTests.cs
UserAuthRepositoryTests.cs

// Domain entity tests
UserTests.cs
GoogleProfileTests.cs

// Command/Query handler tests
GoogleSignInCommandHandlerTests.cs
RefreshTokenCommandHandlerTests.cs
```

### Integration Tests
```csharp
// End-to-end authentication flow
AuthControllerIntegrationTests.cs
GoogleAuthIntegrationTests.cs

// Database integration
UserRepositoryIntegrationTests.cs
```

### Frontend Tests
```javascript
// Component tests
GoogleSignInButton.test.js
UserProfile.test.js

// Service tests
authService.test.js
tokenManager.test.js

// Composable tests
useAuth.test.js
```

## Error Handling

### Backend Error Scenarios
- Invalid Google token → 401 Unauthorized
- Expired JWT token → 401 Unauthorized
- User account disabled → 403 Forbidden
- Google API unavailable → 503 Service Unavailable
- Database connection issues → 500 Internal Server Error

### Frontend Error Handling
- Google SDK initialization failure
- Network connectivity issues
- Token refresh failures
- User cancels Google authentication
- Session timeout handling

## Monitoring and Logging

### Authentication Metrics
- Successful/failed login attempts
- Token refresh frequency
- User session duration
- Authentication endpoint response times

### Security Logging
- Failed authentication attempts
- Suspicious activity detection
- Token validation failures
- User account changes

## Success Criteria
- Users can sign in with Google account
- JWT tokens are securely managed
- User-specific data is properly isolated
- Authentication state persists across browser sessions
- All security best practices are implemented
- Performance impact is minimal
- Error handling provides clear user feedback
- Tests cover all authentication scenarios

## Future Considerations
- Multi-factor authentication support
- Social login alternatives (Microsoft, Apple)
- Enterprise SSO integration
- Advanced user role management
- Audit logging for compliance
- API key authentication for third-party integrations