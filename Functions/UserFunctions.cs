using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SustainabilityCanvas.Api.Data;
using SustainabilityCanvas.Api.Models;
using SustainabilityCanvas.Api.Services;
using SustainabilityCanvas.Api.Attributes;
using System.Net;
using System.Text.Json;
using BCrypt.Net;

namespace SustainabilityCanvas.Api.Functions;

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RegistrationCode { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class CreateAdminRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MasterPassword { get; set; } = string.Empty;
}

public class SetRegistrationCodeRequest
{
    public string Code { get; set; } = string.Empty;
}

public class SetMasterPasswordRequest
{
    public string NewMasterPassword { get; set; } = string.Empty;
}

public class UserFunctions
{
    private readonly SustainabilityCanvasContext _context;
    private readonly ILogger<UserFunctions> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly JwtService _jwtService;
    private readonly RegistrationCodeService _registrationCodeService;
    private readonly MasterPasswordService _masterPasswordService;

    public UserFunctions(SustainabilityCanvasContext context, ILogger<UserFunctions> logger, JsonSerializerOptions jsonOptions, JwtService jwtService, RegistrationCodeService registrationCodeService, MasterPasswordService masterPasswordService)
    {
        _context = context;
        _logger = logger;
        _jsonOptions = jsonOptions;
        _jwtService = jwtService;
        _registrationCodeService = registrationCodeService;
        _masterPasswordService = masterPasswordService;
    }

    [Function("Register")]
    public async Task<HttpResponseData> Register([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/register")] HttpRequestData req)
    {
        _logger.LogInformation("Registering new user");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Request body is empty");
                return badRequest;
            }

            var registerRequest = JsonSerializer.Deserialize<RegisterRequest>(requestBody, _jsonOptions);
            
            if (registerRequest == null || string.IsNullOrEmpty(registerRequest.Email) || string.IsNullOrEmpty(registerRequest.Password) || string.IsNullOrEmpty(registerRequest.RegistrationCode))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Email, password, and registration code are required");
                return badRequest;
            }

            // Validate registration code
            if (!await _registrationCodeService.IsValidCodeAsync(registerRequest.RegistrationCode))
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteStringAsync("Invalid registration code");
                return unauthorized;
            }

