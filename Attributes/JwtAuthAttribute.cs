using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using SustainabilityCanvas.Api.Services;
using System.Net;
using System.Text.Json;

namespace SustainabilityCanvas.Api.Attributes
{
    /// <summary>
    /// Attribute to automatically validate JWT tokens on Azure Functions.
    /// Can be configured to allow only admin users or any authenticated user.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class JwtAuthAttribute : Attribute
    {
        public bool RequireAdmin { get; set; } = false;
        
        public JwtAuthAttribute(bool requireAdmin = false)
        {
            RequireAdmin = requireAdmin;
        }
    }

    /// <summary>
    /// Extension methods for JWT authentication in Azure Functions
    /// </summary>
    public static class JwtAuthExtensions
    {
        /// <summary>
        /// Validates JWT token and returns user information if valid.
        /// Returns null if no JWT validation is required for this function.
        /// Throws UnauthorizedAccessException if JWT validation fails.
        /// </summary>
        public static (int UserId, string Username, bool IsAdmin)? ValidateJwtIfRequired(
            this HttpRequestData req, 
            FunctionContext context)
        {
            // Get the function method to check for JwtAuth attribute
            var method = context.FunctionDefinition.EntryPoint;
            var methodInfo = Type.GetType(method.Substring(0, method.LastIndexOf('.')))
                ?.GetMethod(method.Substring(method.LastIndexOf('.') + 1));

            if (methodInfo == null)
                return null;

            // Check if the method has JwtAuth attribute
            var jwtAuthAttr = methodInfo.GetCustomAttributes(typeof(JwtAuthAttribute), false)
                .FirstOrDefault() as JwtAuthAttribute;

            if (jwtAuthAttr == null)
                return null; // No JWT validation required

            // Get JWT service from DI
            var jwtService = context.InstanceServices.GetRequiredService<JwtService>();

            // Extract and validate token
            if (!req.Headers.TryGetValues("Authorization", out var authHeaders))
            {
                throw new UnauthorizedAccessException("Authorization header is missing");
            }

            var authHeader = authHeaders.FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                throw new UnauthorizedAccessException("Invalid authorization header format");
            }

            var token = authHeader.Substring(7); // Remove "Bearer " prefix

            try
            {
                // Validate the token
                if (!jwtService.IsTokenValid(token))
                {
                    throw new UnauthorizedAccessException("Invalid or expired token");
                }

                // Extract user information
                var userIdStr = jwtService.GetUserIdFromToken(token);
                var username = jwtService.GetUsernameFromToken(token);
                var isAdmin = jwtService.IsAdmin(token);

                if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(username))
                {
                    throw new UnauthorizedAccessException("Invalid token claims");
                }

                if (!int.TryParse(userIdStr, out var userId))
                {
                    throw new UnauthorizedAccessException("Invalid user ID in token");
                }

                // Check admin requirement
                if (jwtAuthAttr.RequireAdmin && !isAdmin)
                {
                    throw new UnauthorizedAccessException("Admin access required");
                }

                return (userId, username, isAdmin);
            }
            catch (Exception ex) when (!(ex is UnauthorizedAccessException))
            {
                throw new UnauthorizedAccessException("Invalid or expired token");
            }
        }

        /// <summary>
        /// Creates a standardized unauthorized response
        /// </summary>
        public static HttpResponseData CreateUnauthorizedResponse(this HttpRequestData req, string message = "Unauthorized")
        {
            var response = req.CreateResponse(HttpStatusCode.Unauthorized);
            response.Headers.Add("Content-Type", "application/json");
            
            var errorResponse = new { error = message };
            response.WriteString(JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            }));
            
            return response;
        }
    }
}