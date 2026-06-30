using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using TorrWind.Core.Localization;
using TorrWind.Core.Services;

namespace TorrWind.App;

public partial class PlayerWindow : Window
{
    private readonly Uri _mediaUri;
    private readonly string _mediaTitle;
    private readonly JsonLocalizationService _localization;
    private readonly DispatcherTimer _positionTimer;
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private bool _isDraggingPosition;
    private bool _isClosing;

    public PlayerWindow(Uri mediaUri, string mediaTitle, JsonLocalizationService localization)
    {
        InitializeComponent();
        _mediaUri = mediaUri;
        _mediaTitle = string.IsNullOrWhiteSpace(mediaTitle) ? mediaUri.ToString() : mediaTitle;
        _localization = localization;
        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _positionTimer.Tick += OnPositionTimerTick;

        Title = string.Format(_localization["PlayerWindowTitle"], _mediaTitle);
        TitleText.Text = _mediaTitle;
        PlayPauseButton.Content = _localization["ActionPause"];
        StopButton.Content = _localization["ActionStop"];
        CloseButton.Content = _localization["ActionClose"];
        VolumeLabel.Text = _localization["FieldVolume"];
        TimeText.Text = FormatTime(0, 0);
        StatusText.Text = _localization["PlayerStatusReady"];
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LibVLCSharp.Shared.Core.Initialize();
            _libVlc = new LibVLC("--no-video-title-show");
            _mediaPlayer = new MediaPlayer(_libVlc);
            _mediaPlayer.Playing += (_, _) => Dispatcher.Invoke(() => SetPlaybackStatus(_localization["PlayerStatusPlaying"]));
            _mediaPlayer.Paused += (_, _) => Dispatcher.Invoke(() => SetPlaybackStatus(_localization["PlayerStatusPaused"]));
            _mediaPlayer.Stopped += (_, _) => Dispatcher.Invoke(() => SetPlaybackStatus(_localization["PlayerStatusStopped"]));
            _mediaPlayer.EndReached += (_, _) => Dispatcher.Invoke(() => SetPlaybackStatus(_localization["PlayerStatusStopped"]));
            _mediaPlayer.EncounteredError += (_, _) => Dispatcher.Invoke(() => SetPlaybackStatus(_localization["PlayerStatusError"]));
            VideoView.MediaPlayer = _mediaPlayer;

            using var media = new Media(_libVlc, _mediaUri);
            media.AddOption(":http-reconnect");
            media.AddOption(":network-caching=1500");
            media.AddOption(":file-caching=1000");
            _mediaPlayer.Play(media);
            _mediaPlayer.Volume = (int)VolumeSlider.Value;
            _positionTimer.Start();
        }
        catch (Exception exception)
        {
            StatusText.Text = exception.Message;
            FileEventLog.User.Error("Player", "Built-in LibVLC player failed to start.", exception, _mediaUri.ToString());
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _isClosing = true;
        _positionTimer.Stop();

        try
        {
            if (_mediaPlayer is not null)
            {
                _mediaPlayer.Stop();
                VideoView.MediaPlayer = null;
                _mediaPlayer.Dispose();
            }

            _libVlc?.Dispose();
        }
        catch (Exception exception)
        {
            FileEventLog.User.Warning("Player", "Built-in player cleanup failed.", exception.Message);
        }
    }

    private void OnPlayPause(object sender, RoutedEventArgs e)
    {
        if (_mediaPlayer is null)
        {
            return;
        }

        if (_mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Pause();
            return;
        }

        _mediaPlayer.Play();
    }

    private void OnStop(object sender, RoutedEventArgs e)
    {
        _mediaPlayer?.Stop();
        PositionSlider.Value = 0;
        TimeText.Text = FormatTime(0, _mediaPlayer?.Length ?? 0);
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnPositionMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingPosition = true;
    }

    private void OnPositionMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingPosition = false;
        SeekToSliderPosition();
    }

    private void OnPositionValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isDraggingPosition)
        {
            TimeText.Text = FormatTime(SliderPositionToTime(), _mediaPlayer?.Length ?? 0);
        }
    }

    private void OnVolumeValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_mediaPlayer is not null)
        {
            _mediaPlayer.Volume = (int)Math.Round(e.NewValue);
        }
    }

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (_mediaPlayer is null || _isDraggingPosition || _isClosing)
        {
            return;
        }

        var length = _mediaPlayer.Length;
        var time = _mediaPlayer.Time;
        if (length > 0)
        {
            PositionSlider.Value = Math.Clamp(_mediaPlayer.Position * PositionSlider.Maximum, 0, PositionSlider.Maximum);
        }

        TimeText.Text = FormatTime(time, length);
    }

    private void SeekToSliderPosition()
    {
        if (_mediaPlayer is null)
        {
            return;
        }

        _mediaPlayer.Position = (float)Math.Clamp(PositionSlider.Value / PositionSlider.Maximum, 0, 1);
    }

    private long SliderPositionToTime()
    {
        var length = _mediaPlayer?.Length ?? 0;
        return length <= 0
            ? 0
            : (long)(length * Math.Clamp(PositionSlider.Value / PositionSlider.Maximum, 0, 1));
    }

    private void SetPlaybackStatus(string status)
    {
        StatusText.Text = status;
        StatusText.Visibility = _mediaPlayer?.IsPlaying == true
            ? Visibility.Collapsed
            : Visibility.Visible;
        PlayPauseButton.Content = _mediaPlayer?.IsPlaying == true
            ? _localization["ActionPause"]
            : _localization["ActionPlay"];
    }

    private static string FormatTime(long timeMs, long lengthMs)
    {
        return $"{FormatDuration(timeMs)} / {FormatDuration(lengthMs)}";
    }

    private static string FormatDuration(long valueMs)
    {
        if (valueMs <= 0)
        {
            return "00:00";
        }

        var value = TimeSpan.FromMilliseconds(valueMs);
        return value.TotalHours >= 1
            ? value.ToString(@"hh\:mm\:ss")
            : value.ToString(@"mm\:ss");
    }
}
