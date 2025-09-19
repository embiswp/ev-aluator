# EvAluator Setup Guide

## Prerequisites

- .NET 9.0 SDK
- Node.js 18+ and npm
- SQL Server LocalDB (or SQL Server)
- Google Cloud Console account

## Configuration Setup

### 1. Backend Configuration

Copy the example configuration file and add your secrets:

```bash
cp src/EvAluator.Api/appsettings.local.example.json src/EvAluator.Api/appsettings.local.json
```

Edit `src/EvAluator.Api/appsettings.local.json` with your actual values:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=EvAluator;Trusted_Connection=true;MultipleActiveResultSets=true"
  },
  "GoogleAuth": {
    "ClientId": "your-actual-google-client-id.googleusercontent.com",
    "ClientSecret": "your-actual-google-client-secret"
  },
  "Jwt": {
    "SecretKey": "generate-a-secure-jwt-secret-key-must-be-at-least-32-characters-long-for-security"
  }
}
```

### 2. Frontend Configuration

Copy the example environment file:

```bash
cp frontend/.env.example frontend/.env.local
```

Edit `frontend/.env.local`:

```env
VITE_GOOGLE_CLIENT_ID=your-actual-google-client-id.googleusercontent.com
VITE_API_BASE_URL=http://localhost:5000
```

## Google Cloud Console Setup

### 1. Create Google OAuth 2.0 Credentials

1. Go to [Google Cloud Console](https://console.cloud.google.com)
2. Create a new project or select an existing one
3. Enable the **Google+ API** (or Google Identity API)
4. Go to **APIs & Services** → **Credentials**
5. Click **Create Credentials** → **OAuth 2.0 Client ID**
6. Choose **Web application**
7. Add **Authorized JavaScript origins**:
   - `http://localhost:3000`
   - `https://localhost:3000`
   - Your production domain (when ready)
8. Add **Authorized redirect URIs**:
   - `http://localhost:3000`
   - Your production domain (when ready)
9. Copy the **Client ID** and **Client Secret**

### 2. JWT Secret Key Generation

Generate a secure JWT secret key (at least 32 characters):

```bash
# Using Node.js
node -e "console.log(require('crypto').randomBytes(32).toString('hex'))"

# Or using PowerShell
[System.Web.Security.Membership]::GeneratePassword(64, 0)
```

## Database Setup

1. Ensure SQL Server LocalDB is installed and running
2. The application will automatically create the database on first run
3. Run database migrations (if available):

```bash
cd src/EvAluator.Api
dotnet ef database update
```

## Running the Application

### Backend

```bash
cd src/EvAluator.Api
dotnet restore
dotnet run
```

The API will be available at `http://localhost:5000`

### Frontend

```bash
cd frontend
npm install
npm run dev
```

The frontend will be available at `http://localhost:3000`

## Troubleshooting

### Common Issues

1. **Google OAuth not working**: 
   - Ensure the Client ID in both backend and frontend configs match
   - Verify authorized origins in Google Cloud Console
   - Check browser developer tools for CORS errors

2. **JWT errors**:
   - Ensure the JWT secret key is at least 32 characters long
   - Verify the secret key is the same in all environments

3. **Database connection issues**:
   - Ensure SQL Server LocalDB is running
   - Check the connection string format
   - Verify database permissions

4. **CORS errors**:
   - Ensure frontend URL is in the CORS policy in Program.cs
   - Check that API base URL in frontend config matches backend

## Security Notes

- Never commit `appsettings.local.json` or `.env.local` files
- Use different JWT secret keys for different environments
- In production, use environment variables or Azure Key Vault for secrets
- Enable HTTPS in production
- Regularly rotate JWT secret keys and Google OAuth credentials

## File Structure

```
├── src/EvAluator.Api/
│   ├── appsettings.json              # Example config (committed)
│   ├── appsettings.local.json        # Local secrets (ignored)
│   └── appsettings.local.example.json # Example local config
├── frontend/
│   ├── .env                          # Example environment (committed)
│   ├── .env.local                    # Local secrets (ignored)
│   └── .env.example                  # Example environment
└── .gitignore                        # Excludes secret files
```