namespace AHS.BlazorShared.Services;

/// <summary>
/// Launches the modal barcode scanner page and returns the scanned value,
/// or null if the user cancelled or no camera permission was granted.
/// Implemented in AHS.Maui (BarcodeScannerService) because the implementation
/// touches MAUI navigation and permission APIs.
/// </summary>
public interface IBarcodeScannerService
{
    Task<string?> ScanAsync();
}
