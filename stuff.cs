using System.Collections.Generic;

namespace AHS.Core.Bluetooth
{
    public class ScannedTag
    {
        public string TagNumber { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public interface IBluetoothSession
    {
        string LastConnectedDeviceId { get; set; }
        List<string> RawScanItems { get; }
        List<ScannedTag> ScannedTags { get; }
        void BufferScan(string input);
    }

    public class BluetoothSession : IBluetoothSession
    {
        public string LastConnectedDeviceId { get; set; } = string.Empty;
        public List<string> RawScanItems { get; } = new();
        public List<ScannedTag> ScannedTags { get; } = new();

        private string _scanBuffer = string.Empty;

        public void BufferScan(string input)
        {
            RawScanItems.Add(input);
            foreach (var ch in input)
            {
                if (ch == '\r' || ch == '\n')
                {
                    if (_scanBuffer.Length == 15 && _scanBuffer.All(char.IsDigit))
                    {
                        ScannedTags.Add(new ScannedTag
                        {
                            TagNumber = _scanBuffer,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    _scanBuffer = string.Empty;
                }
                else
                {
                    _scanBuffer += ch;
                }
            }
        }
    }
}
