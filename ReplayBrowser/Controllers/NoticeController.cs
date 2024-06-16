using Microsoft.AspNetCore.Mvc;
using ReplayBrowser.Data;
using ReplayBrowser.Data.Models;
using ReplayBrowser.Helpers;
using ReplayBrowser.Services;

namespace ReplayBrowser.Controllers;

/// <summary>
/// The controller used for managing notices. Pretty much everything here is admin-only.
/// </summary>
[Controller]
[Route("api/Notices/")]
public class NoticeController : Controller
{
    private readonly NoticeHelper _noticeHelper;
    private readonly AccountService _accountService;
    
    public NoticeController(NoticeHelper noticeHelper, AccountService accountService)
    {
        _noticeHelper = noticeHelper;
        _accountService = accountService;
    }
    
    [HttpDelete]
    [Route("DeleteNotice/{id}")]
    public Task<IActionResult> DeleteNotice(int id)
    {
        if (!_accountService.IsAdmin(User))
        {
            return Task.FromResult<IActionResult>(Unauthorized());
        }
        
        _noticeHelper.DeleteNotice(id);
        
        return Task.FromResult<IActionResult>(Ok());
    }
    
    [HttpPatch]
    [Route("UpdateNotice")]
    public Task<IActionResult> UpdateNotice([FromBody] Notice notice)
    {
        if (!_accountService.IsAdmin(User))
        {
            return Task.FromResult<IActionResult>(Unauthorized());
        }
        
        notice.StartDate = notice.StartDate.ToUniversalTime();
        notice.EndDate = notice.EndDate.ToUniversalTime();
        
        _noticeHelper.UpdateNotice((int)notice.Id!, notice.Title, notice.Message, notice.StartDate, notice.EndDate);
        
        return Task.FromResult<IActionResult>(Ok());
    }
    
    [HttpPost]
    [Route("CreateNotice")]
    public Task<IActionResult> CreateNotice([FromBody] Notice notice)
    {
        if (!_accountService.IsAdmin(User))
        {
            return Task.FromResult<IActionResult>(Unauthorized());
        }
        
        notice.StartDate = notice.StartDate.ToUniversalTime();
        notice.EndDate = notice.EndDate.ToUniversalTime();
        
        _noticeHelper.CreateNotice(notice.Title, notice.Message, notice.StartDate, notice.EndDate);
        
        return Task.FromResult<IActionResult>(Ok());
    }
}