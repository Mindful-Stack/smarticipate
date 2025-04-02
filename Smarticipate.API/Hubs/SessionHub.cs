using Microsoft.AspNetCore.SignalR;

namespace Smarticipate.API.Hubs;

public class SessionHub : Hub
{
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

    public async Task UpdateRemainingTime(string sessionCode, int remainingTime)
    {
        await Clients.Group(sessionCode).SendAsync("TimerUpdated", remainingTime);
    }

    public async Task EndSession(string sessionCode)
    {
        await Clients.Group(sessionCode).SendAsync("SessionEnded");
    }
}