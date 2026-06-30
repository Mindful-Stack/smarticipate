using Smarticipate.API.Data.Identity;

namespace Smarticipate.API.Services;

// Writes an aggregate snapshot for every active session every 10 minutes
public class FeedbackSnapshotService(
    IServiceScopeFactory scopeFactory,
    LiveFeedbackStore store,
    ILogger<FeedbackSnapshotService> logger
    ) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    foreach (var sessionCode in store.ActiveSessions)
                    {
                        using var scope = scopeFactory.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
                        await FeedbackSnapshotWriter.WriteAsync(db, store, sessionCode);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error writing periodic feedback snapshots");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}