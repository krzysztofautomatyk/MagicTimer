using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Threading;
using MagicTimer.Settings;
using Microsoft.Win32;

namespace MagicTimer
{
    public partial class MainWindow : Window
    {
        private static readonly TimeSpan MinimumDuration = TimeSpan.FromMinutes(1);

        private readonly DispatcherTimer _tick;
        private readonly DispatcherTimer _confirmTimeout;
        private readonly DispatcherTimer _remind;

        private readonly SettingsStore _settingsStore;
        private readonly AppSettings _settings;

        private TimeSpan _initialDuration;
        private DateTimeOffset _endsAt;
        private bool _isRunning;

        private int _remindCount;

        public MainWindow()
        {
            InitializeComponent();

            var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            _settingsStore = new SettingsStore(settingsPath);
            _settings = _settingsStore.Load();

            var lastDuration = _settings.LastDuration;
            if (!string.IsNullOrWhiteSpace(lastDuration))
                DurationTextBox.Text = lastDuration;

            SoundPathText.Text = string.IsNullOrWhiteSpace(_settings.SoundFilePath) ? "(brak)" : _settings.SoundFilePath;

            _tick = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _tick.Tick += (_, _) => UpdateUi();

            _confirmTimeout = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            _confirmTimeout.Tick += (_, _) => HideConfirmBanner();

            _remind = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _remind.Tick += (_, _) => OnRemindTick();

            Closed += (_, _) => SaveLastDurationBestEffort();

            SetIdleState();
            UpdateStartStopButton();
        }

        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                StopRunning(showIdle: true);
                return;
            }

            if (!TryGetDuration(out var duration))
                return;

            StartNewCountdown(duration);
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            HideConfirmBanner();
        }

        private void ChooseSoundButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Wybierz plik dźwiękowy",
                Filter = "Audio (*.wav)|*.wav|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != true)
                return;

            _settings.SoundFilePath = dialog.FileName;
            _settingsStore.Save(_settings);

            SoundPathText.Text = _settings.SoundFilePath;
        }

        private void StartNewCountdown(TimeSpan duration)
        {
            _initialDuration = duration;
            _endsAt = DateTimeOffset.Now.Add(_initialDuration);
            _isRunning = true;

            SaveLastDurationBestEffort();

            _remind.Stop();
            _remindCount = 0;

            ConfirmBanner.Visibility = Visibility.Collapsed;
            _confirmTimeout.Stop();

            _tick.Start();
            UpdateUi();
            UpdateStartStopButton();
        }

        private bool TryGetDuration(out TimeSpan duration)
        {
            duration = default;

            var text = DurationTextBox.Text?.Trim() ?? "";
            if (!TryParseMmSs(text, out duration))
            {
                MessageBox.Show(this, "Podaj czas w formacie MM:SS (np. 05:00).", "MagicTimer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (duration < MinimumDuration)
            {
                MessageBox.Show(this, "Minimalny czas to 01:00.", "MagicTimer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private static bool TryParseMmSs(string text, out TimeSpan duration)
        {
            duration = default;

            var parts = text.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return false;

            if (!int.TryParse(parts[0], out var minutes))
                return false;

            if (!int.TryParse(parts[1], out var seconds))
                return false;

            if (minutes < 0)
                return false;

            if (seconds is < 0 or > 59)
                return false;

            duration = new TimeSpan(0, minutes, seconds);
            return true;
        }

        private void UpdateUi()
        {
            if (!_isRunning)
                return;

            var remaining = _endsAt - DateTimeOffset.Now;
            if (remaining <= TimeSpan.Zero)
            {
                TimeText.Text = "00:00";
                Progress.Value = 100;
                OnCompleted();
                return;
            }

            TimeText.Text = Format(remaining);

            if (_initialDuration > TimeSpan.Zero)
            {
                var done = 1.0 - (remaining.TotalMilliseconds / _initialDuration.TotalMilliseconds);
                Progress.Value = Math.Clamp(done * 100.0, 0, 100);
            }
            else
            {
                Progress.Value = 0;
            }
        }

        private void OnCompleted()
        {
            StopRunning(showIdle: false);

            // auto-restart natychmiast po osiągnięciu 00:00
            var nextDuration = _initialDuration;
            if (nextDuration < MinimumDuration)
                nextDuration = MinimumDuration;

            StartNewCountdown(nextDuration);

            // baner i przypominanie dźwiękiem działają niezależnie od kolejnego odliczania
            ConfirmBanner.Visibility = Visibility.Visible;

            _confirmTimeout.Stop();
            _confirmTimeout.Start();

            _remind.Stop();
            _remindCount = 0;
            _remind.Start();

            PlaySoundBestEffort();
        }

        private void OnRemindTick()
        {
            _remindCount++;
            if (_remindCount <= 3)
                PlaySoundBestEffort();

            if (_remindCount >= 3)
                HideConfirmBanner();
        }

        private void HideConfirmBanner()
        {
            ConfirmBanner.Visibility = Visibility.Collapsed;
            _confirmTimeout.Stop();
            _remind.Stop();
        }

        private void StopRunning(bool showIdle)
        {
            _isRunning = false;
            _tick.Stop();

            if (showIdle)
            {
                HideConfirmBanner();
                SetIdleState();
            }

            UpdateStartStopButton();
        }

        private void SetIdleState()
        {
            if (TryGetDuration(out var duration))
            {
                _initialDuration = duration;
                TimeText.Text = Format(duration);
                Progress.Value = 0;
            }
            else
            {
                TimeText.Text = "00:00";
                Progress.Value = 0;
            }
        }

        private static string Format(TimeSpan remaining)
        {
            var totalMinutes = (int)Math.Floor(remaining.TotalMinutes);
            return $"{totalMinutes:00}:{remaining.Seconds:00}";
        }

        private void PlaySoundBestEffort()
        {
            try
            {
                var path = _settings.SoundFilePath;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return;

                using var player = new SoundPlayer(path);
                player.Play();
            }
            catch
            {
                // best-effort
            }
        }

        private void UpdateStartStopButton()
        {
            if (StartStopButton is null)
                return;

            var startBrush = (System.Windows.Media.Brush?)TryFindResource("StartBrush")
                             ?? System.Windows.Media.Brushes.ForestGreen;
            var stopBrush = (System.Windows.Media.Brush?)TryFindResource("StopBrush")
                            ?? System.Windows.Media.Brushes.IndianRed;

            if (_isRunning)
            {
                StartStopButton.Content = "Stop";
                StartStopButton.Background = stopBrush;
            }
            else
            {
                StartStopButton.Content = "Start";
                StartStopButton.Background = startBrush;
            }
        }

        private void SaveLastDurationBestEffort()
        {
            try
            {
                var text = DurationTextBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    return;

                if (!TryParseMmSs(text, out var duration))
                    return;

                if (duration < MinimumDuration)
                    return;

                _settings.LastDuration = text;
                _settingsStore.Save(_settings);
            }
            catch
            {
                // best-effort
            }
        }
    }
}