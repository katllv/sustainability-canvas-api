using Microsoft.EntityFrameworkCore;
using SustainabilityCanvas.Api.Data;
using SustainabilityCanvas.Api.Models;

namespace SustainabilityCanvas.Api.Services;

public class MasterPasswordService
{
    private readonly SustainabilityCanvasContext _context;
    private readonly string _defaultPassword = "adminmaster2025";
    private const string MASTER_PASSWORD_KEY = "MasterPassword";

    public MasterPasswordService(SustainabilityCanvasContext context)
    {
        _context = context;
    }

    public async Task<string> GetCurrentPasswordAsync()
    {
        try
        {
            var setting = await _context.AppSettings
                .FirstOrDefaultAsync(s => s.Key == MASTER_PASSWORD_KEY);
            
            return setting?.Value ?? _defaultPassword;
        }
        catch
        {
            return _defaultPassword;
        }
    }

    public async Task SetPasswordAsync(string newPassword)
    {
        try
        {
            var setting = await _context.AppSettings
                .FirstOrDefaultAsync(s => s.Key == MASTER_PASSWORD_KEY);

            if (setting != null)
            {
                setting.Value = newPassword;
                setting.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                setting = new AppSetting
                {
                    Key = MASTER_PASSWORD_KEY,
                    Value = newPassword,
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

    public async Task<bool> IsValidPasswordAsync(string password)
    {
        var currentPassword = await GetCurrentPasswordAsync();
        return !string.IsNullOrEmpty(password) && password.Equals(currentPassword, StringComparison.Ordinal);
    }
}