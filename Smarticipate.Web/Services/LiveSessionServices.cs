using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace Smarticipate.Web.Services;

public class LiveSessionServices(IConfiguration configuration) : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private string? _role; // teacher or student?
    private bool _disposed;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
    public string? ConnectionId => _hubConnection?.ConnectionId;
    public string? CurrentSessionCode { get; private set; }

    public event Action<int, int, int>? OnQuestionStarted;
    public event Action? OnQuestionStopped;
    public event Action? OnSessionEnded;
    public event Action<int>? OnTimerUpdated;
    public event Action? OnTeacherDisconnected;
    public event Action<int>? OnStudentCountChanged;
    public event Action<int[], int[], int>? OnFeedbackUpdated;
    public event Action<int, string, DateTime>? OnQuestionReceived;
    public event Action<int>? OnQuestionDismissed;
    public event Action? OnFeedbackReset;
    public event Func<Task>? OnReconnected;

    public async Task InitializeConnection()
    {
        if (_hubConnection is null)
        {
            // Hub URL is derived from the configured API base ("ApiUrl"); falls back to the
            // local dev API so nothing changes locally.
            // TODO: set "ApiUrl" in production config before deploy — the hardcoded fallback
            // below is local-dev only.
            var apiBase = configuration["ApiUrl"] ?? "https://localhost:44397/";
            var hubUrl = new Uri(new Uri(apiBase), "sessionHub");

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                    options.SkipNegotiation = true;
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.ServerTimeout = TimeSpan.FromSeconds(15);
            _hubConnection.KeepAliveInterval = TimeSpan.FromSeconds(5);

            _hubConnection.On("TeacherDisconnected", () => OnTeacherDisconnected?.Invoke());
            _hubConnection.On<int>("StudentCountChanged", (count) => OnStudentCountChanged?.Invoke(count));
            _hubConnection.On<int, int, int>("QuestionStarted",
                (questionId, duration, remainingTime) => OnQuestionStarted?.Invoke(questionId, duration, remainingTime));
            _hubConnection.On("QuestionStopped", () => OnQuestionStopped?.Invoke());
            _hubConnection.On<int>("TimerUpdated", (remainingTime) => OnTimerUpdated?.Invoke(remainingTime));
            _hubConnection.On("SessionEnded", () => OnSessionEnded?.Invoke());
            _hubConnection.On<int[], int[], int>("FeedbackUpdated",
                (pace, und, count) => OnFeedbackUpdated?.Invoke(pace, und, count));
            _hubConnection.On<int, string, DateTime>("QuestionReceived",
                (id, text, createdAt) => OnQuestionReceived?.Invoke(id, text, createdAt));
            _hubConnection.On<int>("QuestionDismissed", (id) => OnQuestionDismissed?.Invoke(id));
            _hubConnection.On("FeedbackReset", () => OnFeedbackReset?.Invoke());

            _hubConnection.Reconnected += async (_) => await ReestablishAsync();

            _hubConnection.Closed += async (error) =>
            {
                Console.WriteLine($"Connection closed: {error?.Message}");
                if (_disposed) return;
                try
                {
                    await Task.Delay(Random.Shared.Next(1, 5) * 1000);
                    await _hubConnection!.StartAsync();
                    // A manual StartAsync creates a brand-new connection, so Reconnected won't
                    // fire. Re-establish here so the user isn't left un-registered.
                    await ReestablishAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Reconnect after close failed: {ex.Message}");
                }
            };

            if (_hubConnection.State == HubConnectionState.Disconnected)
                await _hubConnection.StartAsync();
        }
        else if (_hubConnection.State == HubConnectionState.Disconnected)
        {
            await _hubConnection.StartAsync();
        }
    }

    // Ensures the hub is connected, starting it if needed, and waits (briefly) for the
    // Connected state instead of relying on fixed delays.
    public async Task<bool> EnsureConnectedAsync(TimeSpan? timeout = null)
    {
        if (_hubConnection is null)
            await InitializeConnection();

        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (!IsConnected && DateTime.UtcNow < deadline)
        {
            if (_hubConnection!.State == HubConnectionState.Disconnected)
            {
                try { await _hubConnection.StartAsync(); }
                catch { await Task.Delay(100); }
            }
            else
            {
                await Task.Delay(50); // Connecting / Reconnecting — give it a moment
            }
        }

        return IsConnected;
    }

    public async Task JoinSession(string sessionCode)
    {
        if (await EnsureConnectedAsync())
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

    public async Task EndSession()
    {
        if (IsConnected && !string.IsNullOrEmpty(CurrentSessionCode))
        {
            await _hubConnection!.InvokeAsync("EndSession", CurrentSessionCode);
        }
    }

    public async Task RegisterAsTeacher(string sessionCode)
    {
        if (await EnsureConnectedAsync())
        {
            await _hubConnection!.InvokeAsync("RegisterAsTeacher", sessionCode);
            CurrentSessionCode = sessionCode;
            _role = "teacher";
        }
    }

    public async Task RegisterAsStudent(string sessionCode)
    {
        try
        {
            if (await EnsureConnectedAsync())
            {
                await _hubConnection!.InvokeAsync("RegisterAsStudent", sessionCode);
                CurrentSessionCode = sessionCode;
                _role = "student";
            }
            else
            {
                Console.WriteLine("RegisterAsStudent: connection not established");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RegisterAsStudent error: {ex.Message}");
        }
    }

    public async Task LeaveAsStudent(string sessionCode)
    {
        if (IsConnected && !string.IsNullOrEmpty(sessionCode))
        {
            await _hubConnection!.InvokeAsync("LeaveAsStudent", sessionCode);
        }
    }

    public async Task StartQuestion(int questionId, int duration)
    {
        if (IsConnected && !string.IsNullOrEmpty(CurrentSessionCode))
        {
            await _hubConnection!.InvokeAsync("StartQuestion", CurrentSessionCode, questionId, duration);
        }
    }

    public async Task StopQuestion()
    {
        if (IsConnected && !string.IsNullOrEmpty(CurrentSessionCode))
        {
            await _hubConnection!.InvokeAsync("StopQuestion", CurrentSessionCode);
        }
    }


    public async Task UpdateRemainingTime(int remainingTime)
    {
        if (IsConnected && !string.IsNullOrEmpty(CurrentSessionCode))
        {
            await _hubConnection!.InvokeAsync("UpdateRemainingTime", CurrentSessionCode, remainingTime);
        }
    }

    public async Task UpdateFeedback(string sessionCode, int pace, int understanding)
    {
        if (IsConnected)
        {
            await _hubConnection!.InvokeAsync("UpdateFeedback", sessionCode, pace, understanding);
        }
    }

    public async Task ResetFeedback(string sessionCode)
    {
        if (IsConnected)
        {
            await _hubConnection!.InvokeAsync("ResetFeedback", sessionCode);
        }
    }

    public async Task SubmitQuestion(string sessionCode, string text)
    {
        if (IsConnected)
        {
            await _hubConnection!.InvokeAsync("SubmitQuestion", sessionCode, text);
        }
    }

    public async Task DismissQuestion(string sessionCode, int questionId)
    {
        if (IsConnected)
        {
            await _hubConnection!.InvokeAsync("DismissQuestion", sessionCode, questionId);
        }
    }

    public async Task ClearQuestions(string sessionCode)
    {
        if (IsConnected)
        {
            await _hubConnection!.InvokeAsync("ClearQuestions", sessionCode);
        }
    }

    public async Task RequestTeacherState(string sessionCode)
    {
        if (IsConnected)
        {
            await _hubConnection!.InvokeAsync("RequestTeacherState", sessionCode);
        }
    }

    // Re-join the group and re-register after a reconnect (groups don't survive it).
    private async Task ReestablishAsync()
    {
        if (!IsConnected || string.IsNullOrEmpty(CurrentSessionCode)) return;

        await _hubConnection!.InvokeAsync("JoinSession", CurrentSessionCode);
        if (_role == "teacher")
            await _hubConnection.InvokeAsync("RegisterAsTeacher", CurrentSessionCode);
        else if (_role == "student")
            await _hubConnection.InvokeAsync("RegisterAsStudent", CurrentSessionCode);

        if (OnReconnected is not null)
            await OnReconnected.Invoke(); // overlays re-pull state (teacher) / re-sync
    }

    public async Task SendHeartbeat()
    {
        if (!IsConnected) return;
        try
        {
            await _hubConnection!.InvokeAsync("SendHeartbeat");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending heartbeat: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_hubConnection is not null)
        {
            await LeaveSession();
            await _hubConnection.DisposeAsync();
        }
    }
}