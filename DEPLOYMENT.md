# Azure Deployment Guide

## Required Environment Variables

Before deploying to Azure Functions, configure these in **Azure Portal > Your Function App > Configuration > Application Settings**:

### Required Settings

```
JWT_SECRET_KEY = <generate-a-secure-random-string-at-least-32-characters>
DEFAULT_MASTER_PASSWORD = <your-secure-master-password-for-admin-creation>
```

**Optional Settings** (defaults provided):
```
JWT_ISSUER = sustainability-canvas-api
JWT_AUDIENCE = sustainability-canvas-client
JWT_EXPIRATION_HOURS = 2
```

### Connection String

Go to **Azure Portal > Your Function App > Configuration > Connection Strings** and add:

```
Name: DefaultConnection
Value: <your-azure-postgres-connection-string>
Type: Custom
```

Example PostgreSQL connection string:
```
Host=your-server.postgres.database.azure.com;Port=5432;Database=sustainability_canvas;Username=your_admin;Password=your_password;SSL Mode=Require;
```

## Generating Secure Secrets

### JWT Secret Key (32+ characters)
```bash
# Linux/Mac
openssl rand -base64 48

# PowerShell
[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))
```

### Master Password
Use a strong password manager to generate a secure password (16+ characters recommended).

## Post-Deployment Steps

1. **Test the deployment:**
   ```bash
   curl https://your-function-app.azurewebsites.net/api/health
   ```

2. **Create the first admin user:**
   ```bash
   curl -X POST https://your-function-app.azurewebsites.net/api/users/admin/create \
     -H "Content-Type: application/json" \
     -d '{
       "email": "admin@yourdomain.com",
       "password": "YourSecurePassword123!",
       "name": "Admin User",
       "masterPassword": "YOUR_DEFAULT_MASTER_PASSWORD"
     }'
   ```

3. **Update CORS settings in host.json** if needed to match your frontend URL.

## Security Checklist

- ✅ No secrets in source code
- ✅ All sensitive values use environment variables
- ✅ Application throws errors if required secrets are missing
- ✅ JWT secret is at least 32 characters
- ✅ Database credentials are in Connection Strings (not Application Settings)
- ✅ CORS is configured with specific origins
- ✅ .gitignore excludes local.settings.json and appsettings.Development.json

## Local Development

Copy `local.settings.json.template` to `local.settings.json` and fill in your values:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "JWT_SECRET_KEY": "your-local-dev-secret-key-at-least-32-characters-long",
    "DEFAULT_MASTER_PASSWORD": "your-local-master-password"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=your_db;Username=your_user;Password=your_password"
  }
}
```

**Never commit `local.settings.json` to version control!**
