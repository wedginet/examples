# Barcode Scanner Scaffold — AHS Solution Layout

Drop-in scaffold for adding camera-based barcode scanning to your MAUI Blazor
Hybrid app, using the **BarcodeScanning.Native.Maui** NuGet package
(Google ML Kit on Android, Apple VisionKit on iOS).

Target: .NET 10 / MAUI 10.

## How the architecture maps

| Concern | Project | Why |
|---|---|---|
| Interface (`IBarcodeScannerService`) | **AHS.BlazorShared** | Razor components inject it; no MAUI types involved. |
| Implementation (`BarcodeScannerService`) | **AHS.Maui** | Touches MAUI navigation + permissions; only AHS.Maui can. |
| XAML scanner page | **AHS.Maui** | XAML is MAUI-only. |
| DI registration | **AHS.Maui / MauiProgram.cs** | Composition root. |
| Razor button + injection | **AHS.BlazorShared** | Where your keyboard component already lives. |

This mirrors the pattern you already use for the "couple of interfaces for
classes that live in AHS.Maui."

`AHS.Application`, `AHS.Infrastructure`, and `AHS.Core` are **not touched**.
Barcode scanning is a UI/platform concern, not a domain or repository concern.

## File placement (zip mirrors these paths)

The folders inside this zip mirror your project structure. Copy each file
into the matching project at the same relative path.

```
AHS.BlazorShared/
  └─ Services/
      └─ IBarcodeScannerService.cs            ← copy here
  └─ Components/
      └─ ScanButtonExample.razor              ← reference only; copy
                                                  the snippet into your
                                                  existing keyboard Razor

AHS.Maui/
  └─ Services/
      └─ BarcodeScannerService.cs             ← copy here
  └─ Pages/
      └─ BarcodeScannerPage.xaml              ← copy here
      └─ BarcodeScannerPage.xaml.cs           ← copy here
  └─ Platforms/
      └─ Android/
          └─ AndroidManifest.xml              ← MERGE additions
      └─ iOS/
          └─ Info.plist                        ← MERGE additions
  └─ MauiProgram.cs                            ← MERGE additions
```

The `_Snippets/` folder contains the merge-only files (manifest, plist,
MauiProgram). Don't copy those folders verbatim — open your existing files
and paste the marked sections in.

## Install NuGet

Run this once **in the AHS.Maui project only**:

```
dotnet add AHS.Maui package BarcodeScanning.Native.Maui
```

## Optional features — easy to remove

Two optional features are wrapped in matching block markers in
`BarcodeScannerPage.xaml` and `BarcodeScannerPage.xaml.cs`:

```
===== OPTIONAL FEATURE: TORCH TOGGLE — START =====
... 
===== OPTIONAL FEATURE: TORCH TOGGLE — END =====

===== OPTIONAL FEATURE: CAMERA FLIP — START =====
... 
===== OPTIONAL FEATURE: CAMERA FLIP — END =====
```

To remove either feature, delete the block in **both** files. Nothing
else changes.

## Build steps

1. Extract the zip and copy files to the paths shown above.
2. Install the NuGet package in AHS.Maui.
3. Merge the three `_Snippets/` files into your existing MauiProgram.cs,
   AndroidManifest.xml, and Info.plist.
4. Add the inject + button + handler from `ScanButtonExample.razor` into
   your existing keyboard Razor component in AHS.BlazorShared.
5. **Clean and Rebuild** the solution (per your standing rule — extracted
   zips need a full rebuild, not just a build).

## Notes

- Property names in `BarcodeScanning.Native.Maui` occasionally shift
  between major versions (`OnDetectionFinishedEventArg`, `TorchOn`,
  `CameraFacing`). If the version you install renames any of these, the
  fix is one line — the rest of the scaffold is stable.
- The page uses an `Interlocked` guard so rapid duplicate detections from
  the camera resolve the result exactly once.
- The service uses a `SemaphoreSlim` so two rapid taps on the Scan button
  can't stack two modal scanner pages on top of each other.
