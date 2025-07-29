using Android.Bluetooth;
using Core.Bluetooth;
using Infrastructure.Bluetooth;

namespace AHS.Maui.Platforms.Android;

public class BluetoothService_Android : IBluetoothService
{
    private BluetoothAdapter _adapter = BluetoothAdapter.DefaultAdapter;
    private BluetoothDevice? _connectedDevice;

    public Task<List<BluetoothDeviceModel>> GetAvailableDevicesAsync()
    {
        var devices = _adapter.BondedDevices.Select(d => new BluetoothDeviceModel
        {
            Name = d.Name,
            Address = d.Address
        }).ToList();

        return Task.FromResult(devices);
    }

    public Task<bool> ConnectToDeviceAsync(BluetoothDeviceModel device)
    {
        _connectedDevice = _adapter.BondedDevices.FirstOrDefault(d => d.Address == device.Address);
        return Task.FromResult(_connectedDevice != null);
    }
}
