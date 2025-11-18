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

            var profile = await _context.Profiles.FindAsync(id);
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
            
            var updatedProfile = JsonSerializer.Deserialize<Models.Profile>(requestBody, _jsonOptions);

            if (updatedProfile == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid profile data");
                return badRequestResponse;
            }

            // Update profile properties
            profile.Name = updatedProfile.Name;
            profile.Email = updatedProfile.Email;
            profile.ProfileUrl = updatedProfile.ProfileUrl;

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
}