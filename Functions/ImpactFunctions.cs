using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SustainabilityCanvas.Api.Data;
using SustainabilityCanvas.Api.Attributes;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using SustainabilityCanvas.Api.Models;

namespace SustainabilityCanvas.Api.Functions;

public class CreateImpactRequest
{
    public int ProjectId { get; set; }
    public SectionType Type { get; set; }
    public ImpactRating Level { get; set; }
    public SustainabilityDimension Dimension { get; set; }
    public RelationType Relation { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<int> SdgIds { get; set; } = new();
}

public class ImpactFunctions
{
    private readonly SustainabilityCanvasContext _context;
    private readonly ILogger<ImpactFunctions> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ImpactFunctions(SustainabilityCanvasContext context, ILogger<ImpactFunctions> logger, JsonSerializerOptions jsonOptions)
    {
        _context = context;
        _logger = logger;
        _jsonOptions = jsonOptions;
    }

    [Function("GetImpacts")]
    [JwtAuth]
    public async Task<HttpResponseData> GetImpacts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "impacts")] HttpRequestData req,
        FunctionContext context)
    {
        _logger.LogInformation("Getting all Impacts");
        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);

            var impacts = await _context.Impacts
                .Include(i => i.ImpactSdgs)
                .ThenInclude(impactSdg => impactSdg.Sdg)
                .ToListAsync();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            await response.WriteStringAsync(JsonSerializer.Serialize(impacts, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Impacts");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting Impacts");
            return errorResponse;
        }
    }

    [Function("GetImpactById")]
    [JwtAuth]
    public async Task<HttpResponseData> GetImpactById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "impacts/{id}")] HttpRequestData req,
        int id,
        FunctionContext context)
    {
        _logger.LogInformation($"Getting Impact with ID: {id}");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);
            var impact = await _context.Impacts
                .Include(i => i.ImpactSdgs)
                .ThenInclude(impactSdg => impactSdg.Sdg)
                .FirstOrDefaultAsync(i => i.Id == id);
            
            if (impact == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Impact with ID: {id} not found");
                return notFoundResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            await response.WriteStringAsync(JsonSerializer.Serialize(impact, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting Impact with ID: {id}");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting the Impact");
            return errorResponse;
        }
    }

    [Function("GetImpactsByProjectId")]
    [JwtAuth]
    public async Task<HttpResponseData> GetImpactsByProjectId(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId}/impacts")] HttpRequestData req,
        int projectId,
        FunctionContext context)
    {
        _logger.LogInformation($"Getting Impacts for Project ID: {projectId}");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);
            var impacts = await _context.Impacts
                .Where(i => i.ProjectId == projectId)
                .Include(i => i.ImpactSdgs)
                .ThenInclude(impactSdg => impactSdg.Sdg)
                .ToListAsync();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            await response.WriteStringAsync(JsonSerializer.Serialize(impacts, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting Impacts for Project ID: {projectId}");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting Impacts for the Project");
            return errorResponse;
        }
    }

    [Function("CreateImpact")]
    [JwtAuth]
    public async Task<HttpResponseData> CreateImpact(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "impacts")] HttpRequestData req,
        FunctionContext context)
    {
        _logger.LogInformation("Creating a new Impact");

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
            
            var impactRequest = JsonSerializer.Deserialize<CreateImpactRequest>(requestBody, _jsonOptions);
            if (impactRequest == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid Impact data");
                return badRequestResponse;
            }

            // Create the Impact
            var impact = new Impact
            {
                ProjectId = impactRequest.ProjectId,
                Type = impactRequest.Type,
                Level = impactRequest.Level,
                Dimension = impactRequest.Dimension,
                Relation = impactRequest.Relation,
                Description = impactRequest.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Impacts.Add(impact);
            await _context.SaveChangesAsync(); // Save to get Impact ID

            // Create ImpactSdg relationships
            foreach (var sdgId in impactRequest.SdgIds)
            {
                var impactSdg = new ImpactSdg
                {
                    ImpactId = impact.Id,
                    SdgId = sdgId
                };
                _context.ImpactSdgs.Add(impactSdg);
            }
            await _context.SaveChangesAsync();

            // Return the created impact with SDG relationships
            var createdImpact = await _context.Impacts
                .Include(i => i.ImpactSdgs)
                .ThenInclude(impactSdg => impactSdg.Sdg)
                .FirstOrDefaultAsync(i => i.Id == impact.Id);

            var response = req.CreateResponse(HttpStatusCode.Created);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            await response.WriteStringAsync(JsonSerializer.Serialize(createdImpact, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Impact");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while creating the Impact");
            return errorResponse;
        }
    }

    [Function("DeleteImpact")]
    [JwtAuth]
    public async Task<HttpResponseData> DeleteImpact(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "impacts/{id}")] HttpRequestData req,
        int id,
        FunctionContext context)
    {
        _logger.LogInformation($"Deleting Impact with ID: {id}");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);
            var impact = await _context.Impacts.FindAsync(id);
            if (impact == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Impact with ID: {id} not found");
                return notFoundResponse;
            }

            _context.Impacts.Remove(impact);
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
            _logger.LogError(ex, $"Error deleting Impact with ID: {id}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while deleting the Impact");
            return errorResponse;
        }
    }

    [Function("UpdateImpact")]
    [JwtAuth]
    public async Task<HttpResponseData> UpdateImpact(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "impacts/{id}")] HttpRequestData req,
        int id,
        FunctionContext context)
    {
        _logger.LogInformation($"Updating Impact with ID: {id}");

        try
        {
            var authInfo = req.ValidateJwtIfRequired(context);
            var existingImpact = await _context.Impacts
                .Include(i => i.ImpactSdgs)
                .FirstOrDefaultAsync(i => i.Id == id);
                
            if (existingImpact == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Impact with ID: {id} not found");
                return notFoundResponse;
            }

            var requestBody = await req.ReadAsStringAsync();
            
            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Request body is empty");
                return badRequestResponse;
            }
            
            var updateRequest = JsonSerializer.Deserialize<CreateImpactRequest>(requestBody, _jsonOptions);
            if (updateRequest == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid Impact data");
                return badRequestResponse;
            }

            // Update Impact fields
            existingImpact.ProjectId = updateRequest.ProjectId;
            existingImpact.Type = updateRequest.Type;
            existingImpact.Level = updateRequest.Level;
            existingImpact.Dimension = updateRequest.Dimension;
            existingImpact.Relation = updateRequest.Relation;
            existingImpact.Description = updateRequest.Description;
            existingImpact.UpdatedAt = DateTime.UtcNow;

            // Remove existing SDG relationships
            _context.ImpactSdgs.RemoveRange(existingImpact.ImpactSdgs);

            // Add new SDG relationships
            foreach (var sdgId in updateRequest.SdgIds)
            {
                var impactSdg = new ImpactSdg
                {
                    ImpactId = existingImpact.Id,
                    SdgId = sdgId
                };
                _context.ImpactSdgs.Add(impactSdg);
            }

            await _context.SaveChangesAsync();

            // Return updated impact with SDG relationships
            var updatedImpact = await _context.Impacts
                .Include(i => i.ImpactSdgs)
                .ThenInclude(impactSdg => impactSdg.Sdg)
                .FirstOrDefaultAsync(i => i.Id == id);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            await response.WriteStringAsync(JsonSerializer.Serialize(updatedImpact, _jsonOptions));
            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return req.CreateUnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating Impact with ID: {id}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while updating the Impact");
            return errorResponse;
        }
    }
}