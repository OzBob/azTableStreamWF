public class StreamLoggingService : IStreamLoggingService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ClientWebSocket _webSocket;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private bool _disposed;

    public StreamLoggingService()
    {
        _httpClient = new HttpClient();
        _webSocket = new ClientWebSocket();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task AddLogAsync()
    {
        var log = new
        {
            Uid = Guid.NewGuid().ToString(),
            EventId = "event1",
            ParentEventId = (string?)null,
            CorrelationId = "corr1",
            Message = "Test log",
            Level = "Info",
            ContextMethod = "StreamLoggingService.AddLogAsync",
            Timestamp = DateTime.UtcNow,
            ExceptionJson = (string?)null,
            ContextJson = (string?)null
        };

        await _httpClient.PostAsJsonAsync("http://localhost:5000/logs", log);
    }

    public async Task StartStreamingAsync(TextBox logsTextBox)
    {
        var uid = "some-unique-id"; // Replace with actual UID
        await _webSocket.ConnectAsync(new Uri($"ws://localhost:5000/subscribe/{uid}"), _cancellationTokenSource.Token);

        _ = Task.Run(async () =>
        {
            var buffer = new byte[1024 * 4];
            while (_webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    logsTextBox.Invoke(() => logsTextBox.AppendText(message + Environment.NewLine));
                }
            }
        });
    }

    public async Task StopStreamingAsync()
    {
        var uid = "some-unique-id"; // Replace with actual UID
        await _httpClient.PostAsync($"http://localhost:5000/unsubscribe/{uid}", null);
        _webSocket.Abort();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _httpClient.Dispose();
                _webSocket.Dispose();
                _cancellationTokenSource.Dispose();
            }
            _disposed = true;
        }
    }
}
