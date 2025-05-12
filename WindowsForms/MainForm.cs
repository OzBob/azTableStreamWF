public partial class MainForm : Form
{
    private readonly IStreamLoggingService _streamLoggingService;

    public MainForm(IStreamLoggingService streamLoggingService)
    {
        _streamLoggingService = streamLoggingService;
        InitializeComponent();
    }

    private async void AddButton_Click(object sender, EventArgs e)
    {
        await _streamLoggingService.AddLogAsync();
    }

    private async void StartButton_Click(object sender, EventArgs e)
    {
        await _streamLoggingService.StartStreamingAsync(LogsTextBox);
    }

    private async void StopButton_Click(object sender, EventArgs e)
    {
        await _streamLoggingService.StopStreamingAsync();
    }
}
