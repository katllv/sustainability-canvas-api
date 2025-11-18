# sustainability-canvas-api

Azure Functions API for the Digital Sustainability Canvas project - a platform for students to create and manage sustainability impact assessments.

## Local Development Setup

### Prerequisites

- .NET 8.0 SDK
- PostgreSQL database running locally
- Azure Functions Core Tools

### Local Testing Steps

#### 1. Database Setup

Ensure PostgreSQL is running on your local machine:

```bash
# Check if PostgreSQL is running
sudo systemctl status postgresql
# or
pg_isready -h localhost -p 5432
```

#### 2. Configuration

Create `local.settings.json` in the project root (use `local.settings.json.template` as reference):

```json
{
	"IsEncrypted": false,
	"Values": {
		"FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
	},
	"ConnectionStrings": {
		"DefaultConnection": "Host=localhost;Port=5432;Database=postgres;Username=your_username;Password=your_password"
	}
}
```

#### 3. Database Migration

Apply migrations to create all required tables:

```bash
dotnet ef database update
```

#### 4. Start Azure Functions API

```bash
func start --port 7071
```

#### 5. Start Frontend

In your frontend project directory:

```bash
npm run dev
# or
yarn dev
```

## CORS Configuration

The API is configured to allow requests from:

- Production frontend: `https://your-production-domain.com`
- Local development: `http://localhost:5173`

Configuration is in `host.json`:

```json
{
	"extensions": {
		"http": {
			"cors": {
				"allowedOrigins": [
					"https://your-production-domain.com",
					"http://localhost:5173"
				],
				"allowCredentials": false
			}
		}
	}
}
```

## Testing the API

### 1. Create Admin User (required first step):

```bash
curl -X POST http://localhost:7071/api/users/admin/create \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "YourSecurePassword",
    "name": "Admin User",
    "email": "admin@example.com",
    "masterPassword": "YourMasterPassword"
  }'
```

### 2. Register Regular User:

```bash
curl -X POST http://localhost:7071/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "student1",
    "password": "YourPassword123",
    "name": "Student One",
    "email": "student1@example.com",
    "registrationCode": "YourRegistrationCode"
  }'
```

### 3. Login to get JWT token:

```bash
curl -X POST http://localhost:7071/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "student1",
    "password": "YourPassword123"
  }'
```

## API Security

- All endpoints except registration/login require JWT authentication
- Admin endpoints require admin role in JWT token
- Registration requires valid registration code (controlled by admin)
- Admin creation requires master password
- CORS protection prevents unauthorized browser access
