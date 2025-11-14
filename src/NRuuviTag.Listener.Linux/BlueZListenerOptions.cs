namespace NRuuviTag.Listener.Linux;

/// <summary>
/// Options for <see cref="BlueZListener"/>.
/// </summary>
public class BlueZListenerOptions : RuuviTagListenerOptions {

    /// <summary>
    /// The name of the Bluetooth adapter to use.
    /// </summary>
    public string AdapterName { get; set; } = BlueZListener.DefaultBluetoothAdapter;

}
