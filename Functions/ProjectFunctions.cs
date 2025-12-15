using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SustainabilityCanvas.Api.Data;
using SustainabilityCanvas.Api.Models;
using SustainabilityCanvas.Api.Attributes;
using System.Net;
using System.Text.Json;

namespace SustainabilityCanvas.Api.Functions;

public class ProjectFunctions
{
    private readonly SustainabilityCanvasContext _context;
    private readonly ILogger<ProjectFunctions> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ProjectFunctions(SustainabilityCanvasContext context, ILogger<ProjectFunctions> logger, JsonSerializerOptions jsonOptions)
    {
        _context = context;
        _logger = logger;
        _jsonOptions = jsonOptions;
    }

    [Function("GetProjects")]
    [JwtAuth]
    public async Task<HttpResponseData> GetProjects(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects")] HttpRequestData req,
        FunctionContext context)
    {
        _logger.LogInformation("Getting all projects");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);

            var projects = await _context.Projects.ToListAsync();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            await response.WriteStringAsync(JsonSerializer.Serialize(projects, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting projects");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting projects");
            return errorResponse;
        }
    }

    [Function("GetProjectById")]
    [JwtAuth]
    public async Task<HttpResponseData> GetProjectById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{id}")] HttpRequestData req,
        int id,
        FunctionContext context)
    {
        _logger.LogInformation($"Getting project with ID: {id}");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);

            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Project with ID {id} not found");
                return notFoundResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(project, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting project with ID: {id}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting the project");
            return errorResponse;
        }
    }

    [Function("CreateProject")]
    [JwtAuth]
    public async Task<HttpResponseData> CreateProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects")] HttpRequestData req,
        FunctionContext context)
    {
        _logger.LogInformation("Creating a new project");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var project = JsonSerializer.Deserialize<Project>(requestBody, _jsonOptions);

            if (project == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid project data");
                return badRequestResponse;
            }

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            var response = req.CreateResponse(HttpStatusCode.Created);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(project, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while creating the project");
            return errorResponse;
        }
    }

    [Function("DeleteProject")]
    [JwtAuth]
    public async Task<HttpResponseData> DeleteProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "projects/{id}")] HttpRequestData req,
        int id,
        FunctionContext context)
    {
        _logger.LogInformation($"Deleting project with ID: {id}");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);

            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Project with ID {id} not found");
                return notFoundResponse;
            }

            _context.Projects.Remove(project);
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
            _logger.LogError(ex, $"Error deleting project with ID: {id}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while deleting the project");
            return errorResponse;
        }
    }

    [Function("UpdateProject")]
    [JwtAuth]
    public async Task<HttpResponseData> UpdateProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "projects/{id}")] HttpRequestData req,
        int id,
        FunctionContext context)
    {
        _logger.LogInformation($"Updating project with ID: {id}");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);

            var existingProject = await _context.Projects.FindAsync(id);
            if (existingProject == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Project with ID {id} not found");
                return notFoundResponse;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Request body is empty");
                return badRequestResponse;
            }

            _logger.LogInformation($"Request body: {requestBody}");

            var updatedProject = JsonSerializer.Deserialize<Project>(requestBody, _jsonOptions);

            if (updatedProject == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid project data");
                return badRequestResponse;
            }

            // Update only the fields that are provided
            if (!string.IsNullOrEmpty(updatedProject.Title))
            {
                existingProject.Title = updatedProject.Title;
            }
            
            existingProject.Description = updatedProject.Description;
            existingProject.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(existingProject, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, $"JSON deserialization error for project ID: {id}");
            var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequestResponse.WriteStringAsync($"Invalid JSON format: {ex.Message}");
            return badRequestResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating project with ID: {id}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"An error occurred while updating the project: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("GetProjectsByProfileId")]
    [JwtAuth]
    public async Task<HttpResponseData> GetProjectsByProfileId(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "profiles/{profileId}/projects")] HttpRequestData req,
        int profileId,
        FunctionContext context)
    {
        _logger.LogInformation($"Getting projects for profile ID: {profileId}");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);

            var projects = await _context.Projects
                .Where(p => p.ProfileId == profileId)
                .ToListAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(projects, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting projects for profile ID: {profileId}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting projects for the profile");
            return errorResponse;
        }
    }

    [Function("GetUserProjectsWithCollaborators")]
    [JwtAuth]
    public async Task<HttpResponseData> GetUserProjectsWithCollaborators(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "profiles/{profileId}/projects-full")] HttpRequestData req,
        int profileId,
        FunctionContext context)
    {
        _logger.LogInformation($"Getting all projects with collaborators for profile ID: {profileId}");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);

            // Get all profiles with users for later use
            var allProfiles = await _context.Profiles
                .Include(p => p.User)
                .ToDictionaryAsync(p => p.Id);

            // Get projects owned by user
            var ownedProjects = await _context.Projects
                .Where(p => p.ProfileId == profileId)
                .Include(p => p.ProjectCollaborators)
                    .ThenInclude(pc => pc.Profile)
                .ToListAsync();

            // Get projects where user is a collaborator
            var collaboratedProjectIds = await _context.ProjectCollaborators
                .Where(pc => pc.ProfileId == profileId)
                .Select(pc => pc.ProjectId)
                .ToListAsync();

            var collaboratedProjects = await _context.Projects
                .Where(p => collaboratedProjectIds.Contains(p.Id))
                .Include(p => p.ProjectCollaborators)
                    .ThenInclude(pc => pc.Profile)
                .ToListAsync();

            // Combine and deduplicate
            var allProjects = ownedProjects
                .Concat(collaboratedProjects)
                .GroupBy(p => p.Id)
                .Select(g => g.First())
                .ToList();

            // Transform to include owner and collaborators in a simplified format
            var projectsWithCollaborators = allProjects.Select(p => new
            {
                p.Id,
                p.ProfileId,
                p.Title,
                p.Description,
                p.CreatedAt,
                p.UpdatedAt,
                Collaborators = new[]
                {
                    // Get the owner profile
                    allProfiles.GetValueOrDefault(p.ProfileId) != null
                        ? new {
                            ProfileId = allProfiles[p.ProfileId].Id,
                            Name = allProfiles[p.ProfileId].Name,
                            Email = allProfiles[p.ProfileId].User?.Email ?? string.Empty,
                            ProfileUrl = allProfiles[p.ProfileId].ProfileUrl
                        }
                        : null
                }
                .Concat(
                    // Get all collaborators
                    p.ProjectCollaborators
                        .Where(pc => pc.ProfileId != p.ProfileId)
                        .Select(pc => new {
                            ProfileId = pc.Profile.Id,
                            Name = pc.Profile.Name,
                            Email = pc.Profile.User?.Email ?? string.Empty,
                            ProfileUrl = pc.Profile.ProfileUrl
                        })
                )
                .Where(c => c != null && !string.IsNullOrEmpty(c.Email))
                .Distinct()
                .ToList()
            }).ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            await response.WriteStringAsync(JsonSerializer.Serialize(projectsWithCollaborators, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting projects for profile ID: {profileId}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting user projects");
            return errorResponse;
        }
    }

    [Function("GetProjectAnalysis")]
    [JwtAuth]
    public async Task<HttpResponseData> GetProjectAnalysis(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId}/analysis")] HttpRequestData req,
        int projectId,
        FunctionContext context)
    {
        _logger.LogInformation("Getting analysis data for project ID: {ProjectId}", projectId);

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);
            if (!authInfo.HasValue)
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            // Get user's profile
            var profile = await _context.Profiles
                .FirstOrDefaultAsync(p => p.UserId == authInfo.Value.UserId);

            if (profile == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Profile not found");
                return notFoundResponse;
            }

            // Check if user has access to this project (is owner or collaborator)
            var hasAccess = await _context.Projects
                .Where(p => p.Id == projectId)
                .Where(p => p.ProfileId == profile.Id || 
                           p.ProjectCollaborators.Any(c => c.ProfileId == profile.Id))
                .AnyAsync();

            if (!hasAccess)
            {
                var forbiddenResponse = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbiddenResponse.WriteStringAsync("You don't have access to this project");
                return forbiddenResponse;
            }

            // Get all impacts for this project with their SDG associations
            var impacts = await _context.Impacts
                .Where(i => i.ProjectId == projectId)
                .Include(i => i.ImpactSdgs)
                .ToListAsync();

            // Calculate summary statistics
            var totalEntries = impacts.Count;
            var sdgsCovered = impacts
                .SelectMany(i => i.ImpactSdgs.Select(is2 => is2.SdgId))
                .Distinct()
                .Count();
            var activeDimensions = impacts
                .Select(i => i.Dimension)
                .Distinct()
                .Count();

            // Calculate impact distribution by RelationType
            var impactDistribution = Enum.GetValues<RelationType>()
                .Select(rt => new
                {
                    name = rt.ToString(),
                    value = impacts.Count(i => i.Relation == rt)
                })
                .Where(x => x.value > 0)
                .ToList();

            // Calculate dimension distribution by SustainabilityDimension
            var dimensionDistribution = Enum.GetValues<SustainabilityDimension>()
                .Select(sd => new
                {
                    name = sd.ToString(),
                    value = impacts.Count(i => i.Dimension == sd)
                })
                .Where(x => x.value > 0)
                .ToList();

            // Calculate SDG counts
            var sdgCounts = impacts
                .SelectMany(i => i.ImpactSdgs.Select(is2 => is2.SdgId))
                .GroupBy(sdgId => sdgId)
                .Select(g => new
                {
                    sdg = g.Key,
                    count = g.Count()
                })
                .OrderBy(x => x.sdg)
                .ToList();

            var analysisData = new
            {
                summary = new
                {
                    totalEntries,
                    sdgsCovered,
                    activeDimensions
                },
                impactDistribution,
                dimensionDistribution,
                sdgCounts
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            await response.WriteStringAsync(JsonSerializer.Serialize(analysisData, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project analysis for project ID: {ProjectId}", projectId);

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting project analysis");
            return errorResponse;
        }
    }
}