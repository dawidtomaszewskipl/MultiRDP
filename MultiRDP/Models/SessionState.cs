namespace MultiRDP.Models;

/// <summary>Stan sesji RDP / RDP session state</summary>
public enum SessionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error
}
