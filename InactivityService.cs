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

    public event Action OnLogout;
    public event Action OnWarning;

    public InactivityService(ILogger<InactivityService> logger)
    {
        _logger = logger;
        _lastActivityTime = DateTime.Now;

        _warningTimer = new Timer((_inactivityTime - _warningTime).TotalMilliseconds);
        _warningTimer.Elapsed += WarningTimerElapsed;

        _inactivityTimer = new Timer(_inactivityTime.TotalMilliseconds);
        _inactivityTimer.Elapsed += InactivityTimerElapsed;
    }

    public void ResetTimer()
    {
        _lastActivityTime = DateTime.Now;
        _warningTimer.Stop();
        _inactivityTimer.Stop();
        _warningTimer.Start();
        _inactivityTimer.Start();
        _logger.LogInformation("Inactivity timer reset.");
    }

    private void WarningTimerElapsed(object sender, ElapsedEventArgs e)
    {
        _warningTimer.Stop();
        OnWarning?.Invoke();
        _logger.LogInformation("Warning timer elapsed.");
    }

    private void InactivityTimerElapsed(object sender, ElapsedEventArgs e)
    {
        _inactivityTimer.Stop();
        OnLogout?.Invoke();
        _logger.LogInformation("Inactivity timer elapsed.");
    }
}
