using C;
using ChatApp_Part2;
using System;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ChatApp_Part2
{
    public partial class MainWindow : Window
    {
        // New class name: SecurityBot (instead of CyberBot)
        private SecurityBot securityBot;

        public MainWindow()
        {
            InitializeComponent();
            securityBot = new SecurityBot();
            Loaded += MainWindow_Loaded;
        }

        // ── Startup ────────────────────────────────────────────────────────
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Log session open
            ThreatResponseEngine.WriteSessionMarker(starting: true);

            // Play audio immediately on launch
            _ = Task.Run(() => PlayStartupAudio());

            await DisplayShieldMessage("🛡  SecureShield Awareness System is online.");
            await Task.Delay(200);
            await DisplayShieldMessage("Your digital security is our mission.");
            await Task.Delay(200);
            await DisplayShieldMessage("State your agent name to begin.");
        }

        // ── Button handlers ────────────────────────────────────────────────
        private async void SendButton_Click(object sender, RoutedEventArgs e)
            => await HandleAgentInput();

        private async void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
            {
                e.Handled = true;
                await HandleAgentInput();
            }
        }

        private async void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ChatPanel.Children.Clear();
            DrillPanelBorder.Visibility = Visibility.Collapsed;
            await DisplayShieldMessage("🧹  Channel cleared. SecureShield is standing by.");
        }

        // ── Core input handler ─────────────────────────────────────────────
        private async Task HandleAgentInput()
        {
            string agentInput = InputTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(agentInput)) return;

            // Render agent bubble
            RenderAgentBubble(agentInput);
            InputTextBox.Clear();
            InputTextBox.Focus();

            // Show "processing" indicator
            ToggleProcessingIndicator(true);

            // Dispatch to ThreatResponseEngine (new class name)
            string response = await Task.Run(() =>
                ThreatResponseEngine.Dispatch(agentInput, securityBot));

            ToggleProcessingIndicator(false);

            // Render shield response
            await DisplayShieldMessage(response);

            // Refresh drill panel state
            RefreshDrillPanel();

            await Task.Delay(50);
            ChatScrollViewer.ScrollToBottom();
        }

        // ── Agent bubble (purple) ──────────────────────────────────────────
        private void RenderAgentBubble(string text)
        {
            Border bubble = new Border { Style = (Style)FindResource("AgentBubble") };
            TextBlock tb = new TextBlock
            {
                Text = $"🧑‍💻  {text}",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                FontSize = 14,
            };
            bubble.Child = tb;
            ChatPanel.Children.Add(bubble);
            ChatScrollViewer.ScrollToBottom();
        }

        // ── Shield message (green, with streaming text effect) ─────────────
        private async Task DisplayShieldMessage(string message)
        {
            Border bubble = new Border { Style = (Style)FindResource("ShieldBubble") };
            TextBlock tb = new TextBlock
            {
                Text = "🛡  ",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(0x86, 0xEF, 0xAC)),
                FontSize = 14,
                FontFamily = new FontFamily("Consolas"),
            };
            bubble.Child = tb;
            ChatPanel.Children.Add(bubble);
            ChatScrollViewer.ScrollToBottom();

            // Streaming text effect (faster for longer messages)
            for (int i = 0; i <= message.Length; i++)
            {
                tb.Text = $"🛡  {message[..i]}";
                await Task.Delay(5);
                ChatScrollViewer.ScrollToBottom();
            }

            // Apply colour formatting after streaming completes
            ApplyGreenThemeFormatting(tb, message);
            ChatScrollViewer.ScrollToBottom();

            // Update drill panel
            RefreshDrillPanel();
        }

        // ── Audio ──────────────────────────────────────────────────────────
        private void PlayStartupAudio()
        {
            try
            {
                string path = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "welcome.wav");
                if (System.IO.File.Exists(path))
                {
                    using SoundPlayer player = new SoundPlayer(path);
                    player.PlaySync();
                }
            }
            catch { /* non-critical */ }
        }

        // ── Processing indicator ────────────────────────────────────────────
        private Border? _processingBubble;

        private void ToggleProcessingIndicator(bool show)
        {
            if (show)
            {
                _processingBubble = new Border { Style = (Style)FindResource("ShieldBubble") };
                TextBlock tb = new TextBlock
                {
                    Text = "🛡  processing…",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x2D, 0x6A, 0x2D)),
                    FontStyle = FontStyles.Italic,
                    FontSize = 13,
                };
                _processingBubble.Child = tb;
                ChatPanel.Children.Add(_processingBubble);
                ChatScrollViewer.ScrollToBottom();
            }
            else
            {
                if (_processingBubble != null && ChatPanel.Children.Contains(_processingBubble))
                    ChatPanel.Children.Remove(_processingBubble);
                _processingBubble = null;
            }
        }

        // ── Drill panel ─────────────────────────────────────────────────────
        private void RefreshDrillPanel()
        {
            if (!ThreatResponseEngine.DrillActive)
            {
                DrillPanelBorder.Visibility = Visibility.Collapsed;
                return;
            }

            DrillPanelBorder.Visibility = Visibility.Visible;
            BuildDrillChoiceButtons();
        }

        private void BuildDrillChoiceButtons()
        {
            DrillOptionsPanel.Children.Clear();

            foreach (string label in new[] { "1", "2", "3", "4" })
            {
                Button btn = new Button
                {
                    Content = $"  {label}",
                    Style = (Style)FindResource("DrillButton"),
                    Tag = label,
                };
                btn.Click += DrillChoiceButton_Click;
                DrillOptionsPanel.Children.Add(btn);
            }

            DrillProgressText.Text = "Drill active — click a choice or type 1 / 2 / 3 / 4";
            DrillScoreText.Text = string.Empty;
        }

        private async void DrillChoiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                InputTextBox.Text = tag;
                await HandleAgentInput();
            }
        }

        // ── Green/purple colour formatting ──────────────────────────────────
        private static void ApplyGreenThemeFormatting(TextBlock tb, string fullText)
        {
            tb.Text = string.Empty;
            tb.Inlines.Clear();

            foreach (string rawLine in fullText.Split('\n'))
            {
                Run run = new Run(rawLine)
                {
                    Foreground = ResolveLineColour(rawLine)
                };
                tb.Inlines.Add(run);
                tb.Inlines.Add(new LineBreak());
            }
        }

        private static Brush ResolveLineColour(string line)
        {
            // Section headers with emoji
            if (line.StartsWith("🎣") || line.StartsWith("🔑") || line.StartsWith("🌐") ||
                line.StartsWith("🛡") || line.StartsWith("🔒") || line.StartsWith("🎭") ||
                line.StartsWith("🦠") || line.StartsWith("🔏") || line.StartsWith("🚨") ||
                line.StartsWith("📋") || line.StartsWith("📜") || line.StartsWith("⭐"))
                return new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));   // bright green

            // Confirmed correct / positive
            if (line.TrimStart().StartsWith("✓") || line.StartsWith("✅"))
                return new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99));   // emerald

            // Incorrect / negative / warning
            if (line.TrimStart().StartsWith("✗") || line.StartsWith("❌") || line.StartsWith("⚠️"))
                return new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));   // soft red

            // Bullet points
            if (line.TrimStart().StartsWith("•"))
                return new SolidColorBrush(Color.FromRgb(0xA7, 0x8B, 0xFA));   // purple/lavender

            // Debrief / tips
            if (line.TrimStart().StartsWith("💡"))
                return new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));   // amber

            // Numbered steps (incident response, drill options)
            if (line.TrimStart().StartsWith("1.") || line.TrimStart().StartsWith("2.") ||
                line.TrimStart().StartsWith("3.") || line.TrimStart().StartsWith("4.") ||
                line.TrimStart().StartsWith("5.") || line.TrimStart().StartsWith("6.") ||
                line.TrimStart().StartsWith("7."))
                return new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));   // amber

            // Separator lines
            if (line.StartsWith("─") || line.StartsWith("═") || line.StartsWith("══"))
                return new SolidColorBrush(Color.FromRgb(0x2D, 0x6A, 0x2D));   // dark green

            // Default text
            return new SolidColorBrush(Color.FromRgb(0x86, 0xEF, 0xAC));       // light green
        }

        // ── Window close ────────────────────────────────────────────────────
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            ThreatResponseEngine.WriteSessionMarker(starting: false);
            base.OnClosing(e);
        }
    }
}