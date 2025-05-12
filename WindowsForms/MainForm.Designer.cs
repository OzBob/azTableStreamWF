private Button AddButton;
private Button StartButton;
private Button StopButton;
private TextBox LogsTextBox;

private void InitializeComponent()
{
    this.AddButton = new Button();
    this.StartButton = new Button();
    this.StopButton = new Button();
    this.LogsTextBox = new TextBox();

    // AddButton
    this.AddButton.Text = "Add";
    this.AddButton.Click += new EventHandler(this.AddButton_Click);

    // StartButton
    this.StartButton.Text = "Start";
    this.StartButton.Click += new EventHandler(this.StartButton_Click);

    // StopButton
    this.StopButton.Text = "Stop";
    this.StopButton.Click += new EventHandler(this.StopButton_Click);

    // LogsTextBox
    this.LogsTextBox.Multiline = true;
    this.LogsTextBox.ScrollBars = ScrollBars.Vertical;

    // MainForm
    this.Controls.Add(this.AddButton);
    this.Controls.Add(this.StartButton);
    this.Controls.Add(this.StopButton);
    this.Controls.Add(this.LogsTextBox);
    this.Text = "Streaming Logs";
}
