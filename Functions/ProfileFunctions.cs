using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SustainabilityCanvas.Api.Data;
using SustainabilityCanvas.Api.Attributes;
using System.Net;
using System.Text.Json;

namespace SustainabilityCanvas.Api.Functions;

public class ProfileFunctions
{
    private readonly SustainabilityCanvasContext _context;
    private readonly ILogger<ProfileFunctions> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ProfileFunctions(SustainabilityCanvasContext context, ILogger<ProfileFunctions> logger, JsonSerializerOptions jsonOptions)
    {
        _context = context;
        _logger = logger;
        _jsonOptions = jsonOptions;
    }



    [Function("GetProfileById")]
    [JwtAuth]
    public async Task<HttpResponseData> GetProfileById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "profiles/{id}")] HttpRequestData req,
        int id,
        FunctionContext context)
    {
        _logger.LogInformation($"Getting profile with ID: {id}");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);

            var profile = await _context.Profiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);
                
            if (profile == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Profile with ID {id} not found");
                return notFoundResponse;
            }

            // Check if user can access this profile (own profile or admin)
            if (authInfo.HasValue && !authInfo.Value.IsAdmin && profile.UserId != authInfo.Value.UserId)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("You can only view your own profile");
                return forbidden;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            // Fetch user separately to ensure we get the Role
            var user = await _context.Users.FindAsync(profile.UserId);
            _logger.LogInformation($"Profile ID: {profile.Id}, UserId: {profile.UserId}, User loaded: {user != null}");
            if (user != null)
            {
                _logger.LogInformation($"User Email: {user.Email}, Role enum: {user.Role}, Role int: {(int)user.Role}");
            }

            var roleValue = user?.Role ?? 0;
            _logger.LogInformation($"Final role value being sent: {roleValue} (int: {(int)roleValue})");

            var profileData = new
            {
                profile.Id,
                profile.Name,
                profile.ProfileUrl,
                profile.JobTitle,
                profile.Department,
                profile.Organization,
                profile.Location,
                Role = (int)roleValue
            };

            var json = JsonSerializer.Serialize(profileData, _jsonOptions);
            _logger.LogInformation($"Sending JSON: {json}");
            
            await response.WriteStringAsync(json);
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting profile with ID: {id}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting the profile");
            return errorResponse;
        }
    }


    [Function("UpdateProfile")]
    [JwtAuth]
    public async Task<HttpResponseData> UpdateProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "profiles/{id}")] HttpRequestData req,
        int id,
        FunctionContext context)
    {
        _logger.LogInformation($"Updating profile with ID: {id}");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);

            var profile = await _context.Profiles.FindAsync(id);
            if (profile == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Profile with ID {id} not found");
                return notFoundResponse;
            }

            // Check if user owns this profile (users can only edit their own profiles)
            if (authInfo.HasValue && profile.UserId != authInfo.Value.UserId)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("You can only edit your own profile");
                return forbidden;
            }

            var requestBody = await req.ReadAsStringAsync();
            
            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Request body is empty");
                return badRequestResponse;
            }
            
            // Parse as a dictionary to only update provided fields
            var updates = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(requestBody, _jsonOptions);

            if (updates == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid profile data");
                return badRequestResponse;
            }

            _logger.LogInformation($"Received updates: {string.Join(", ", updates.Keys)}");

            // Only update fields that are present in the request (case-insensitive check)
            foreach (var key in updates.Keys)
            {
                var lowerKey = key.ToLowerInvariant();
                
                if (lowerKey == "name")
                    profile.Name = updates[key].GetString() ?? profile.Name;
                else if (lowerKey == "profileurl")
                    profile.ProfileUrl = updates[key].GetString();
                else if (lowerKey == "jobtitle")
                    profile.JobTitle = updates[key].GetString();
                else if (lowerKey == "department")
                    profile.Department = updates[key].GetString();
                else if (lowerKey == "organization")
                    profile.Organization = updates[key].GetString();
                else if (lowerKey == "location")
                    profile.Location = updates[key].GetString();
            }

            await _context.SaveChangesAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            await response.WriteStringAsync(JsonSerializer.Serialize(profile, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating profile with ID: {id}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while updating the profile");
            return errorResponse;
        }
    }

    [Function("UploadProfilePicture")]
    [JwtAuth]
    public async Task<HttpResponseData> UploadProfilePicture(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "profiles/{id}/picture")] HttpRequestData req,
        int id,
        FunctionContext context)
    {
        _logger.LogInformation($"Uploading profile picture for profile ID: {id}");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);

            var profile = await _context.Profiles.FindAsync(id);
            if (profile == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Profile with ID {id} not found");
                return notFoundResponse;
            }

            // Check if user owns this profile
            if (authInfo.HasValue && profile.UserId != authInfo.Value.UserId)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("You can only upload pictures to your own profile");
                return forbidden;
            }

            var requestBody = await req.ReadAsStringAsync();
            
            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Request body is empty");
                return badRequestResponse;
            }
            
            var uploadData = JsonSerializer.Deserialize<ProfilePictureUpload>(requestBody, _jsonOptions);

            if (uploadData == null || string.IsNullOrEmpty(uploadData.ImageData))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid image data");
                return badRequestResponse;
            }

            // Store as data URL (base64 encoded)
            profile.ProfileUrl = uploadData.ImageData;

            await _context.SaveChangesAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            await response.WriteStringAsync(JsonSerializer.Serialize(new { profileUrl = profile.ProfileUrl }, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error uploading profile picture for ID: {id}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while uploading the profile picture");
            return errorResponse;
        }
    }
}

public class ProfilePictureUpload
{
    public string ImageData { get; set; } = string.Empty;
}