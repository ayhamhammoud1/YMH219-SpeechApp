using System.Speech.Recognition;
using System.Speech.Synthesis;

namespace SpeechApp;

public partial class Form1 : Form
{
    private readonly SpeechSession session = new();
    private readonly ComboBox voices = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    private readonly ComboBox languages = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    private readonly RichTextBox input = new() { Dock = DockStyle.Fill, Text = "Merhaba! Bu metni sesli okutabilirsin." };
    private readonly RichTextBox transcript = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly Label playbackStatus = new() { Dock = DockStyle.Fill, Text = "Okuma bekliyor." };
    private readonly Label recognitionStatus = new() { Dock = DockStyle.Fill, Text = "Tanıma bekliyor." };

    public Form1()
    {
        InitializeComponent();
        Text = "Ses Atölyesi";
        ClientSize = new Size(980, 475);
        MinimumSize = new Size(780, 370);
        Font = new Font("Segoe UI", 10);
        BackColor = Color.LavenderBlush;
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(14) };
        grid.ColumnStyles.Add(new(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new(SizeType.Percent, 50));
        grid.Controls.Add(Section("Metni seslendir", voices, input, playbackStatus,
            Command("Oku", Speak), Command("Duraklat / Sürdür", Pause),
            Command("Durdur", () => TryPlayback(() => session.Speaker.SpeakAsyncCancelAll())),
            Command("Temizle", input.Clear)), 0, 0);
        var microphone = Command("Mikrofon", () => { });
        var wav = Command("WAV dosyası", () => { });
        var stop = Command("Durdur", () =>
        {
            try { session.StopRecognition(); } catch (InvalidOperationException ex) { recognitionStatus.Text = ex.Message; }
        });
        microphone.Click += async (_, _) => await RecognizeAsync();
        wav.Click += async (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "WAV ses dosyaları|*.wav" };
            if (dialog.ShowDialog(this) == DialogResult.OK) await RecognizeAsync(dialog.FileName);
        };
        grid.Controls.Add(Section("Konuşmayı yazıya çevir", languages, transcript, recognitionStatus,
            microphone, wav, stop, Command("Temizle", transcript.Clear)), 1, 0);
        Controls.Add(grid);
        Shown += (_, _) => LoadDevices();
        FormClosed += (_, _) => session.Dispose();
    }

    private void LoadDevices()
    {
        TryPlayback(() =>
        {
            foreach (var voice in session.Speaker.GetInstalledVoices().Where(v => v.Enabled))
                voices.Items.Add($"{voice.VoiceInfo.Name} ({voice.VoiceInfo.Culture.Name})");
            if (voices.Items.Count > 0) voices.SelectedIndex = 0;
            else playbackStatus.Text = "Windows SAPI ses paketi bulunamadı.";
            session.Speaker.SpeakCompleted += (_, e) => OnUi(() =>
                playbackStatus.Text = e.Error?.Message ?? (e.Cancelled ? "Okuma durduruldu." : "Okuma tamamlandı."));
        });
        try
        {
            languages.DisplayMember = nameof(RecognizerInfo.Description);
            foreach (var language in SpeechRecognitionEngine.InstalledRecognizers()) languages.Items.Add(language);
            if (languages.Items.Count > 0) languages.SelectedIndex = 0;
            else recognitionStatus.Text = "Windows SAPI tanıma modeli bulunamadı.";
        }
        catch (Exception ex) { recognitionStatus.Text = "Tanıma altyapısı açılamadı: " + ex.Message; }
    }

    private void Speak() => TryPlayback(() =>
    {
        if (voices.SelectedItem is not string selected) throw new InvalidOperationException("Önce bir Windows sesi seç.");
        if (string.IsNullOrWhiteSpace(input.Text)) throw new InvalidOperationException("Okunacak metni gir.");
        if (session.Speaker.State != SynthesizerState.Ready) return;
        session.Speaker.SelectVoice(selected[..selected.LastIndexOf(" (", StringComparison.Ordinal)]);
        session.Speaker.SpeakAsync(input.Text);
        playbackStatus.Text = "Okunuyor…";
    });

    private void Pause() => TryPlayback(() =>
    {
        if (session.Speaker.State == SynthesizerState.Speaking)
        {
            session.Speaker.Pause();
            playbackStatus.Text = "Okuma duraklatıldı.";
        }
        else if (session.Speaker.State == SynthesizerState.Paused)
        {
            session.Speaker.Resume();
            playbackStatus.Text = "Okunuyor…";
        }
    });

    private void TryPlayback(Action action)
    {
        try { action(); }
        catch (Exception ex) { playbackStatus.Text = "Ses çıkışı: " + ex.Message; }
    }

    private async Task RecognizeAsync(string? path = null)
    {
        if (session.Recognizer is not null) return;
        if (languages.SelectedItem is not RecognizerInfo language)
        {
            recognitionStatus.Text = "Bu işlem için bir Windows SAPI tanıma modeli kur.";
            return;
        }
        string source = path is null ? "Mikrofon" : "WAV";
        try
        {
            recognitionStatus.Text = $"{source} tanınıyor · {language.Culture.Name}";
            await session.RecognizeAsync(language, path, text => OnUi(() => transcript.AppendText(text + Environment.NewLine)));
            if (!IsDisposed) recognitionStatus.Text = $"{source} oturumu sona erdi.";
        }
        catch (Exception ex)
        {
            if (!IsDisposed) recognitionStatus.Text = $"{source} açılamadı veya tanıma durdu: {ex.Message}";
        }
    }

    private void OnUi(Action action)
    {
        if (!IsDisposed && IsHandleCreated) BeginInvoke(action);
    }

    private static Button Command(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += (_, _) => action();
        return button;
    }

    private static Control Section(string title, ComboBox selector, Control editor, Label status, params Control[] buttons)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, Padding = new Padding(8) };
        foreach (int height in new[] { 36, 38 }) panel.RowStyles.Add(new(SizeType.Absolute, height));
        panel.RowStyles.Add(new(SizeType.Percent, 100));
        panel.RowStyles.Add(new(SizeType.Absolute, 46));
        panel.RowStyles.Add(new(SizeType.Absolute, 45));
        panel.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI", 15, FontStyle.Bold) });
        panel.Controls.Add(selector);
        panel.Controls.Add(editor);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill };
        actions.Controls.AddRange(buttons);
        panel.Controls.Add(actions);
        panel.Controls.Add(status);
        return panel;
    }
}
