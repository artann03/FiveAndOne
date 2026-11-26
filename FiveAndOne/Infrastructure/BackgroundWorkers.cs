using FiveAndOne.Application.Services.Payments;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Application.Services.Subscriptions;

namespace FiveAndOne.API.Infrastructure;

/// <summary>Runs a job on a fixed interval in its own DI scope. One failing run never stops the worker.</summary>
public abstract class PeriodicWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger _logger;

    protected PeriodicWorker(IServiceScopeFactory scopes, ILogger logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected abstract TimeSpan Interval { get; }
    protected virtual TimeSpan InitialDelay => TimeSpan.FromSeconds(10);
    protected abstract Task RunAsync(IServiceProvider services, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await RunAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Worker} run failed", GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}

/// <summary>Sends queued emails/SMS from the outbox.</summary>
public sealed class NotificationWorker : PeriodicWorker
{
    public NotificationWorker(IServiceScopeFactory scopes, ILogger<NotificationWorker> logger) : base(scopes, logger) { }

    protected override TimeSpan Interval => TimeSpan.FromSeconds(15);

    protected override async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var notifications = services.GetRequiredService<INotificationService>();
        while (await notifications.DispatchPendingAsync(50, cancellationToken) == 50)
        {
            // keep going while full batches come back
        }
    }
}

/// <summary>Releases slots of reservations whose online payment window ran out (post-MVP payments).</summary>
public sealed class ReservationExpiryWorker : PeriodicWorker
{
    public ReservationExpiryWorker(IServiceScopeFactory scopes, ILogger<ReservationExpiryWorker> logger) : base(scopes, logger) { }

    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

    protected override Task RunAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<IReservationPaymentService>().ExpireUnpaidAsync(cancellationToken);
}

/// <summary>Subscription renewals, grace period and expiry (hourly), and data retention (stories 44-46).</summary>
public sealed class SubscriptionWorker : PeriodicWorker
{
    public SubscriptionWorker(IServiceScopeFactory scopes, ILogger<SubscriptionWorker> logger) : base(scopes, logger) { }

    protected override TimeSpan Interval => TimeSpan.FromHours(1);
    protected override TimeSpan InitialDelay => TimeSpan.FromSeconds(30);

    protected override async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var subscriptions = services.GetRequiredService<ISubscriptionService>();
        await subscriptions.ProcessRenewalsAsync(cancellationToken);
        await subscriptions.ProcessRetentionAsync(cancellationToken);
    }
}
