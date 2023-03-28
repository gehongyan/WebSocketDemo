namespace WebSocketDemo;

/// <summary> Specifies the connection state of a client. </summary>
public enum ConnectionState : byte
{
    /// <summary> The client has disconnected from Business. </summary>
    Disconnected,

    /// <summary> The client is connecting to Business. </summary>
    Connecting,

    /// <summary> The client has established a connection to Business. </summary>
    Connected,

    /// <summary> The client is disconnecting from Business. </summary>
    Disconnecting
}
