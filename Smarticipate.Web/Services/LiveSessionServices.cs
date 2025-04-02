using System.Dynamic;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Smarticipate.Web.Services;

public class LiveSessionServices(NavigationManager navigationManager) : IAsyncDisposable
{
    private HubConnection? _hubConnection;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
    public string? CurrentSessionCode { get; private set; }

    public event Action<int, int, int>? OnQuestionStarted;
    public event Action? OnSessionEnded;
    public event Action<int>? OnTimerUpdated;

    public async Task InitializeConnection()
    {
        if (_hubConnection is null)
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(navigationManager.ToAbsoluteUri("/sessionHub"))
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<int, int, int>("QuestionStarted", (questionId, duration, remainingTime) =>
                {
                    OnQuestionStarted?.Invoke(questionId, duration, remainingTime);
                }
            );

            _hubConnection.On<int>("TimerUpdated", (remainingTime) =>
            {
                OnTimerUpdated?.Invoke(remainingTime);
            });

            _hubConnection.On("SessionEnded", () =>
            {
                OnSessionEnded?.Invoke();
            });

            await _hubConnection.StartAsync();
        }
    }

    public async Task JoinSession(string sessionCode)
    {
        if (_hubConnection is null)
        {
            await InitializeConnection();
        }

        if (IsConnected)
        {
            await _hubConnection!.InvokeAsync("JoinSession", sessionCode);
            CurrentSessionCode = sessionCode;
        }        
    }

    public async Task LeaveSession()
    {
        if (IsConnected && !string.IsNullOrEmpty(CurrentSessionCode))
        {
            await _hubConnection!.InvokeAsync("LeaveSession", CurrentSessionCode);
            CurrentSessionCode = null;
        }
    }

    public async Task StartQuestion(int questionId, int duration)
    {
        if (IsConnected && !string.IsNullOrEmpty(CurrentSessionCode))
        {
            await _hubConnection!.InvokeAsync("StartQuestion", CurrentSessionCode, questionId, duration);
        }
    }

    public async Task UpdateRemainingTime(int remainingTime)
    {
        if (IsConnected && !string.IsNullOrEmpty(CurrentSessionCode))
        {
            await _hubConnection!.InvokeAsync("UpdateRemainingTime", CurrentSessionCode, remainingTime);
        }
    }

    public async Task EndSession()
    {
        if (IsConnected && !string.IsNullOrEmpty(CurrentSessionCode))
        {
            await _hubConnection!.InvokeAsync("EndSession", CurrentSessionCode);
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await LeaveSession();
            await _hubConnection.DisposeAsync();
        }
    }
}