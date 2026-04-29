// =============================================================================
// MauiProgram.cs — ADDITIONS ONLY
// Merge these lines into your existing MauiProgram.cs.
// =============================================================================

using BarcodeScanning;              // <-- add
using YourApp.Pages;                // <-- add (replace YourApp)
using YourApp.Services;             // <-- add (replace YourApp)

public static class MauiProgramAdditionsExample
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiBlazorWebView()
            .UseBarcodeScanning();   // <-- add this line

        builder.Services.AddMauiBlazorWebView();

        // ===== Barcode scanner registrations =====
        builder.Services.AddSingleton<IBarcodeScannerService, BarcodeScannerService>();
        builder.Services.AddTransient<BarcodeScannerPage>();
        // ==========================================

        return builder.Build();
    }
}
