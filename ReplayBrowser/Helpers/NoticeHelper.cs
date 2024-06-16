using ReplayBrowser.Data;
using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Helpers;

/// <summary>
/// Helper class for notices. Handles the logic for displaying notices as well as the logic for creating and deleting notices.
/// </summary>
public class NoticeHelper
{
    private IServiceScopeFactory _factory;
    
    public NoticeHelper(IServiceScopeFactory factory)
    {
        _factory = factory;
    }
    
    public async void CreateNotice(string title, string message, DateTime startDate, DateTime endDate)
    {
        using var scope = _factory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        
        var notice = new Notice
        {
            Title = title,
            Message = message,
            StartDate = startDate,
            EndDate = endDate
        };
        
        await dbContext.Notices.AddAsync(notice);
        await dbContext.SaveChangesAsync();
    }
    
    public async void DeleteNotice(int id)
    {
        using var scope = _factory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        
        var notice = await dbContext.Notices.FindAsync(id);
        
        if (notice != null)
        {
            dbContext.Notices.Remove(notice);
            await dbContext.SaveChangesAsync();
        }
    }
    
    public async void UpdateNotice(int id, string title, string message, DateTime startDate, DateTime endDate)
    {
        using var scope = _factory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        
        var notice = await dbContext.Notices.FindAsync(id);
        
        if (notice != null)
        {
            notice.Title = title;
            notice.Message = message;
            notice.StartDate = startDate;
            notice.EndDate = endDate;
            
            await dbContext.SaveChangesAsync();
        }
    }

    public List<Notice> GetActiveNotices()
    {
        using var scope = _factory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        
        var now = DateTime.UtcNow;
        return dbContext.Notices.Where(n => n.StartDate <= now && n.EndDate >= now).ToList();
    }
    
    public List<Notice> GetAllNotices()
    {
        using var scope = _factory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        
        return dbContext.Notices.ToList();
    }
}