# Barcode Scanner Scaffold for MAUI Blazor Hybrid

Drop-in scaffold for adding camera-based barcode scanning to a MAUI Blazor
Hybrid app (Android + iOS) using the **BarcodeScanning.Native.Maui** NuGet
package (uses Google ML Kit on Android, Apple VisionKit on iOS).

Replace `YourApp` in every namespace with your project's root namespace.

## Install

```
dotnet add package BarcodeScanning.Native.Maui
```

## Files in this scaffold

| File | Where it goes |
|------|---------------|
| `Snippets/MauiProgram_additions.cs` | Merge into your existing `MauiProgram.cs` |
| `Snippets/AndroidManifest_additions.xml` | Merge into `Platforms/Android/AndroidManifest.xml` |
| `Snippets/Info_plist_additions.xml` | Merge into `Platforms/iOS/Info.plist` |
| `Pages/BarcodeScannerPage.xaml` | Drop into a `Pages` folder in your MAUI project |
| `Pages/BarcodeScannerPage.xaml.cs` | Same folder as the XAML |
| `Services/IBarcodeScannerService.cs` | Drop into a `Services` folder |
| `Services/BarcodeScannerService.cs` | Same folder as the interface |
| `Components/ScanButtonExample.razor` | Reference example for wiring a Razor button |

## Optional features — easy to remove

Two optional features are included and clearly marked with block comments
in both `BarcodeScannerPage.xaml` and `BarcodeScannerPage.xaml.cs`:

```
// ===== OPTIONAL FEATURE: TORCH TOGGLE — START =====
... code ...
// ===== OPTIONAL FEATURE: TORCH TOGGLE — END =====

// ===== OPTIONAL FEATURE: CAMERA FLIP — START =====
... code ...
// ===== OPTIONAL FEATURE: CAMERA FLIP — END =====
```

To remove either feature, delete everything between the matching START
and END markers in **both** the XAML and code-behind files. The scanner
will continue to work without changes elsewhere.

To keep both features, leave the files as-is.

## Notes

- The scaffold targets **.NET 9 / MAUI 9** (uses `Application.Current.Windows[0].Page`).
  If you're on .NET 8, change that line to `Application.Current?.MainPage` in
  `BarcodeScannerService.cs` (it's flagged in a comment).
- The scanner page resolves exactly once per scan via an `Interlocked` guard,
  so rapid duplicate detections from the camera won't cause double-pop or
  double-resolve issues.
- The service uses a `SemaphoreSlim` to prevent two rapid taps on the scan
  button from stacking two modal scanner pages.
- Property names in `BarcodeScanning.Native.Maui` occasionally shift between
  major versions. If you upgrade and see a build error on `OnDetectionFinished`
  or `TorchOn`, check the package README for the renamed equivalent —
  the rest of the scaffold is stable.
