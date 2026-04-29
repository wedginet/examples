namespace YourApp.Services;

/// <summary>
/// Launches the modal barcode scanner page and returns the scanned value,
/// or null if the user cancelled or no camera permission was granted.
/// </summary>
public interface IBarcodeScannerService
{
    Task<string?> ScanAsync();
}
