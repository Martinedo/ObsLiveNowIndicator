using System;
using System.Threading.Tasks;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types.Events;
using Serilog;

namespace ObsLiveNowIndicator.Services;

public class ObsWebSocketService
{
    private readonly OBSWebsocket _obs;
    private bool _isConnected;
    private bool _shouldAutoReconnect = true;
    private bool _isReconnecting;
    private bool _isDisposed;
    private string _currentUrl = "";
    private int _currentPort;
    private string _currentPassword = "";

    public event Action<bool>? StreamingStateChanged;
    public bool IsStreaming { get; private set; }

    public ObsWebSocketService()
    {
        _obs = new OBSWebsocket();
        _obs.Connected += OnConnected;
        _obs.Disconnected += OnDisconnected;
        _obs.StreamStateChanged += OnStreamStateChanged;
    }

    public async Task<bool> ConnectAsync(string url, int port, string password)
    {
        try
        {
            Log.Debug("ConnectAsync called - URL: {Url}:{Port}", url, port);
            
            // Disconnect if already connected
            if (_isConnected)
            {
                Log.Debug("Already connected, disconnecting first...");
                await DisconnectAsync(false); // Don't auto-reconnect from this disconnect
            }

            _currentUrl = url;
            _currentPort = port;
            _currentPassword = password;
            _shouldAutoReconnect = true; // Enable auto-reconnect for this connection
            _isReconnecting = false;
            _isDisposed = false; // Reset disposed flag on new connection

            // Build WebSocket URL
            var wsUrl = $"{url}:{port}";
            
            Log.Information("Attempting to connect to {WsUrl}...", wsUrl);

            await Task.Run(() =>
            {
                if (string.IsNullOrEmpty(password))
                {
                    _obs.ConnectAsync(wsUrl, "");
                }
                else
                {
                    _obs.ConnectAsync(wsUrl, password);
                }
            });
            
            Log.Debug("Connection initiated (waiting for OnConnected callback)...");

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to connect to OBS");
            return false;
        }
    }

    public async Task DisconnectAsync(bool allowAutoReconnect = false)
    {
        _shouldAutoReconnect = allowAutoReconnect;
        // Only mark as disposed on permanent disconnect (app exit)
        // Don't set _isDisposed here - let it be controlled by the caller
        
        if (_isConnected)
        {
            await Task.Run(() => _obs.Disconnect());
            _isConnected = false;
        }
    }
    
    /// <summary>
    /// Permanently dispose the service (call on app exit only)
    /// </summary>
    public void Dispose()
    {
        _isDisposed = true;
        _shouldAutoReconnect = false;
        if (_isConnected)
        {
            _obs.Disconnect();
            _isConnected = false;
        }
    }

    private void OnConnected(object? sender, EventArgs e)
    {
        if (_isDisposed) return;
        
        _isConnected = true;
        Log.Information("Connected to OBS WebSocket");

        // Get initial streaming state
        try
        {
            var status = _obs.GetStreamStatus();
            IsStreaming = status.IsActive;
            Log.Debug("Initial streaming state: {IsStreaming}", IsStreaming);
            
            // Fire event on thread pool to avoid blocking OBS callback
            Task.Run(() => StreamingStateChanged?.Invoke(IsStreaming));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error getting initial streaming state");
        }
    }

    private void OnDisconnected(object? sender, OBSWebsocketDotNet.Communication.ObsDisconnectionInfo e)
    {
        _isConnected = false;
        Log.Information("Disconnected from OBS WebSocket: {Reason}", e.DisconnectReason);

        // Don't do anything if disposed (app is shutting down)
        if (_isDisposed) return;

        // Stop streaming indicator - fire on thread pool to avoid blocking
        IsStreaming = false;
        Task.Run(() => StreamingStateChanged?.Invoke(false));

        // Only attempt to reconnect if auto-reconnect is enabled and not already reconnecting
        if (_shouldAutoReconnect && !_isReconnecting && !string.IsNullOrEmpty(_currentUrl))
        {
            _isReconnecting = true;
            Log.Information("Scheduling reconnect in 5 seconds...");
            _ = Task.Delay(5000).ContinueWith(async _ => 
            {
                if (_shouldAutoReconnect && !_isConnected && !_isDisposed)
                {
                    Log.Information("Attempting to reconnect...");
                    await ConnectAsync(_currentUrl, _currentPort, _currentPassword);
                }
                _isReconnecting = false;
            });
        }
    }

    private void OnStreamStateChanged(object? sender, StreamStateChangedEventArgs e)
    {
        if (_isDisposed) return;
        
        IsStreaming = e.OutputState.IsActive;
        Log.Information("Stream state changed: {State}", IsStreaming ? "Started" : "Stopped");
        
        // Fire event on thread pool to avoid blocking OBS callback
        Task.Run(() =>
        {
            try
            {
                StreamingStateChanged?.Invoke(IsStreaming);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error invoking StreamingStateChanged");
            }
        });
    }
}
