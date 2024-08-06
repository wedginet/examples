using System;
using System.Timers;
using Microsoft.Extensions.Logging;

public class InactivityService
{
    private readonly Timer _inactivityTimer;
    private readonly Timer _warningTimer;
    private readonly ILogger<InactivityService> _logger;
    private readonly TimeSpan _warningTime = TimeSpan.FromMinutes(2);
    private readonly TimeSpan _inactivityTime = TimeSpan.FromMinutes(30);
    private DateTime _lastActivityTime;

    public event Action OnInactivity;
    public event Action OnWarning;

    public InactivityService(ILogger<InactivityService> logger)
    {
        _logger = logger;
        _inactivityTimer = new Timer(_inactivityTime.TotalMilliseconds);
        _warningTimer = new Timer(_warningTime.TotalMilliseconds);
        _inactivityTimer.Elapsed += InactivityTimerElapsed;
        _warningTimer.Elapsed += WarningTimerElapsed;
    }

    public void Start()
    {
        ResetTimers();
    }

    public void Stop()
    {
        _inactivityTimer.Stop();
        _warningTimer.Stop();
    }

    public void ResetTimers()
    {
        _lastActivityTime = DateTime.Now;
        _inactivityTimer.Stop();
        _inactivityTimer.Start();
        _warningTimer.Stop();
        _warningTimer.Interval = (_inactivityTime - _warningTime).TotalMilliseconds;
        _warningTimer.Start();
        _logger.LogInformation("Timers reset at {Time}", _lastActivityTime);
    }

    private void InactivityTimerElapsed(object sender, ElapsedEventArgs e)
    {
        _logger.LogInformation("User inactive for {Time} minutes", _inactivityTime.TotalMinutes);
        OnInactivity?.Invoke();
        Stop();
    }

    private void WarningTimerElapsed(object sender, ElapsedEventArgs e)
    {
        _logger.LogInformation("Warning user of inactivity at {Time}", DateTime.Now);
        OnWarning?.Invoke();
    }
}
