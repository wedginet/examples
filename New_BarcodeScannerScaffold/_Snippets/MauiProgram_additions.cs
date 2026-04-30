// =============================================================================
// AHS.Maui / MauiProgram.cs — ADDITIONS ONLY
//
// Merge these lines into your existing MauiProgram.cs in the AHS.Maui project.
// Do NOT replace your file with this one.
// =============================================================================

// 1. ADD these usings at the top of MauiProgram.cs:

using BarcodeScanning;
using AHS.BlazorShared.Services;
using AHS.Maui.Services;
using AHS.Maui.Pages;


// 2. In your CreateMauiApp() builder chain, add the .UseBarcodeScanning() call.
//    Your existing chain probably looks like:
//
//       builder
//           .UseMauiApp<App>()
//           .UseMauiBlazorWebView()
//           ...
//
//    Add the marked line:

//        .UseBarcodeScanning();   // <-- add this


// 3. After your existing service registrations, add:

//    builder.Services.AddSingleton<IBarcodeScannerService, BarcodeScannerService>();
//    builder.Services.AddTransient<BarcodeScannerPage>();


// =============================================================================
// Full example for clarity (yours will have more lines):
// =============================================================================
/*
public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    builder
        .UseMauiApp<App>()
        .UseMauiBlazorWebView()
        .UseBarcodeScanning();                  // <-- ADD

    builder.Services.AddMauiBlazorWebView();

    // ----- Barcode scanner registrations (ADD) -----
    builder.Services.AddSingleton<IBarcodeScannerService, BarcodeScannerService>();
    builder.Services.AddTransient<BarcodeScannerPage>();
    // -----------------------------------------------

    // ... your other AHS service registrations ...

    return builder.Build();
}
*/
