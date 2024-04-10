using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Shared;

namespace Server.Api;

/// <summary>
/// Contains endpoints for data retrieval. Such as search completions, leaderboards, and more.
/// </summary>
[ApiController]
[EnableCors]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    private readonly ReplayDbContext _context;
    public static readonly Dictionary<Guid, WebSocket> ConnectedUsers = new();
    private Timer _timer;
    
    public DataController(ReplayDbContext context)
    {
        _context = context;
        _timer = new Timer(CheckInactiveConnections, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Provides a list of usernames which start with the given username.
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("username-completion")]
    public async Task<ActionResult> GetUsernameCompletion(
        [FromQuery] string username
    )
    {
        var completions = await _context.Players
            .Where(p => p.PlayerOocName.ToLower().StartsWith(username.ToLower()))
            .Select(p => p.PlayerOocName)
            .Distinct() // Remove duplicates
            .Take(10)
            .ToListAsync();

        return Ok(completions);
    }
    
    [Route("/ws")]
    public async Task Connect()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            var userId = Guid.NewGuid();
            ConnectedUsers.Add(userId, webSocket);
            Log.Information("User connected with ID {UserId}", userId);
            await Echo(webSocket, userId);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }
    
    private async Task Echo(WebSocket webSocket, Guid userId)
    {
        var buffer = new byte[1024 * 4];
        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        while (!result.CloseStatus.HasValue)
        {
            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            
            if (Encoding.UTF8.GetString(buffer).Contains("count"))
            {
                var count = ConnectedUsers.Count;
                var countBytes = Encoding.UTF8.GetBytes(count.ToString());
                await webSocket.SendAsync(new ArraySegment<byte>(countBytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }

            buffer = new byte[1024 * 4];
        }

        ConnectedUsers.Remove(userId, out _);
        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
    }
    
    private void CheckInactiveConnections(object state)
    {
        foreach (var user in ConnectedUsers)
        {
            if (user.Value.State == WebSocketState.Open) continue;
            
            ConnectedUsers.Remove(user.Key, out _);
            Log.Information("User disconnected with ID {UserId}", user.Key);
        }
    }
}