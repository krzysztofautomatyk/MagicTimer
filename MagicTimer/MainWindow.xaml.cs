using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MagicTimer.Settings;
using Microsoft.Win32;
using NAudio.Wave;

namespace MagicTimer
{
    public partial class MainWindow : Window
    {
        private static readonly TimeSpan MinimumDuration = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan BlinkThreshold = TimeSpan.FromSeconds(5);

        private readonly DispatcherTimer _tick;
        private readonly DispatcherTimer _confirmTimeout;
        private readonly DispatcherTimer _remind;

        private readonly SettingsStore _settingsStore;
        private readonly AppSettings _settings;

        private TimeSpan _initialDuration;
        private DateTimeOffset _endsAt;
        private bool _isRunning;
        private bool _isBlinking;

        private int _remindCount;

        private const double CompactHeight = 120;

        private IWavePlayer? _audioPlayer;
        private AudioFileReader? _audioFile;

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

            ApplyTimerFont();
            ApplyColors();

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

            Closed += (_, _) =>
            {
                StopSound();
                SaveLastDurationBestEffort();
            };

            // Ustaw checkbox BEZ wywoływania eventu
            EnableRemindersCheckBox.Checked -= EnableRemindersCheckBox_CheckedChanged;
            EnableRemindersCheckBox.Unchecked -= EnableRemindersCheckBox_CheckedChanged;
            EnableRemindersCheckBox.IsChecked = _settings.EnableReminders;
            EnableRemindersCheckBox.Checked += EnableRemindersCheckBox_CheckedChanged;
            EnableRemindersCheckBox.Unchecked += EnableRemindersCheckBox_CheckedChanged;

            SetIdleState();
            UpdateStartStopButton();
            UpdateCompactMode();
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
                Filter = "Audio (*.wav;*.mp3)|*.wav;*.mp3|WAV (*.wav)|*.wav|MP3 (*.mp3)|*.mp3|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != true)
                return;

            _settings.SoundFilePath = dialog.FileName;
            _settingsStore.Save(_settings);

            SoundPathText.Text = _settings.SoundFilePath;
        }

        private void ChooseFontButton_Click(object sender, RoutedEventArgs e)
        {
            var fontDialog = new FontPickerDialog(this, _settings.TimerFontFamily ?? "Consolas");
            if (fontDialog.ShowDialog() != true)
                return;

            var selectedFont = fontDialog.SelectedFontFamily;
            if (string.IsNullOrWhiteSpace(selectedFont))
                return;

            _settings.TimerFontFamily = selectedFont;
            _settingsStore.Save(_settings);

            ApplyTimerFont();
        }

        private void ChooseColorsButton_Click(object sender, RoutedEventArgs e)
        {
            var colorDialog = new ColorPickerDialog(this, _settings, () =>
            {
                _settingsStore.Save(_settings);
                ApplyColors();
            });
            colorDialog.ShowDialog();
        }

        private void ApplyTimerFont()
        {
            var fontName = _settings.TimerFontFamily;
            if (string.IsNullOrWhiteSpace(fontName))
                fontName = "Consolas";

            try
            {
                TimeText.FontFamily = new FontFamily(fontName);
            }
            catch
            {
                TimeText.FontFamily = new FontFamily("Consolas");
            }
        }

        private void ApplyColors()
        {
            try
            {
                // Tło okna
                Background = new SolidColorBrush(ParseColor(_settings.BackgroundColor, "#0B0E14"));

                // Tło i tekst zegarka
                TimerBorder.Background = new SolidColorBrush(ParseColor(_settings.TimerBackgroundColor, "#0F172A"));
                TimeText.Foreground = new SolidColorBrush(ParseColor(_settings.TimerForegroundColor, "#E6EDF3"));

                // Progress bar
                Progress.Foreground = new SolidColorBrush(ParseColor(_settings.ProgressBarColor, "#58A6FF"));

                // Aktualizuj zasoby kolorów dla przycisków Start/Stop
                Resources["StartBrush"] = new SolidColorBrush(ParseColor(_settings.StartButtonColor, "#2EA043"));
                Resources["StopBrush"] = new SolidColorBrush(ParseColor(_settings.StopButtonColor, "#D73A49"));

                // Kolory tekstu (labele)
                var textColor = new SolidColorBrush(ParseColor(_settings.TextColor, "#E6EDF3"));
                TimeLabel.Foreground = textColor;
                SoundPathText.Foreground = textColor;
                EnableRemindersCheckBox.Foreground = textColor;

                // Tło i tekst inputa
                var inputBg = new SolidColorBrush(ParseColor(_settings.InputBackgroundColor, "#111827"));
                DurationTextBox.Background = inputBg;
                DurationTextBox.Foreground = textColor;

                // Baner potwierdzenia
                ConfirmBanner.Background = new SolidColorBrush(ParseColor(_settings.BannerBackgroundColor, "#7F1D1D"));

                // Zaktualizuj przycisk Start/Stop
                UpdateStartStopButton();
            }
            catch
            {
                // best-effort
            }
        }

