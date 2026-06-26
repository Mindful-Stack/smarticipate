using System.Dynamic;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;

namespace Smarticipate.Web.Services;

public class LiveSessionServices(NavigationManager navigationManager) : IAsyncDisposable
{
    public HubConnection? _hubConnection { get; private set; }
    private string? _role; // teacher or student?

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
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
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(navigationManager.ToAbsoluteUri("https://localhost:44397/sessionHub"), options =>
                {
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                    options.SkipNegotiation = true;
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.ServerTimeout = TimeSpan.FromSeconds(15);
            _hubConnection.KeepAliveInterval = TimeSpan.FromSeconds(5);

            //debug handler
            _hubConnection.On<string>("Debug", (message) => { Console.WriteLine($"SignalR Debug: {message}"); });

            _hubConnection.On("TeacherDisconnected", () =>
            {
                Console.WriteLine("Teacher disconnected unexpectedly");
                OnTeacherDisconnected?.Invoke();
            });

            _hubConnection.On<int>("StudentCountChanged", (count) =>
            {
                Console.WriteLine($"LiveSessionServices received StudentCountChanged: {count}");
                OnStudentCountChanged?.Invoke(count);
            });

            _hubConnection.On<int, int, int>("QuestionStarted", (questionId, duration, remainingTime) =>
                {
                    Console.WriteLine(
                        $"RECEIVED QuestionStarted event: questionId={questionId}, duration={duration}, remainingTime={remainingTime}");
                    OnQuestionStarted?.Invoke(questionId, duration, remainingTime);
                }
            );

            _hubConnection.On("QuestionStopped", () => { OnQuestionStopped?.Invoke(); });

            _hubConnection.On<int>("TimerUpdated", (remainingTime) => { OnTimerUpdated?.Invoke(remainingTime); });

            _hubConnection.On("SessionEnded", () => { OnSessionEnded?.Invoke(); });

            _hubConnection.On<int[], int[], int>("FeedbackUpdated",
                (pace, und, count) => OnFeedbackUpdated?.Invoke(pace, und, count));

            _hubConnection.On<int, string, DateTime>("QuestionReceived",
                (id, text, createdAt) => OnQuestionReceived?.Invoke(id, text, createdAt));

            _hubConnection.On<int>("QuestionDismissed", (id) => OnQuestionDismissed?.Invoke(id));

            _hubConnection.On("FeedbackReset", () => OnFeedbackReset?.Invoke());

            //reconnection event handling
            _hubConnection.Reconnected += async (connectionId) =>
            {
                Console.WriteLine($"Reconnected with new id {connectionId}; re-establishing session state");
                await ReestablishAsync();
            };

            _hubConnection.Closed += async (error) =>
            {
                Console.WriteLine($"Connection closed: {error?.Message}");
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await _hubConnection.StartAsync();
                // A manual StartAsync creates a brand-new connection, so Reconnected won't
                // fire. Re-establish here so the user isn't left un-registered.
                await ReestablishAsync();
            };

            // Only start if not already connected
            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                await _hubConnection.StartAsync();
                Console.WriteLine($"Connection started with ID: {_hubConnection.ConnectionId}");
            }
        }
        else if (_hubConnection.State == HubConnectionState.Disconnected)
        {
            // Try to start the connection if it exists but is disconnected
            await _hubConnection.StartAsync();
            Console.WriteLine($"Connection restarted with ID: {_hubConnection.ConnectionId}");
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

    public async Task EndSession()
    {
        if (IsConnected && !string.IsNullOrEmpty(CurrentSessionCode))
        {
            await _hubConnection!.InvokeAsync("EndSession", CurrentSessionCode);
        }
    }

    public async Task RegisterAsTeacher(string sessionCode)
    {
        if (_hubConnection is null)
        {
            await InitializeConnection();
        }

        if (IsConnected)
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
            if (_hubConnection is null)
            {
                await InitializeConnection();
                await Task.Delay(500); // Additional delay to ensure connection is ready
            }

            if (IsConnected)
            {
                await _hubConnection!.InvokeAsync("RegisterAsStudent", sessionCode);
                CurrentSessionCode = sessionCode;
                _role = "student";
                Console.WriteLine("LiveSessionServices.RegisterAsStudent: Successfully registered");
            }
            else
            {
                Console.WriteLine("LiveSessionServices.RegisterAsStudent: Failed - Connection not established");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LiveSessionServices.RegisterAsStudent: ERROR - {ex.Message}");
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

    public async Task RequestTeacherState(string sessionCode, bool allowSnapshotFallback)
    {
        if (IsConnected)
        {
            await _hubConnection!.InvokeAsync("RequestTeacherState", sessionCode, allowSnapshotFallback);
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
        if (IsConnected)
        {
            try
            {
                await _hubConnection!.InvokeAsync("SendHeartbeat");
                Console.WriteLine("Heartbeat sent");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending heartbeat: {ex.Message}");
                throw;
            }
        }
        else
        {
            throw new InvalidOperationException("Connection is not active");
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