using YourApp.Pages;

namespace YourApp.Services;

public sealed class BarcodeScannerService : IBarcodeScannerService
{
    // Prevent two rapid taps on the scan button from stacking two modal
    // scanner pages on top of each other.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<string?> ScanAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted) return null;

            // .NET 9 / MAUI 9 form. If you're on .NET 8, change this line to:
            //     var rootPage = Application.Current?.MainPage;
            var rootPage = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (rootPage is null) return null;

            var page = new BarcodeScannerPage();

            await MainThread.InvokeOnMainThreadAsync(() =>
                rootPage.Navigation.PushModalAsync(page));

            return await page.WaitForResultAsync();
        }
        finally
        {
            _gate.Release();
        }
    }
}
