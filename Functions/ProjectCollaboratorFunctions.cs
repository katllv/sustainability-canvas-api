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
    public string Email { get; set; } = string.Empty;
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
            
            // Get the project to find the owner
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Project with ID {projectId} not found");
                return notFoundResponse;
            }

            var collaborators = new List<object>();

            // Add owner first
            var owner = await _context.Profiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == project.ProfileId);
            
            if (owner != null)
            {
                collaborators.Add(new
                {
                    ProfileId = owner.Id,
                    Name = owner.Name,
                    ProfileUrl = owner.ProfileUrl,
                    Email = owner.User.Email
                });
            }

            // Add other collaborators (excluding owner if they're also listed)
            var projectCollaborators = await _context.ProjectCollaborators
                .Where(pc => pc.ProjectId == projectId && pc.ProfileId != project.ProfileId)
                .Include(pc => pc.Profile)
                    .ThenInclude(p => p.User)
                .Select(pc => new
                {
                    ProfileId = pc.Profile.Id,
                    Name = pc.Profile.Name,
                    ProfileUrl = pc.Profile.ProfileUrl,
                    Email = pc.Profile.User.Email
                })
                .ToListAsync();

            collaborators.AddRange(projectCollaborators);

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
            if (collaboratorRequest == null || string.IsNullOrEmpty(collaboratorRequest.Email))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Email is required");
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

            // Find user by email
            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Email == collaboratorRequest.Email);
            
            if (user == null || user.Profile == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"User with email {collaboratorRequest.Email} not found");
                return notFoundResponse;
            }

            // Check if collaboration already exists
            var existingCollaborator = await _context.ProjectCollaborators
                .FirstOrDefaultAsync(pc => pc.ProjectId == projectId && pc.ProfileId == user.Profile.Id);

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
                ProfileId = user.Profile.Id
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

    [Function("RemoveCollaborator")]
    [JwtAuth]
    public async Task<HttpResponseData> RemoveCollaborator(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "projects/{projectId}/collaborators/{profileId}")] HttpRequestData req,
        int projectId,
        int profileId,
        FunctionContext context)
    {
        _logger.LogInformation($"Removing collaborator with profile ID {profileId} from project {projectId}");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);
            
            // Get the project
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Project with ID {projectId} not found");
                return notFoundResponse;
            }

            // Check if we're removing the owner
            if (project.ProfileId == profileId)
            {
                // Get other collaborators
                var otherCollaborators = await _context.ProjectCollaborators
                    .Where(pc => pc.ProjectId == projectId)
                    .ToListAsync();

                if (otherCollaborators.Any())
                {
                    // Transfer ownership to the first collaborator
                    var newOwner = otherCollaborators.First();
                    project.ProfileId = newOwner.ProfileId;
                    
                    // Remove the new owner from collaborators table to avoid duplication
                    _context.ProjectCollaborators.Remove(newOwner);
                    
                    await _context.SaveChangesAsync();
                    
                    var transferResponse = req.CreateResponse(HttpStatusCode.OK);
                    await transferResponse.WriteStringAsync($"Ownership transferred to profile ID {newOwner.ProfileId}");
                    return transferResponse;
                }
                else
                {
                    // No other collaborators - delete the project
                    _context.Projects.Remove(project);
                    await _context.SaveChangesAsync();
                    
                    var deleteResponse = req.CreateResponse(HttpStatusCode.OK);
                    deleteResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    await deleteResponse.WriteStringAsync(JsonSerializer.Serialize(new { projectDeleted = true }, _jsonOptions));
                    return deleteResponse;
                }
            }
            else
            {
                // Removing a regular collaborator
                var collaborator = await _context.ProjectCollaborators
                    .FirstOrDefaultAsync(pc => pc.ProjectId == projectId && pc.ProfileId == profileId);
                
                if (collaborator == null)
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync($"Collaborator with profile ID {profileId} not found in project {projectId}");
                    return notFoundResponse;
                }

                _context.ProjectCollaborators.Remove(collaborator);
                await _context.SaveChangesAsync();

                var response = req.CreateResponse(HttpStatusCode.NoContent);
                return response;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error removing collaborator profile ID {profileId} from project {projectId}");

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

