using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SustainabilityCanvas.Api.Data;
using System.Net;
using System.Text.Json;

namespace SustainabilityCanvas.Api.Functions;

public class SdgsFunction
{
    private readonly SustainabilityCanvasContext _context;
    private readonly ILogger<SdgsFunction> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public SdgsFunction(SustainabilityCanvasContext context, ILogger<SdgsFunction> logger, JsonSerializerOptions jsonOptions)
    {
        _context = context;
        _logger = logger;
        _jsonOptions = jsonOptions;
    }

    [Function("GetSdgs")]
    public async Task<HttpResponseData> GetSdgs(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sdgs")] HttpRequestData req)
    {
        _logger.LogInformation("Getting all SDGs");

        try
        {
            var sdgs = await _context.Sdgs.ToListAsync();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            await response.WriteStringAsync(JsonSerializer.Serialize(sdgs, _jsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting SDGs");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while getting SDGs");
            return errorResponse;
        }
    }
}