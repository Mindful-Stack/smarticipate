using Microsoft.AspNetCore.SignalR;

namespace Smarticipate.API.Hubs;

public class SessionHub : Hub
{
    private static readonly Dictionary<string, HashSet<string>> _teacherConnections = new();
    
    public async Task RegisterAsTeacher(string sessionCode)
    {
        // Add this connection to the teachers group for this session
        await Groups.AddToGroupAsync(Context.ConnectionId, $"teacher-{sessionCode}");
        
        // Track this connection as a teacher for this session
        lock (_teacherConnections)
        {
            if (!_teacherConnections.ContainsKey(sessionCode))
                _teacherConnections[sessionCode] = new HashSet<string>();
                
            _teacherConnections[sessionCode].Add(Context.ConnectionId);
        }
        
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionCode);
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Check if this was a teacher connection
        string? teacherSession = null;
        
        lock (_teacherConnections)
        {
            foreach (var session in _teacherConnections)
            {
                if (session.Value.Contains(Context.ConnectionId))
                {
                    teacherSession = session.Key;
                    session.Value.Remove(Context.ConnectionId);
                    
                    // If this was the last teacher connection for this session
                    if (session.Value.Count == 0)
                    {
                        _teacherConnections.Remove(teacherSession);
                        break;
                    }
                }
            }
        }
        
        // If this was the last teacher for a session, notify students
        if (teacherSession != null && !_teacherConnections.ContainsKey(teacherSession))
        {
            await Clients.Group(teacherSession).SendAsync("TeacherDisconnected");
        }
        
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinSession(string sessionCode)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionCode);
    }

    public async Task LeaveSession(string sessionCode)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionCode);
    }

    public async Task StartQuestion(string sessionCode, int questionId, int duration)
    {
        await Clients.Group(sessionCode).SendAsync("QuestionStarted", questionId, duration, duration);
    }

    public async Task StopQuestion(string sessionCode)
    {
        await Clients.Group(sessionCode).SendAsync("QuestionStopped");
    }

    public async Task UpdateRemainingTime(string sessionCode, int remainingTime)
    {
        await Clients.Group(sessionCode).SendAsync("TimerUpdated", remainingTime);
    }

    public async Task EndSession(string sessionCode)
    {
        await Clients.Group(sessionCode).SendAsync("SessionEnded");
    }
    
    public async Task SendHeartbeat()
    {
        await Clients.Caller.SendAsync("Debug", "Heartbeat received");
    }
    }