using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SustainabilityCanvas.Api.Data;
using SustainabilityCanvas.Api.Models;
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
    public async Task<HttpResponseData> GetProjects(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects")] HttpRequestData req)
    {
        _logger.LogInformation("Getting all projects");

        try
        {
            var projects = await _context.Projects.ToListAsync();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            await response.WriteStringAsync(JsonSerializer.Serialize(projects, _jsonOptions));
            return response;
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
    public async Task<HttpResponseData> GetProjectById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{id}")] HttpRequestData req,
        int id)
    {
        _logger.LogInformation($"Getting project with ID: {id}");

        try
        {
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
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting project with ID: {id}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting the project");
            return errorResponse;
        }
    }

    [Function("CreateProject")]
    public async Task<HttpResponseData> CreateProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects")] HttpRequestData req)
    {
        _logger.LogInformation("Creating a new project");

        try
        {
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while creating the project");
            return errorResponse;
        }
    }

    [Function("DeleteProject")]
    public async Task<HttpResponseData> DeleteProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "projects/{id}")] HttpRequestData req,
        int id)
    {
        _logger.LogInformation($"Deleting project with ID: {id}");

        try
        {
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
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting project with ID: {id}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while deleting the project");
            return errorResponse;
        }
    }

    [Function("UpdateProject")]
    public async Task<HttpResponseData> UpdateProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "projects/{id}")] HttpRequestData req,
        int id)
    {
        _logger.LogInformation($"Updating project with ID: {id}");

        try
        {
            var existingProject = await _context.Projects.FindAsync(id);
            if (existingProject == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Project with ID {id} not found");
                return notFoundResponse;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var updatedProject = JsonSerializer.Deserialize<Project>(requestBody, _jsonOptions);

            if (updatedProject == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid project data");
                return badRequestResponse;
            }

            // Update fields
            existingProject.Title = updatedProject.Title;
            existingProject.Description = updatedProject.Description;
            existingProject.ProfileId = updatedProject.ProfileId;

            await _context.SaveChangesAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(existingProject, _jsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating project with ID: {id}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while updating the project");
            return errorResponse;
        }
    }

    [Function("GetProjectsByProfileId")]
    public async Task<HttpResponseData> GetProjectsByProfileId(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "profiles/{profileId}/projects")] HttpRequestData req,
        int profileId)
    {
        _logger.LogInformation($"Getting projects for profile ID: {profileId}");

        try
        {
            var projects = await _context.Projects
                .Where(p => p.ProfileId == profileId)
                .ToListAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(projects, _jsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting projects for profile ID: {profileId}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting projects for the profile");
            return errorResponse;
        }
    }
}