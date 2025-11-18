using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SustainabilityCanvas.Api.Data;
using SustainabilityCanvas.Api.Models;
using SustainabilityCanvas.Api.Attributes;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SustainabilityCanvas.Api.Functions;

public class CreateCollaboratorRequest
{
    public int ProfileId { get; set; }
    public int ProjectId { get; set; }
    public CollaboratorRole Role { get; set; } = CollaboratorRole.Viewer;
}

public class ProjectCollaboratorFunctions
{
    private readonly SustainabilityCanvasContext _context;
    private readonly ILogger<ProjectCollaboratorFunctions> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ProjectCollaboratorFunctions(SustainabilityCanvasContext context, ILogger<ProjectCollaboratorFunctions> logger, JsonSerializerOptions jsonOptions)
    {
        _context = context;
        _logger = logger;
        _jsonOptions = jsonOptions;
    }

    [Function("GetProjectCollaborators")]
    [JwtAuth]
    public async Task<HttpResponseData> GetProjectCollaborators(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId}/collaborators")] HttpRequestData req,
        int projectId,
        FunctionContext context)
    {
        _logger.LogInformation($"Getting collaborators for project ID: {projectId}");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);
            var collaborators = await _context.ProjectCollaborators
                .Where(pc => pc.ProjectId == projectId)
                .Include(pc => pc.Profile)
                .ToListAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            await response.WriteStringAsync(JsonSerializer.Serialize(collaborators, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting collaborators for project ID: {projectId}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting project collaborators");
            return errorResponse;
        }
    }

    [Function("AddProjectCollaborator")]
    [JwtAuth]
    public async Task<HttpResponseData> AddProjectCollaborator(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId}/collaborators")] HttpRequestData req,
        int projectId,
        FunctionContext context)
    {
        _logger.LogInformation($"Adding collaborator to project ID: {projectId}");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);
            var requestBody = await req.ReadAsStringAsync();

            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Request body is empty");
                return badRequestResponse;
            }

            var collaboratorRequest = JsonSerializer.Deserialize<CreateCollaboratorRequest>(requestBody, _jsonOptions);
            if (collaboratorRequest == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid collaborator data");
                return badRequestResponse;
            }

            // Check if project exists
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Project with ID {projectId} not found");
                return notFoundResponse;
            }

            // Check if profile exists
            var profile = await _context.Profiles.FindAsync(collaboratorRequest.ProfileId);
            if (profile == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Profile with ID {collaboratorRequest.ProfileId} not found");
                return notFoundResponse;
            }

            // Check if collaboration already exists
            var existingCollaborator = await _context.ProjectCollaborators
                .FirstOrDefaultAsync(pc => pc.ProjectId == projectId && pc.ProfileId == collaboratorRequest.ProfileId);

            if (existingCollaborator != null)
            {
                var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
                await conflictResponse.WriteStringAsync("User is already a collaborator on this project");
                return conflictResponse;
            }

            // Create collaborator
            var collaborator = new ProjectCollaborator
            {
                ProjectId = projectId,
                ProfileId = collaboratorRequest.ProfileId,
                Role = collaboratorRequest.Role
            };

            _context.ProjectCollaborators.Add(collaborator);
            await _context.SaveChangesAsync();

            // Return the created collaborator with profile info
            var createdCollaborator = await _context.ProjectCollaborators
                .Include(pc => pc.Profile)
                .FirstOrDefaultAsync(pc => pc.Id == collaborator.Id);

            var response = req.CreateResponse(HttpStatusCode.Created);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            await response.WriteStringAsync(JsonSerializer.Serialize(createdCollaborator, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error adding collaborator to project ID: {projectId}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while adding collaborator");
            return errorResponse;
        }
    }

    [Function("UpdateCollaboratorRole")]
    [JwtAuth]
    public async Task<HttpResponseData> UpdateCollaboratorRole(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "collaborators/{collaboratorId}")] HttpRequestData req,
        int collaboratorId,
        FunctionContext context)
    {
        _logger.LogInformation($"Updating collaborator ID: {collaboratorId}");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);
            var existingCollaborator = await _context.ProjectCollaborators
                .Include(pc => pc.Profile)
                .FirstOrDefaultAsync(pc => pc.Id == collaboratorId);

            if (existingCollaborator == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Collaborator with ID {collaboratorId} not found");
                return notFoundResponse;
            }

            var requestBody = await req.ReadAsStringAsync();

            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Request body is empty");
                return badRequestResponse;
            }

            var updateRequest = JsonSerializer.Deserialize<CreateCollaboratorRequest>(requestBody, _jsonOptions);
            if (updateRequest == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid collaborator data");
                return badRequestResponse;
            }

            // Update role
            existingCollaborator.Role = updateRequest.Role;
            await _context.SaveChangesAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            await response.WriteStringAsync(JsonSerializer.Serialize(existingCollaborator, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating collaborator ID: {collaboratorId}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while updating collaborator");
            return errorResponse;
        }
    }

    [Function("RemoveCollaborator")]
    [JwtAuth]
    public async Task<HttpResponseData> RemoveCollaborator(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "collaborators/{collaboratorId}")] HttpRequestData req,
        int collaboratorId,
        FunctionContext context)
    {
        _logger.LogInformation($"Removing collaborator ID: {collaboratorId}");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);
            var collaborator = await _context.ProjectCollaborators.FindAsync(collaboratorId);
            if (collaborator == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Collaborator with ID {collaboratorId} not found");
                return notFoundResponse;
            }

            _context.ProjectCollaborators.Remove(collaborator);
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
            _logger.LogError(ex, $"Error removing collaborator ID: {collaboratorId}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while removing collaborator");
            return errorResponse;
        }
    }

    [Function("GetUserProjects")]
    [JwtAuth]
    public async Task<HttpResponseData> GetUserProjects(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "profiles/{profileId}/collaborations")] HttpRequestData req,
        int profileId,
        FunctionContext context)
    {
        _logger.LogInformation($"Getting projects for profile ID: {profileId}");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);
            var collaborations = await _context.ProjectCollaborators
                .Where(pc => pc.ProfileId == profileId)
                .Include(pc => pc.Project)
                .ToListAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            await response.WriteStringAsync(JsonSerializer.Serialize(collaborations, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting collaborations for profile ID: {profileId}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting user collaborations");
            return errorResponse;
        }
    }
}