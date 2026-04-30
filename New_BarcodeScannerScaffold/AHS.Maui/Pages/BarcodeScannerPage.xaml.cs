using BarcodeScanning;

namespace AHS.Maui.Pages;

public partial class BarcodeScannerPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _tcs = new();
    private int _completed;   // Interlocked guard so we resolve only once

    public Task<string?> WaitForResultAsync() => _tcs.Task;

    public BarcodeScannerPage()
    {
        InitializeComponent();
    }

    // -------------------------------------------------------------------------
    // Detection
    // -------------------------------------------------------------------------
    private async void OnDetectionFinished(object sender, OnDetectionFinishedEventArg e)
    {
        try
        {
            var hit = e.BarcodeResults?.FirstOrDefault();
            if (hit is null) return;

            var value = !string.IsNullOrEmpty(hit.DisplayValue)
                ? hit.DisplayValue
                : hit.RawValue;

            await CompleteAsync(value);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Scanner detection error: {ex}");
            await CompleteAsync(null);
        }
    }

    // -------------------------------------------------------------------------
    // Cancel / hardware back
    // -------------------------------------------------------------------------
    private async void OnCancelClicked(object sender, EventArgs e)
    {
        try
        {
            await CompleteAsync(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cancel error: {ex}");
        }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CompleteAsync(null);
        return true;
    }

    // ===== OPTIONAL FEATURE: TORCH TOGGLE — START =====
    // To remove: delete this entire region AND the matching button in
    // BarcodeScannerPage.xaml. No other files need changes.
    private async void OnTorchToggleClicked(object sender, EventArgs e)
    {
        try
        {
            CameraView.TorchOn = !CameraView.TorchOn;
            TorchButton.Text = CameraView.TorchOn ? "🔦 Torch (On)" : "🔦 Torch";
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Torch toggle error: {ex}");
        }
    }
    // ===== OPTIONAL FEATURE: TORCH TOGGLE — END =====

    // ===== OPTIONAL FEATURE: CAMERA FLIP — START =====
    // To remove: delete this entire region AND the matching button in
    // BarcodeScannerPage.xaml. No other files need changes.
    private async void OnFlipCameraClicked(object sender, EventArgs e)
    {
        try
        {
            CameraView.CameraFacing = CameraView.CameraFacing == CameraFacing.Back
                ? CameraFacing.Front
                : CameraFacing.Back;
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Camera flip error: {ex}");
        }
    }
    // ===== OPTIONAL FEATURE: CAMERA FLIP — END =====

    // -------------------------------------------------------------------------
    // Single-shot completion + modal dismissal
    // -------------------------------------------------------------------------
    private async Task CompleteAsync(string? value)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0) return;

        CameraView.PauseScanning = true;
        CameraView.CameraEnabled = false;

        _tcs.TrySetResult(value);

        if (Navigation.ModalStack.Contains(this))
            await Navigation.PopModalAsync();
    }
}
