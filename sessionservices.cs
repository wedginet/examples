private const string BluetoothKey = "SelectedBluetoothDevice";

public Task SetSelectedBluetoothDeviceAsync(BluetoothDeviceModel device)
{
    Preferences.Set(BluetoothKey, JsonSerializer.Serialize(device));
    return Task.CompletedTask;
}

public Task<BluetoothDeviceModel?> GetSelectedBluetoothDeviceAsync()
{
    if (Preferences.ContainsKey(BluetoothKey))
    {
        var json = Preferences.Get(BluetoothKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                return Task.FromResult(JsonSerializer.Deserialize<BluetoothDeviceModel>(json));
            }
            catch { }
        }
    }
    return Task.FromResult<BluetoothDeviceModel?>(null);
}
