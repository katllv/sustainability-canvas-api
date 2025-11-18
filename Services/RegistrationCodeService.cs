using Microsoft.EntityFrameworkCore;
using SustainabilityCanvas.Api.Data;
using SustainabilityCanvas.Api.Models;

namespace SustainabilityCanvas.Api.Services;

public class RegistrationCodeService
{
    private readonly SustainabilityCanvasContext _context;
    private readonly string _defaultCode = "digitalsustainabilitycanvas";
    private const string REGISTRATION_CODE_KEY = "RegistrationCode";

    public RegistrationCodeService(SustainabilityCanvasContext context)
    {
        _context = context;
    }

    public async Task<string> GetCurrentCodeAsync()
    {
        try
        {
            var setting = await _context.AppSettings
                .FirstOrDefaultAsync(s => s.Key == REGISTRATION_CODE_KEY);
            
            return setting?.Value ?? _defaultCode;
        }
        catch
        {
            return _defaultCode;
        }
    }

    public async Task SetCodeAsync(string newCode)
    {
        try
        {
            var setting = await _context.AppSettings
                .FirstOrDefaultAsync(s => s.Key == REGISTRATION_CODE_KEY);

            if (setting != null)
            {
                setting.Value = newCode;
                setting.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                setting = new AppSetting
                {
                    Key = REGISTRATION_CODE_KEY,
                    Value = newCode,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.AppSettings.Add(setting);
            }

            await _context.SaveChangesAsync();
        }
        catch
        {
            // If database operation fails, the operation fails gracefully
            throw;
        }
    }

    public async Task<bool> IsValidCodeAsync(string code)
    {
        var currentCode = await GetCurrentCodeAsync();
        return !string.IsNullOrEmpty(code) && code.Equals(currentCode, StringComparison.OrdinalIgnoreCase);
    }
}