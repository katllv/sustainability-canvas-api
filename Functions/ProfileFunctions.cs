using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SustainabilityCanvas.Api.Data;
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

    [Function("GetProfiles")]
    public async Task<HttpResponseData> GetProfiles(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "profiles")] HttpRequestData req)
    {
        _logger.LogInformation("Getting all profiles");

        try
        {
            var profiles = await _context.Profiles.ToListAsync();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            await response.WriteStringAsync(JsonSerializer.Serialize(profiles, _jsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profiles");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting profiles");
            return errorResponse;
        }
    }

    [Function("GetProfileById")]
    public async Task<HttpResponseData> GetProfileById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "profiles/{id}")] HttpRequestData req,
        int id)
    {
        _logger.LogInformation($"Getting profile with ID: {id}");

        try
        {
            var profile = await _context.Profiles.FindAsync(id);
            if (profile == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Profile with ID {id} not found");
                return notFoundResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            await response.WriteStringAsync(JsonSerializer.Serialize(profile));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting profile with ID: {id}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting the profile");
            return errorResponse;
        }
    }

    [Function("CreateProfile")]
    public async Task<HttpResponseData> CreateProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "profiles")] HttpRequestData req)
    {
        _logger.LogInformation("Creating a new profile");
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            
            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Request body is empty");
                return badRequestResponse;
            }
            
            var newProfile = JsonSerializer.Deserialize<Models.Profile>(requestBody, _jsonOptions);

            if (newProfile == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid profile data");
                return badRequestResponse;
            }

            _context.Profiles.Add(newProfile);
            await _context.SaveChangesAsync();

            var response = req.CreateResponse(HttpStatusCode.Created);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            await response.WriteStringAsync(JsonSerializer.Serialize(newProfile, _jsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating profile");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while creating the profile");
            return errorResponse;
        }
    }

    [Function("DeleteProfile")]
    public async Task<HttpResponseData> DeleteProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "profiles/{id}")] HttpRequestData req,
        int id)
    {
        _logger.LogInformation($"Deleting profile with ID: {id}");
        try
        {
            var profile = await _context.Profiles.FindAsync(id);
            if (profile == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Profile with ID {id} not found");
                return notFoundResponse;
            }

            _context.Profiles.Remove(profile);
            await _context.SaveChangesAsync();

            var response = req.CreateResponse(HttpStatusCode.NoContent);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting profile with ID: {id}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while deleting the profile");
            return errorResponse;
        }
    }

    [Function("UpdateProfile")]
    public async Task<HttpResponseData> UpdateProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "profiles/{id}")] HttpRequestData req,
        int id)
    {
        _logger.LogInformation($"Updating profile with ID: {id}");

        try
        {
            var profile = await _context.Profiles.FindAsync(id);
            if (profile == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Profile with ID {id} not found");
                return notFoundResponse;
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
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating profile with ID: {id}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while updating the profile");
            return errorResponse;
        }
    }
}