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
		"FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
		"JWT_SECRET_KEY": "your-local-dev-secret-key-at-least-32-characters-long-for-security",
		"DEFAULT_MASTER_PASSWORD": "your-local-master-password"
	},
	"ConnectionStrings": {
		"DefaultConnection": "Host=localhost;Port=5432;Database=postgres;Username=your_username;Password=your_password"
	}
}
```

**Important:** Never commit `local.settings.json` to version control!

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

- Production frontend: `https://agreeable-hill-0630dd403.3.azurestaticapps.net`
- Local development: `http://localhost:5173`

Configuration is in `host.json`:

```json
{
	"extensions": {
		"http": {
			"cors": {
				"allowedOrigins": [
					"https://agreeable-hill-0630dd403.3.azurestaticapps.net",
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
    "email": "admin@example.com",
    "password": "YourSecurePassword",
    "name": "Admin User",
    "masterPassword": "your-local-master-password"
  }'
```

### 2. Register Regular User:

```bash
curl -X POST http://localhost:7071/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "student1@example.com",
    "password": "YourPassword123",
    "name": "Student One",
    "registrationCode": "YourRegistrationCode"
  }'
```

### 3. Login to get JWT token:

```bash
curl -X POST http://localhost:7071/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "student1@example.com",
    "password": "YourPassword123"
  }'
```

Save the `token` from the response and use it in subsequent requests:

```bash
curl -X GET http://localhost:7071/api/profiles/1/projects \
  -H "Authorization: Bearer YOUR_JWT_TOKEN_HERE"
```

## Key Features

### Authentication & Authorization
- **Email-based authentication** with JWT tokens
- **BCrypt password hashing** for secure storage
- **Role-based access control** (User, Admin)
- **Registration code system** to control user registration
- **Master password** for admin creation

### Project Management
- Create and manage sustainability projects
- **Collaborative projects** with email-based invitations
- **Ownership transfer** when removing project owner
- Automatic project deletion if no collaborators remain

### Impact Assessment
- Create impacts with **1-5 scoring system**
- Title and description for each impact
- Categorize by **sustainability dimensions** (Environmental, Social, Economic)
- Link impacts to **UN SDGs** (Sustainable Development Goals)
- Track **relation types** (Direct, Indirect, Hidden)

### Analytics
- **Project analysis endpoint** (`GET /projects/{id}/analysis`)
- Score distribution and sentiment analysis
- SDG coverage metrics
- Impact and dimension breakdowns

## API Endpoints

### Authentication
- `POST /api/auth/register` - Register new user (requires registration code)
- `POST /api/auth/login` - Login and receive JWT token
- `PUT /api/users/email` - Update user email

### Admin
- `POST /api/users/admin/create` - Create admin user (requires master password)
- `GET /api/users/admin/all` - Get all users (admin only)
- `DELETE /api/users/admin/{userId}` - Delete user (admin only)
- `GET /api/management/registration-code` - Get current registration code (admin only)
- `POST /api/management/registration-code` - Set registration code (admin only)
- `GET /api/management/master-password` - Get master password (admin only)
- `POST /api/management/master-password` - Set master password (admin only)

### Projects
- `GET /api/projects` - Get all projects
- `GET /api/projects/{id}` - Get project by ID
- `POST /api/projects` - Create project
- `PUT /api/projects/{id}` - Update project
- `DELETE /api/projects/{id}` - Delete project
- `GET /api/profiles/{profileId}/projects` - Get user's projects
- `GET /api/profiles/{profileId}/projects-full` - Get projects with collaborators
- `GET /api/projects/{projectId}/analysis` - Get project analytics

### Collaborators
- `GET /api/projects/{projectId}/collaborators` - Get all collaborators (includes owner)
- `POST /api/projects/{projectId}/collaborators` - Add collaborator by email
- `DELETE /api/projects/{projectId}/collaborators/{profileId}` - Remove collaborator

### Impacts
- `GET /api/projects/{projectId}/impacts` - Get all impacts for project
- `POST /api/impacts` - Create impact
- `PUT /api/impacts/{id}` - Update impact
- `DELETE /api/impacts/{id}` - Delete impact

### Profiles
- `GET /api/profiles/{id}` - Get profile by ID
- `PUT /api/profiles/{id}` - Update profile
- `DELETE /api/profiles/{id}` - Delete profile

### SDGs
- `GET /api/sdgs` - Get all SDGs

## API Security

- All endpoints except registration/login require JWT authentication
- Admin endpoints require admin role in JWT token
- Registration requires valid registration code (controlled by admin)
- Admin creation requires master password
- **Environment variables required**: `JWT_SECRET_KEY` (min 32 chars), `DEFAULT_MASTER_PASSWORD`
- CORS protection prevents unauthorized browser access
- Passwords hashed with BCrypt
- No hardcoded secrets in source code

## Deployment

See [DEPLOYMENT.md](DEPLOYMENT.md) for detailed Azure deployment instructions.