        private static Color ParseColor(string colorText, string fallback)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(colorText))
                    return (Color)ColorConverter.ConvertFromString(colorText);
            }
            catch { }

            return (Color)ColorConverter.ConvertFromString(fallback);
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
                StopBlinking();
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

            if (remaining <= BlinkThreshold && !_isBlinking)
            {
                StartBlinking();
            }
            else if (remaining > BlinkThreshold && _isBlinking)
            {
                StopBlinking();
            }
        }

        private void StartBlinking()
        {
            if (_isBlinking)
                return;

            _isBlinking = true;
            
            // Utwórz dynamiczną animację z kolorami z ustawień
            var normalColor = ParseColor(_settings.TimerBackgroundColor, "#0F172A");
            var blinkColor = ParseColor(_settings.BlinkColor, "#7F1D1D");
            
            var animation = new ColorAnimationUsingKeyFrames
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            animation.KeyFrames.Add(new DiscreteColorKeyFrame(normalColor, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new DiscreteColorKeyFrame(blinkColor, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.5))));
            animation.KeyFrames.Add(new DiscreteColorKeyFrame(normalColor, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1))));
            
            TimerBorder.Background = new SolidColorBrush(normalColor);
            TimerBorder.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        private void StopBlinking()
        {
            if (!_isBlinking)
                return;

            _isBlinking = false;
            
            TimerBorder.Background.BeginAnimation(SolidColorBrush.ColorProperty, null);
            TimerBorder.Background = new SolidColorBrush(ParseColor(_settings.TimerBackgroundColor, "#0F172A"));
        }

        private void OnCompleted()
        {
            StopRunning(showIdle: false);

            // auto-restart natychmiast po osiągnięciu 00:00
            var nextDuration = _initialDuration;
            if (nextDuration < MinimumDuration)
                nextDuration = MinimumDuration;

            StartNewCountdown(nextDuration);

            // baner i przypominanie dźwiękiem tylko gdy włączone
            if (_settings.EnableReminders)
            {
                ConfirmBanner.Visibility = Visibility.Visible;

                _confirmTimeout.Stop();
                _confirmTimeout.Start();

                _remind.Stop();
                _remindCount = 0;
                _remind.Start();
            }

            PlaySoundBestEffort();
        }

        private void OnRemindTick()
        {
            if (!_settings.EnableReminders)
            {
                _remind.Stop();
                return;
            }

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
                StopSound();

                var path = _settings.SoundFilePath;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return;

                var extension = Path.GetExtension(path).ToLowerInvariant();

                if (extension == ".wav")
                {
                    using var player = new SoundPlayer(path);
                    player.Play();
                }
                else if (extension == ".mp3")
                {
                    _audioFile = new AudioFileReader(path);
                    _audioPlayer = new WaveOutEvent();
                    _audioPlayer.Init(_audioFile);
                    _audioPlayer.Play();
                }
            }
            catch
            {
                // best-effort
            }
        }

        private void StopSound()
        {
            try
            {
                _audioPlayer?.Stop();
                _audioPlayer?.Dispose();
                _audioPlayer = null;

                _audioFile?.Dispose();
                _audioFile = null;
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

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCompactMode();
        }

        private void UpdateCompactMode()
        {
            var isCompact = ActualHeight < CompactHeight;

            Progress.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
            ControlsPanel.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
            SoundPathText.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        }

        private void EnableRemindersCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_settings == null || _settingsStore == null)
                return;

            _settings.EnableReminders = EnableRemindersCheckBox.IsChecked == true;
            _settingsStore.Save(_settings);

            // Jeśli przypomnienia są wyłączone podczas działania, zatrzymaj przypominanie i ukryj baner
            if (!_settings.EnableReminders)
            {
                if (_remind != null)
                {
                    _remind.Stop();
                    _remindCount = 0;
                }
                
                // Ukryj baner potwierdzenia od razu
                HideConfirmBanner();
            }
        }
    }
}