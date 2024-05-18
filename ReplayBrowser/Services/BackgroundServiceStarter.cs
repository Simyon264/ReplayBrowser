namespace ReplayBrowser.Services;

public class BackgroundServiceStarter<T> : IHostedService where T:IHostedService
{
    readonly T backgroundService;

    public BackgroundServiceStarter(T backgroundService)
    {
        this.backgroundService = backgroundService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return backgroundService.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return backgroundService.StopAsync(cancellationToken);
    }
}