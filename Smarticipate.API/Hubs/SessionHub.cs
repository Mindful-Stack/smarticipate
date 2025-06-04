﻿using Microsoft.AspNetCore.SignalR;

namespace Smarticipate.API.Hubs;

public class SessionHub : Hub
{
    private static readonly Dictionary<string, HashSet<string>> _teacherConnections = new();
    private static readonly Dictionary<string, HashSet<string>> _studentConnections = new();

    private static readonly Dictionary<string, (int QuestionId, int Duration, DateTime StartTime)> _activeQuestions =
        new();

    public async Task JoinSession(string sessionCode)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionCode);
    }

    public async Task LeaveSession(string sessionCode)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionCode);
    }

    public async Task EndSession(string sessionCode)
    {
        await Clients.Group(sessionCode).SendAsync("SessionEnded");
    }

    public async Task RegisterAsTeacher(string sessionCode)
    {
        var connectionId = Context.ConnectionId;

        try
        {
            // Add this connection to the teachers group for this session
            await Groups.AddToGroupAsync(connectionId, $"teacher-{sessionCode}");

            // Track this connection as a teacher for this session
            lock (_teacherConnections)
            {
                if (!_teacherConnections.ContainsKey(sessionCode))
                {
                    _teacherConnections[sessionCode] = new HashSet<string>();
                }
                _teacherConnections[sessionCode].Add(connectionId);
            }

            await Groups.AddToGroupAsync(connectionId, sessionCode);
            Console.WriteLine($"Added {connectionId} to {sessionCode} general group");

            // Send a test message to verify the connection
            await Clients.Caller.SendAsync("Debug", "Teacher registration successful");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in RegisterAsTeacher: {ex.Message}");
        }
    }

    public async Task RegisterAsStudent(string sessionCode)
    {
        var connectionId = Context.ConnectionId;

        try
        {
            await Groups.AddToGroupAsync(connectionId, $"student-{sessionCode}");
            await Groups.AddToGroupAsync(connectionId, sessionCode);

            int studentCount;
            lock (_studentConnections)
            {
                if (!_studentConnections.ContainsKey(sessionCode))
                {
                    _studentConnections[sessionCode] = new HashSet<string>();
                }

                _studentConnections[sessionCode].Add(connectionId);
                studentCount = _studentConnections[sessionCode].Count;
                Console.WriteLine(
                    $"Student IDs in session {sessionCode}: {string.Join(", ", _studentConnections[sessionCode])}");
            }

            await Clients.Group($"teacher-{sessionCode}").SendAsync("StudentCountChanged", studentCount);
            Console.WriteLine($"Sent StudentCountChanged({studentCount}) to teacher-{sessionCode}");

            var activeQuestion = GetActiveQuestionForSession(sessionCode); 
            if (activeQuestion.HasValue)
            {
                // Send active question details directly to the newly connected student
                await Clients.Caller.SendAsync("QuestionStarted",
                    activeQuestion.Value.QuestionId,
                    activeQuestion.Value.Duration,
                    activeQuestion.Value.RemainingTime);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in RegisterAsStudent: {ex.Message}");
        }
    }

    public async Task LeaveAsStudent(string sessionCode)
    {
        var connectionId = Context.ConnectionId;
        await Groups.RemoveFromGroupAsync(connectionId, $"student-{sessionCode}");

        await Groups.RemoveFromGroupAsync(connectionId, sessionCode);

        int studentCount = 0;

        lock (_studentConnections)
        {
            if (_studentConnections.ContainsKey(sessionCode))
            {
                _studentConnections[sessionCode].Remove(connectionId);
                studentCount = _studentConnections[sessionCode].Count;

                if (_studentConnections[sessionCode].Count == 0)
                {
                    _studentConnections.Remove(sessionCode);
                }
            }
        }

        await Clients.Group($"teacher-{sessionCode}").SendAsync("StudentCountChanged", studentCount);
    }

    public async Task StartQuestion(string sessionCode, int questionId, int duration)
    {
        lock (_activeQuestions)
        {
            _activeQuestions[sessionCode] = (questionId, duration, DateTime.Now);
        }

        await Clients.Group(sessionCode).SendAsync("QuestionStarted", questionId, duration, duration);
    }

    public async Task StopQuestion(string sessionCode)
    {
        lock (_activeQuestions)
        {
            _activeQuestions.Remove(sessionCode);
        }

        await Clients.Group(sessionCode).SendAsync("QuestionStopped");
    }
    
    private (int QuestionId, int Duration, int RemainingTime)? GetActiveQuestionForSession(string sessionCode)
    {
        lock (_activeQuestions)
        {
            if (_activeQuestions.TryGetValue(sessionCode, out var questionInfo))
            {
                //Check time passed since question started
                var elapsed = (int)(DateTime.Now - questionInfo.StartTime).TotalSeconds;
                var remaining = Math.Max(0, questionInfo.Duration - elapsed);
            
                return (questionInfo.QuestionId, questionInfo.Duration, remaining);
            }
        }
    
        return null;
    }

    public async Task UpdateRemainingTime(string sessionCode, int remainingTime)
    {
        await Clients.Group(sessionCode).SendAsync("TimerUpdated", remainingTime);
    }

    public async Task SendHeartbeat()
    {
        await Clients.Caller.SendAsync("Debug", "Heartbeat received");
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

        string? studentSession = null;
        int studentCount = 0;

        lock (_studentConnections)
        {
            foreach (var session in _studentConnections)
            {
                if (session.Value.Contains(Context.ConnectionId))
                {
                    studentSession = session.Key;
                    session.Value.Remove(Context.ConnectionId);
                    studentCount = session.Value.Count;

                    if (session.Value.Count == 0)
                    {
                        _studentConnections.Remove(studentSession);
                    }

                    break;
                }
            }
        }

        if (studentSession is not null)
        {
            await Clients.Group($"teacher-{studentSession}").SendAsync("StudentCountChanged", studentCount);
        }

        Console.WriteLine($"Student left session {studentSession}, new count: {studentCount}");

        await base.OnDisconnectedAsync(exception);
    }
}
