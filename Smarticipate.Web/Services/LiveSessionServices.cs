using System.Dynamic;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;

namespace Smarticipate.Web.Services;

public class LiveSessionServices(NavigationManager navigationManager) : IAsyncDisposable
{
    public HubConnection? _hubConnection { get; private set; }

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
    public string? CurrentSessionCode { get; private set; }

    public event Action<int, int, int>? OnQuestionStarted;
    public event Action? OnQuestionStopped;
    public event Action? OnSessionEnded;
    public event Action<int>? OnTimerUpdated;
    public event Action? OnTeacherDisconnected;
    public event Action<int>? OnStudentCountChanged;

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

            //reconnection event handling
            _hubConnection.Closed += async (error) =>
            {
                Console.WriteLine($"Connection closed: {error?.Message}");
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await _hubConnection.StartAsync();
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