            // Check if email already exists
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == registerRequest.Email);
            if (existingUser != null)
            {
                var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                await conflict.WriteStringAsync("Email already exists");
                return conflict;
            }

            // Create user with hashed password
            var user = new User
            {
                Email = registerRequest.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerRequest.Password),
                Role = UserRole.User
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Create associated profile
            var profile = new Profile
            {
                UserId = user.Id,
                Name = registerRequest.Name
            };

            _context.Profiles.Add(profile);
            await _context.SaveChangesAsync();

            // Generate JWT token
            var token = _jwtService.GenerateToken(user.Id, user.Email, user.Role.ToString());

            var response = req.CreateResponse(HttpStatusCode.Created);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            var responseData = new
            {
                user.Id,
                user.Email,
                user.Role,
                Token = token,
                Profile = new
                {
                    profile.Id,
                    profile.Name
                }
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(responseData, _jsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Registration failed");
            return errorResponse;
        }
    }

    [Function("Login")]
    public async Task<HttpResponseData> Login([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/login")] HttpRequestData req)
    {
        _logger.LogInformation("User login attempt");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Request body is empty");
                return badRequest;
            }

            var loginRequest = JsonSerializer.Deserialize<LoginRequest>(requestBody, _jsonOptions);
            
            if (loginRequest == null || string.IsNullOrEmpty(loginRequest.Email) || string.IsNullOrEmpty(loginRequest.Password))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Email and password are required");
                return badRequest;
            }

            // Find user with profile
            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Email == loginRequest.Email);

            if (user == null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteStringAsync("Invalid email or password");
                return unauthorized;
            }

            // Verify password
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.PasswordHash);
            
            if (!isPasswordValid)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteStringAsync("Invalid email or password");
                return unauthorized;
            }

            // Generate JWT token
            var token = _jwtService.GenerateToken(user.Id, user.Email, user.Role.ToString());
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            var responseData = new
            {
                message = "Login successful",
                token = token,
                user = new
                {
                    user.Id,
                    user.Email,
                    user.Role,
                    Profile = user.Profile != null ? new
                    {
                        user.Profile.Id,
                        user.Profile.Name
                    } : null
                }
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(responseData, _jsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Login failed");
            return errorResponse;
        }
    }

    [Function("UpdateUserEmail")]
    [JwtAuth]
    public async Task<HttpResponseData> UpdateUserEmail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "users/email")] HttpRequestData req,
        FunctionContext context)
    {
        _logger.LogInformation("Updating user email");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);
            if (!authInfo.HasValue)
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            var requestBody = await req.ReadAsStringAsync();

            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Request body is empty");
                return badRequestResponse;
            }

            var updateRequest = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody, _jsonOptions);
            if (updateRequest == null || !updateRequest.ContainsKey("email"))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Email is required");
                return badRequestResponse;
            }

            var newEmail = updateRequest["email"];

            // Validate email format (basic check)
            if (string.IsNullOrWhiteSpace(newEmail) || !newEmail.Contains("@"))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid email format");
                return badRequestResponse;
            }

            // Check if email is already taken
            var emailExists = await _context.Users.AnyAsync(u => u.Email == newEmail && u.Id != authInfo.Value.UserId);
            if (emailExists)
            {
                var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
                await conflictResponse.WriteStringAsync("Email is already in use");
                return conflictResponse;
            }

            // Get the user
            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == authInfo.Value.UserId);

            if (user == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("User not found");
                return notFoundResponse;
            }

            // Update email
            user.Email = newEmail;
            await _context.SaveChangesAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            var responseData = new
            {
                user.Id,
                user.Email,
                user.Role,
                Profile = user.Profile != null ? new
                {
                    user.Profile.Id,
                    user.Profile.Name,
                    user.Profile.ProfileUrl
                } : null
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(responseData, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user email");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Failed to update email");
            return errorResponse;
        }
    }

    // ADMIN FUNCTIONS

    [Function("GetAllUsers")]
    [JwtAuth(requireAdmin: true)]
    public async Task<HttpResponseData> GetAllUsers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users/admin/all")] HttpRequestData req,
        FunctionContext context)
    {
        _logger.LogInformation("Getting all users (Admin only)");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);
            
            var users = await _context.Users
                .Include(u => u.Profile)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.Role,
                    Profile = u.Profile != null ? new
                    {
                        u.Profile.Id,
                        u.Profile.Name
                    } : null
                })
                .ToListAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(users, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized admin access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all users");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting users");
            return errorResponse;
        }
    }

    [Function("DeleteUser")]
    [JwtAuth(requireAdmin: true)]
    public async Task<HttpResponseData> DeleteUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "users/admin/{userId:int}")] HttpRequestData req, 
        int userId,
        FunctionContext context)
    {
        _logger.LogInformation($"Deleting user {userId} (Admin only)");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);

            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("User not found");
                return notFound;
            }

            // This will cascade delete Profile and all related data (Projects, Impacts, etc.)
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            var response = req.CreateResponse(HttpStatusCode.NoContent);
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting user {userId}");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while deleting the user");
            return errorResponse;
        }
    }

    [Function("DeleteAllNonAdminUsers")]
    [JwtAuth(requireAdmin: true)]
    public async Task<HttpResponseData> DeleteAllNonAdminUsers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "users/admin/delete-all-non-admin")] HttpRequestData req,
        FunctionContext context)
    {
        _logger.LogInformation("Deleting all non-admin users (Admin only)");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);

            // Get all non-admin users with their profiles
            var nonAdminUsers = await _context.Users
                .Where(u => u.Role == UserRole.User)
                .Include(u => u.Profile)
                .ToListAsync();

            var deleteCount = nonAdminUsers.Count;

            if (deleteCount == 0)
            {
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonSerializer.Serialize(new { message = "No non-admin users to delete", deletedCount = 0 }, _jsonOptions));
                return response;
            }

            // Get all profile IDs
            var profileIds = nonAdminUsers
                .Where(u => u.Profile != null)
                .Select(u => u.Profile!.Id)
                .ToList();

            // Delete all projects owned by these profiles
            var projectsToDelete = await _context.Projects
                .Where(p => profileIds.Contains(p.ProfileId))
                .ToListAsync();
            _context.Projects.RemoveRange(projectsToDelete);

            // Delete all collaborator records for these profiles
            var collaboratorsToDelete = await _context.ProjectCollaborators
                .Where(pc => profileIds.Contains(pc.ProfileId))
                .ToListAsync();
            _context.ProjectCollaborators.RemoveRange(collaboratorsToDelete);

            // Now delete all non-admin users (cascade will delete profiles)
            _context.Users.RemoveRange(nonAdminUsers);
            
            await _context.SaveChangesAsync();

            var successResponse = req.CreateResponse(HttpStatusCode.OK);
            successResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            var responseData = new
            {
                message = $"Successfully deleted {deleteCount} non-admin user(s) and their associated data",
                deletedCount = deleteCount,
                projectsDeleted = projectsToDelete.Count,
                collaborationsDeleted = collaboratorsToDelete.Count
            };

            await successResponse.WriteStringAsync(JsonSerializer.Serialize(responseData, _jsonOptions));
            return successResponse;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all non-admin users");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while deleting non-admin users");
            return errorResponse;
        }
    }

    [Function("CreateAdmin")]
    public async Task<HttpResponseData> CreateAdmin([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "users/admin/create")] HttpRequestData req)
    {
        _logger.LogInformation("Creating admin user");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Request body is empty");
                return badRequest;
            }

            var createAdminRequest = JsonSerializer.Deserialize<CreateAdminRequest>(requestBody, _jsonOptions);
            
            if (createAdminRequest == null || string.IsNullOrEmpty(createAdminRequest.Email) || 
                string.IsNullOrEmpty(createAdminRequest.Password) || string.IsNullOrEmpty(createAdminRequest.MasterPassword))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Email, password, and master password are required");
                return badRequest;
            }

            // Validate master password using service
            if (!await _masterPasswordService.IsValidPasswordAsync(createAdminRequest.MasterPassword))
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteStringAsync("Invalid master password");
                return unauthorized;
            }

            // Check if user already exists
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == createAdminRequest.Email);
            if (existingUser != null)
            {
                var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                await conflict.WriteStringAsync("User with this email already exists");
                return conflict;
            }

            // Create admin user
            var adminUser = new User
            {
                Email = createAdminRequest.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(createAdminRequest.Password),
                Role = UserRole.Admin
            };

            _context.Users.Add(adminUser);
            await _context.SaveChangesAsync();

            // Create associated profile
            var profile = new Profile
            {
                UserId = adminUser.Id,
                Name = createAdminRequest.Name
            };

            _context.Profiles.Add(profile);
            await _context.SaveChangesAsync();

            // Generate JWT token for admin user
            var token = _jwtService.GenerateToken(adminUser.Id, adminUser.Email, adminUser.Role.ToString());

            var response = req.CreateResponse(HttpStatusCode.Created);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            var responseData = new
            {
                adminUser.Id,
                adminUser.Email,
                adminUser.Role,
                Token = token,
                Profile = new
                {
                    profile.Id,
                    profile.Name
                }
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(responseData, _jsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating admin user");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while creating the admin user");
            return errorResponse;
        }
    }

    // REGISTRATION CODE MANAGEMENT

    [Function("GetRegistrationCode")]
    [JwtAuth(requireAdmin: true)]
    public async Task<HttpResponseData> GetRegistrationCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/registration-code")] HttpRequestData req,
        FunctionContext context)
    {
        _logger.LogInformation("Getting current registration code (Admin only)");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);

            var currentCode = await _registrationCodeService.GetCurrentCodeAsync();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            var responseData = new { code = currentCode };
            await response.WriteStringAsync(JsonSerializer.Serialize(responseData, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting registration code");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting registration code");
            return errorResponse;
        }
    }

    [Function("SetRegistrationCode")]
    [JwtAuth(requireAdmin: true)]
    public async Task<HttpResponseData> SetRegistrationCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/registration-code")] HttpRequestData req,
        FunctionContext context)
    {
        _logger.LogInformation("Setting new registration code (Admin only)");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Request body is empty");
                return badRequest;
            }

            var setCodeRequest = JsonSerializer.Deserialize<SetRegistrationCodeRequest>(requestBody, _jsonOptions);
            
            if (setCodeRequest == null || string.IsNullOrEmpty(setCodeRequest.Code))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Registration code is required");
                return badRequest;
            }

            await _registrationCodeService.SetCodeAsync(setCodeRequest.Code);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            var responseData = new 
            { 
                message = "Registration code updated successfully",
                code = setCodeRequest.Code
            };
            
            await response.WriteStringAsync(JsonSerializer.Serialize(responseData, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting registration code");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while setting registration code");
            return errorResponse;
        }
    }

    [Function("GetMasterPassword")]
    [JwtAuth(requireAdmin: true)]
    public async Task<HttpResponseData> GetMasterPassword(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/master-password")] HttpRequestData req,
        FunctionContext context)
    {
        _logger.LogInformation("Getting current master password (Admin only)");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);

            var masterPassword = await _masterPasswordService.GetCurrentPasswordAsync();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            var responseData = new { password = masterPassword };
            await response.WriteStringAsync(JsonSerializer.Serialize(responseData, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting master password");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting master password");
            return errorResponse;
        }
    }

    [Function("SetMasterPassword")]
    [JwtAuth(requireAdmin: true)]
    public async Task<HttpResponseData> SetMasterPassword(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/master-password")] HttpRequestData req,
        FunctionContext context)
    {
        _logger.LogInformation("Setting new master password (Admin only)");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Request body is empty");
                return badRequest;
            }

            var setMasterPasswordRequest = JsonSerializer.Deserialize<SetMasterPasswordRequest>(requestBody, _jsonOptions);
            
            if (setMasterPasswordRequest == null || string.IsNullOrEmpty(setMasterPasswordRequest.NewMasterPassword))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("New master password is required");
                return badRequest;
            }

            await _masterPasswordService.SetPasswordAsync(setMasterPasswordRequest.NewMasterPassword);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            var responseData = new 
            { 
                message = "Master password updated successfully",
                password = setMasterPasswordRequest.NewMasterPassword
            };
            
            await response.WriteStringAsync(JsonSerializer.Serialize(responseData, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting master password");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while setting master password");
            return errorResponse;
        }
    }
}
