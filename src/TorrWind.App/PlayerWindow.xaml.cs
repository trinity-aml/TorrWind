using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using TorrWind.Core.Localization;
using TorrWind.Core.Models;
using TorrWind.Core.Services;
using ComboBox = System.Windows.Controls.ComboBox;
using MenuItem = System.Windows.Controls.MenuItem;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsMouseButtons = System.Windows.Forms.MouseButtons;
using WinFormsMouseEventArgs = System.Windows.Forms.MouseEventArgs;
using WinFormsPanel = System.Windows.Forms.Panel;

namespace TorrWind.App;

public partial class PlayerWindow : Window
{
    private const string IconPlay = "\uE768";
    private const string IconPause = "\uE769";
    private const string IconFullscreen = "\uE740";
    private const string IconWindowed = "\uE73F";
    private const int WindowMessageLeftButtonDown = 0x0201;
    private const int WindowMessageLeftDoubleClick = 0x0203;
    private const int WindowMessageRightButtonUp = 0x0205;
    private const int WindowMessageContextMenu = 0x007B;
    private const uint SetWindowPosNoMove = 0x0002;
    private const uint SetWindowPosNoSize = 0x0001;
    private const uint SetWindowPosShowWindow = 0x0040;
    private const uint SetWindowPosFrameChanged = 0x0020;

    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNotTopmost = new(-2);

    private readonly Uri _mediaUri;
    private readonly string _mediaTitle;
    private readonly JsonLocalizationService _localization;
    private readonly ServerProfile? _server;
    private readonly DispatcherTimer _positionTimer;
    private readonly DispatcherTimer _trackRefreshTimer;
    private readonly ObservableCollection<PlayerPlaylistItem> _playlist = [];

    private MpvPlayerHost? _player;
    private WinFormsPanel? _mpvPanel;
    private WinFormsLabel? _nativeStatusLabel;
    private HttpClient? _httpClient;
    private int _currentPlaylistIndex = -1;
    private long _currentTimeMs;
    private long _currentLengthMs;
    private string? _lastTrackListSignature;
    private long _lastNativeLeftClickTicks;
    private int _lastNativeLeftClickX;
    private int _lastNativeLeftClickY;
    private long _lastPointerFullscreenToggleTicks;
    private bool _isDraggingPosition;
    private bool _isClosing;
    private bool _isUpdatingPlaylistSelection;
    private bool _isUpdatingTrackControls;
    private bool _isPollingPosition;
    private bool _isRefreshingTracks;
    private bool _manualStopRequested;
    private bool _hasHandledEnd;
    private bool _isFullscreen;
    private bool _isPlayerPaused = true;
    private bool _isPlayerIdle = true;
    private WindowState _windowStateBeforeFullscreen;
    private WindowStyle _windowStyleBeforeFullscreen;
    private ResizeMode _resizeModeBeforeFullscreen;
    private bool _topmostBeforeFullscreen;
    private GridLength _sidebarWidthBeforeFullscreen;
    private double _leftBeforeFullscreen;
    private double _topBeforeFullscreen;
    private double _widthBeforeFullscreen;
    private double _heightBeforeFullscreen;

    public PlayerWindow(
        Uri mediaUri,
        string mediaTitle,
        JsonLocalizationService localization,
        ServerProfile? server = null)
    {
        InitializeComponent();
        _mediaUri = mediaUri;
        _mediaTitle = string.IsNullOrWhiteSpace(mediaTitle) ? mediaUri.ToString() : mediaTitle;
        _localization = localization;
        _server = server;

        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _positionTimer.Tick += OnPositionTimerTick;

        _trackRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _trackRefreshTimer.Tick += OnTrackRefreshTimerTick;

        ConfigureVideoHost();
        ConfigureText();
        ConfigureStaticOptions();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _player = new MpvPlayerHost();
            _player.EndReached += OnMpvEndReached;
            _player.Exited += OnMpvExited;
            _player.TracksChanged += OnMpvTracksChanged;

            ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;

            PlaylistList.ItemsSource = _playlist;
            await _player.StartAsync(GetVideoWindowHandle(), _server, CancellationToken.None).ConfigureAwait(true);
            await LoadPlaylistAsync().ConfigureAwait(true);
            await PlayPlaylistIndexAsync(0).ConfigureAwait(true);
            _positionTimer.Start();
        }
        catch (Exception exception)
        {
            StatusText.Text = exception.Message;
            StatusText.Visibility = Visibility.Visible;
            if (_nativeStatusLabel is not null)
            {
                _nativeStatusLabel.Text = exception.Message;
                _nativeStatusLabel.Visible = true;
            }

            FileEventLog.User.Error("Player", "Built-in mpv player failed to start.", exception, _mediaUri.ToString());
        }
    }

    private void ConfigureVideoHost()
    {
        _mpvPanel = new WinFormsPanel
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
            BackColor = System.Drawing.Color.Black
        };
        _mpvPanel.MouseDown += OnNativeVideoMouseDown;
        _nativeStatusLabel = new WinFormsLabel
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
            BackColor = System.Drawing.Color.Black,
            ForeColor = System.Drawing.Color.FromArgb(201, 211, 223),
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        };
        _nativeStatusLabel.MouseDown += OnNativeVideoMouseDown;
        _mpvPanel.Controls.Add(_nativeStatusLabel);
        MpvHostControl.Child = _mpvPanel;
    }

    private IntPtr GetVideoWindowHandle()
    {
        if (_mpvPanel is null)
        {
            throw new InvalidOperationException("Video host is not initialized.");
        }

        _mpvPanel.CreateControl();
        return _mpvPanel.Handle;
    }

    private void ConfigureText()
    {
        Title = string.Format(_localization["PlayerWindowTitle"], _mediaTitle);
        TitleText.Text = _mediaTitle;
        PlaylistHeaderText.Text = _localization["PlayerPlaylist"];
        TrackSettingsHeaderText.Text = _localization["PlayerTrackSettings"];
        AudioTrackLabel.Text = _localization["PlayerAudioTrack"];
        AudioDelayLabel.Text = _localization["PlayerAudioDelay"];
        VideoTrackLabel.Text = _localization["PlayerVideoTrack"];
        AspectRatioLabel.Text = _localization["PlayerAspectRatio"];
        SubtitleTrackLabel.Text = _localization["PlayerSubtitleTrack"];
        SubtitleDelayLabel.Text = _localization["PlayerSubtitleDelay"];
        VolumeLabel.Text = _localization["FieldVolume"];
        TimeText.Text = FormatTime(0, 0);
        StatusText.Text = _localization["PlayerStatusReady"];
        if (_nativeStatusLabel is not null)
        {
            _nativeStatusLabel.Text = StatusText.Text;
        }

        PreviousButton.ToolTip = _localization["PlayerPreviousEpisode"];
        PlayPauseButton.Content = IconPlay;
        PlayPauseButton.ToolTip = _localization["ActionPlay"];
        StopButton.ToolTip = _localization["ActionStop"];
        NextButton.ToolTip = _localization["PlayerNextEpisode"];
        FullscreenButton.Content = IconFullscreen;
        FullscreenButton.ToolTip = _localization["PlayerEnterFullscreen"];
        CloseButton.ToolTip = _localization["ActionClose"];
    }

    private void ConfigureStaticOptions()
    {
        AspectRatioCombo.ItemsSource = new[]
        {
            new PlayerOption(string.Empty, _localization["PlayerAspectDefault"]),
            new PlayerOption("16:9", "16:9"),
            new PlayerOption("4:3", "4:3"),
            new PlayerOption("21:9", "21:9"),
            new PlayerOption("1:1", "1:1")
        };
        AspectRatioCombo.SelectedIndex = 0;
    }

    private async Task LoadPlaylistAsync()
    {
        _playlist.Clear();

        if (M3uPlaylistParser.LooksLikePlaylist(_mediaUri))
        {
            try
            {
                var playlistText = await ReadPlaylistTextAsync(_mediaUri).ConfigureAwait(true);
                foreach (var item in M3uPlaylistParser.Parse(playlistText, _mediaUri))
                {
                    _playlist.Add(new PlayerPlaylistItem(item.Number, item.Title, item.Uri));
                }
            }
            catch (Exception exception)
            {
                FileEventLog.User.Warning("Player", "Failed to load M3U playlist; falling back to the original URL.", exception.Message);
            }
        }

        if (_playlist.Count == 0)
        {
            _playlist.Add(new PlayerPlaylistItem(1, _mediaTitle, _mediaUri));
        }

        RenumberPlaylist();
        UpdatePlaylistButtons();
    }

    private Task<string> ReadPlaylistTextAsync(Uri uri)
    {
        return uri.IsFile
            ? File.ReadAllTextAsync(uri.LocalPath)
            : GetHttpClient().GetStringAsync(uri);
    }

    private void RenumberPlaylist()
    {
        for (var index = 0; index < _playlist.Count; index++)
        {
            _playlist[index].Number = index + 1;
        }
    }

    private void PlayPlaylistIndex(int index)
    {
        _ = PlayPlaylistIndexAsync(index);
    }

    private async Task PlayPlaylistIndexAsync(int index)
    {
        if (_player is null || index < 0 || index >= _playlist.Count)
        {
            return;
        }

        try
        {
            _manualStopRequested = false;
            _hasHandledEnd = false;
            _currentPlaylistIndex = index;
            var item = _playlist[index];
            TitleText.Text = $"{item.NumberText} {item.Title}";
            Title = string.Format(_localization["PlayerWindowTitle"], item.Title);
            StatusText.Text = _localization["PlayerStatusReady"];
            StatusText.Visibility = Visibility.Visible;
            PositionSlider.Value = 0;
            _currentTimeMs = 0;
            _currentLengthMs = 0;
            TimeText.Text = FormatTime(0, 0);
            UpdatePlaylistSelection(index);
            UpdatePlaylistButtons();

            await PlayMediaItemAsync(item).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            SetPlaybackStatus(_localization["PlayerStatusError"]);
            FileEventLog.User.Error("Player", "Failed to open media in built-in mpv player.", exception, _playlist.ElementAtOrDefault(index)?.Uri.ToString() ?? string.Empty);
        }
    }

    private async Task PlayMediaItemAsync(PlayerPlaylistItem item)
    {
        if (_player is null)
        {
            return;
        }

        ResetTrackControls();
        await _player.LoadFileAsync(item.Uri, CancellationToken.None).ConfigureAwait(true);
        await _player.SetVolumeAsync(VolumeSlider.Value, CancellationToken.None).ConfigureAwait(true);
        await ReapplyTrackTimingAsync().ConfigureAwait(true);
        _isPlayerIdle = false;
        _isPlayerPaused = false;
        SetPlaybackStatus(_localization["PlayerStatusPlaying"]);
        StartTrackRefresh();

        FileEventLog.User.Info("Player", "Built-in mpv player opened playlist item.", item.Uri.ToString());
    }

    private void OnPlaylistSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPlaylistSelection || PlaylistList.SelectedItem is not PlayerPlaylistItem item)
        {
            return;
        }

        PlayPlaylistIndex(item.Number - 1);
    }

    private void UpdatePlaylistSelection(int index)
    {
        _isUpdatingPlaylistSelection = true;
        try
        {
            PlaylistList.SelectedIndex = index;
            PlaylistList.ScrollIntoView(_playlist[index]);
        }
        finally
        {
            _isUpdatingPlaylistSelection = false;
        }
    }

    private void OnPrevious(object sender, RoutedEventArgs e)
    {
        PlayPlaylistIndex(_currentPlaylistIndex - 1);
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        PlayPlaylistIndex(_currentPlaylistIndex + 1);
    }

    private void OnMpvEndReached(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_isClosing || _hasHandledEnd)
            {
                return;
            }

            _hasHandledEnd = true;
            OnMediaEnded();
        }));
    }

    private void OnMpvExited(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_isClosing)
            {
                return;
            }

            _isPlayerIdle = true;
            _isPlayerPaused = true;
            SetPlaybackStatus(_localization["PlayerStatusError"]);
            FileEventLog.User.Warning("Player", "mpv process exited unexpectedly.", string.Empty);
        }));
    }

    private void OnMpvTracksChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            if (_isClosing)
            {
                return;
            }

            await RefreshTrackControlsAsync().ConfigureAwait(true);
            if (!_isPlayerIdle)
            {
                _trackRefreshTimer.Start();
            }
        }));
    }

    private void OnMediaEnded()
    {
        if (_isClosing || _manualStopRequested)
        {
            return;
        }

        if (_currentPlaylistIndex + 1 < _playlist.Count)
        {
            PlayPlaylistIndex(_currentPlaylistIndex + 1);
            return;
        }

        _isPlayerIdle = true;
        _isPlayerPaused = true;
        SetPlaybackStatus(_localization["PlayerStatusStopped"]);
    }

    private void UpdatePlaylistButtons()
    {
        PreviousButton.IsEnabled = _currentPlaylistIndex > 0;
        NextButton.IsEnabled = _currentPlaylistIndex >= 0 && _currentPlaylistIndex < _playlist.Count - 1;
        PlaylistHeaderText.Text = string.Format(_localization["PlayerPlaylistWithCount"], _playlist.Count);
    }

    private HttpClient GetHttpClient()
    {
        if (_httpClient is not null)
        {
            return _httpClient;
        }

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        };

        if (_server?.IgnoreCertificateErrors == true)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        if (_server is not null && !string.IsNullOrWhiteSpace(_server.Username))
        {
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(_server.Username + ":" + (_server.Password ?? string.Empty)));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        }

        return _httpClient;
    }

    private void StartTrackRefresh()
    {
        _lastTrackListSignature = null;
        _trackRefreshTimer.Start();
    }

    private async void OnTrackRefreshTimerTick(object? sender, EventArgs e)
    {
        if (_isClosing || _isPlayerIdle)
        {
            _trackRefreshTimer.Stop();
            return;
        }

        if (_isRefreshingTracks)
        {
            return;
        }

        _isRefreshingTracks = true;
        try
        {
            await RefreshTrackControlsAsync().ConfigureAwait(true);
        }
        finally
        {
            _isRefreshingTracks = false;
        }
    }

    private async Task RefreshTrackControlsAsync()
    {
        if (_player is null)
        {
            return;
        }

        IReadOnlyList<MpvTrack> tracks;
        try
        {
            tracks = await _player.GetTracksAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            FileEventLog.User.Warning("Player", "Failed to refresh mpv track list.", exception.Message);
            return;
        }

        var signature = BuildTrackListSignature(tracks);
        if (_lastTrackListSignature is not null &&
            string.Equals(signature, _lastTrackListSignature, StringComparison.Ordinal))
        {
            return;
        }

        _lastTrackListSignature = signature;

        _isUpdatingTrackControls = true;
        try
        {
            SetTrackItems(AudioTrackCombo, tracks.Where(track => track.Type.Equals("audio", StringComparison.OrdinalIgnoreCase)), _localization["PlayerNoAudioTracks"]);
            SetTrackItems(VideoTrackCombo, tracks.Where(track => track.Type.Equals("video", StringComparison.OrdinalIgnoreCase)), _localization["PlayerNoVideoTracks"]);
            SetTrackItems(SubtitleTrackCombo, tracks.Where(track => track.Type.Equals("sub", StringComparison.OrdinalIgnoreCase)), _localization["PlayerNoSubtitleTracks"]);
            FileEventLog.User.Info("Player", "mpv tracks refreshed.", CountTracksByType(tracks));
        }
        finally
        {
            _isUpdatingTrackControls = false;
        }
    }

    private static string BuildTrackListSignature(IReadOnlyList<MpvTrack> tracks)
    {
        return string.Join("|", tracks.Select(track =>
            $"{track.Id}:{track.Type}:{track.Name}:{track.Selected}"));
    }

    private static string CountTracksByType(IReadOnlyList<MpvTrack> tracks)
    {
        var audio = tracks.Count(track => track.Type.Equals("audio", StringComparison.OrdinalIgnoreCase));
        var video = tracks.Count(track => track.Type.Equals("video", StringComparison.OrdinalIgnoreCase));
        var subtitles = tracks.Count(track => track.Type.Equals("sub", StringComparison.OrdinalIgnoreCase));
        return $"audio={audio}; video={video}; subtitles={subtitles}; total={tracks.Count}";
    }

    private void SetTrackItems(
        ComboBox comboBox,
        IEnumerable<MpvTrack> tracks,
        string emptyLabel)
    {
        var trackList = tracks.ToList();
        var items = new List<PlayerOption>();
        if (trackList.Count == 0)
        {
            items.Add(new PlayerOption(-1, emptyLabel));
        }
        else
        {
            items.Add(new PlayerOption(-1, _localization["PlayerTrackDisabled"]));
            items.AddRange(trackList.Select(track => new PlayerOption(track.Id, track.Name)));
        }

        comboBox.ItemsSource = items;
        var selectedTrack = trackList.FirstOrDefault(track => track.Selected);
        comboBox.SelectedItem = selectedTrack is null
            ? items[0]
            : items.FirstOrDefault(item => item.Id == selectedTrack.Id) ?? items[0];
        comboBox.IsEnabled = trackList.Count > 0;
    }

    private void ResetTrackControls()
    {
        _isUpdatingTrackControls = true;
        try
        {
            AudioTrackCombo.ItemsSource = new[] { new PlayerOption(-1, _localization["PlayerTracksLoading"]) };
            VideoTrackCombo.ItemsSource = new[] { new PlayerOption(-1, _localization["PlayerTracksLoading"]) };
            SubtitleTrackCombo.ItemsSource = new[] { new PlayerOption(-1, _localization["PlayerTracksLoading"]) };
            AudioTrackCombo.SelectedIndex = 0;
            VideoTrackCombo.SelectedIndex = 0;
            SubtitleTrackCombo.SelectedIndex = 0;
            AudioTrackCombo.IsEnabled = false;
            VideoTrackCombo.IsEnabled = false;
            SubtitleTrackCombo.IsEnabled = false;
        }
        finally
        {
            _isUpdatingTrackControls = false;
        }
    }

    private void OnAudioTrackSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isUpdatingTrackControls && AudioTrackCombo.SelectedItem is PlayerOption option)
        {
            _ = RunPlayerCommandAsync(() => _player?.SetTrackAsync("aid", option.Id, CancellationToken.None) ?? Task.CompletedTask);
        }
    }

    private void OnVideoTrackSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isUpdatingTrackControls && VideoTrackCombo.SelectedItem is PlayerOption option)
        {
            _ = RunPlayerCommandAsync(() => _player?.SetTrackAsync("vid", option.Id, CancellationToken.None) ?? Task.CompletedTask);
        }
    }

    private void OnSubtitleTrackSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isUpdatingTrackControls && SubtitleTrackCombo.SelectedItem is PlayerOption option)
        {
            _ = RunPlayerCommandAsync(() => _player?.SetTrackAsync("sid", option.Id, CancellationToken.None) ?? Task.CompletedTask);
        }
    }

    private void OnAspectRatioSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AspectRatioCombo.SelectedItem is PlayerOption option)
        {
            _ = RunPlayerCommandAsync(() => _player?.SetAspectRatioAsync(option.Value, CancellationToken.None) ?? Task.CompletedTask);
        }
    }

    private void OnAudioDelayChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        AudioDelayLabel.Text = string.Format(_localization["PlayerAudioDelayWithValue"], (int)e.NewValue);
        _ = RunPlayerCommandAsync(() => _player?.SetAudioDelayAsync(e.NewValue / 1000d, CancellationToken.None) ?? Task.CompletedTask);
    }

    private void OnSubtitleDelayChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        SubtitleDelayLabel.Text = string.Format(_localization["PlayerSubtitleDelayWithValue"], (int)e.NewValue);
        _ = RunPlayerCommandAsync(() => _player?.SetSubtitleDelayAsync(e.NewValue / 1000d, CancellationToken.None) ?? Task.CompletedTask);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _isClosing = true;
        _positionTimer.Stop();
        _trackRefreshTimer.Stop();
        ComponentDispatcher.ThreadFilterMessage -= OnThreadFilterMessage;

        try
        {
            if (_player is not null)
            {
                _player.EndReached -= OnMpvEndReached;
                _player.Exited -= OnMpvExited;
                _player.TracksChanged -= OnMpvTracksChanged;
                _player.Dispose();
            }

            _httpClient?.Dispose();
            MpvHostControl.Child = null;
            _mpvPanel?.Dispose();
        }
        catch (Exception exception)
        {
            FileEventLog.User.Warning("Player", "Built-in player cleanup failed.", exception.Message);
        }
    }

    private async void OnPlayPause(object sender, RoutedEventArgs e)
    {
        if (_player is null)
        {
            return;
        }

        try
        {
            if (_isPlayerIdle)
            {
                _manualStopRequested = false;
                await PlayPlaylistIndexAsync(Math.Max(0, _currentPlaylistIndex)).ConfigureAwait(true);
                return;
            }

            _manualStopRequested = false;
            await _player.SetPauseAsync(!_isPlayerPaused, CancellationToken.None).ConfigureAwait(true);
            _isPlayerPaused = !_isPlayerPaused;
            SetPlaybackStatus(_isPlayerPaused ? _localization["PlayerStatusPaused"] : _localization["PlayerStatusPlaying"]);
        }
        catch (Exception exception)
        {
            FileEventLog.User.Warning("Player", "Failed to toggle mpv playback.", exception.Message);
        }
    }

    private async void OnStop(object sender, RoutedEventArgs e)
    {
        _manualStopRequested = true;
        _hasHandledEnd = true;
        _currentTimeMs = 0;
        _isPlayerPaused = true;
        _isPlayerIdle = true;

        if (_player is not null)
        {
            await RunPlayerCommandAsync(() => _player.StopAsync(CancellationToken.None)).ConfigureAwait(true);
        }

        PositionSlider.Value = 0;
        TimeText.Text = FormatTime(0, _currentLengthMs);
        SetPlaybackStatus(_localization["PlayerStatusStopped"]);
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnFullscreen(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isFullscreen)
        {
            SetFullscreen(false);
            e.Handled = true;
        }
    }

    private void OnVideoHostMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ClickCount >= 2)
        {
            ToggleFullscreenFromPointer();
            e.Handled = true;
        }
    }

    private void OnPlayerRightClick(object sender, MouseButtonEventArgs e)
    {
        OpenPlayerContextMenu(sender as UIElement ?? VideoHost);
        e.Handled = true;
    }

    private void OnNativeVideoMouseDown(object? sender, WinFormsMouseEventArgs e)
    {
        if (e.Button == WinFormsMouseButtons.Left && e.Clicks >= 2)
        {
            Dispatcher.BeginInvoke(new Action(ToggleFullscreenFromPointer));
            return;
        }

        if (e.Button == WinFormsMouseButtons.Right)
        {
            Dispatcher.BeginInvoke(new Action(() => OpenPlayerContextMenu(VideoHost)));
        }
    }

    private void OnPlayerContextMenuOpened(object sender, RoutedEventArgs e)
    {
        BuildPlayerContextMenu();
    }

    private void OnThreadFilterMessage(ref MSG msg, ref bool handled)
    {
        if (!IsNativeVideoMouseMessage(msg.message) || !IsMouseOverVideoHost())
        {
            return;
        }

        if (msg.message == WindowMessageLeftButtonDown)
        {
            if (!IsNativeDoubleClick(msg.lParam))
            {
                return;
            }

            ToggleFullscreenFromPointer();
            handled = true;
            return;
        }

        if (msg.message == WindowMessageLeftDoubleClick)
        {
            ToggleFullscreenFromPointer();
            handled = true;
            return;
        }

        OpenPlayerContextMenu(VideoHost);
        handled = true;
    }

    private static bool IsNativeVideoMouseMessage(int message)
    {
        return message is WindowMessageLeftButtonDown or WindowMessageLeftDoubleClick or WindowMessageRightButtonUp or WindowMessageContextMenu;
    }

    private bool IsNativeDoubleClick(IntPtr lParam)
    {
        var now = Environment.TickCount64;
        var x = GetSignedLowWord(lParam);
        var y = GetSignedHighWord(lParam);
        var isDoubleClick = now - _lastNativeLeftClickTicks <= GetDoubleClickTime() &&
            Math.Abs(x - _lastNativeLeftClickX) <= System.Windows.Forms.SystemInformation.DoubleClickSize.Width &&
            Math.Abs(y - _lastNativeLeftClickY) <= System.Windows.Forms.SystemInformation.DoubleClickSize.Height;

        _lastNativeLeftClickTicks = now;
        _lastNativeLeftClickX = x;
        _lastNativeLeftClickY = y;

        if (isDoubleClick)
        {
            _lastNativeLeftClickTicks = 0;
        }

        return isDoubleClick;
    }

    private static int GetSignedLowWord(IntPtr value)
    {
        return unchecked((short)((long)value & 0xFFFF));
    }

    private static int GetSignedHighWord(IntPtr value)
    {
        return unchecked((short)(((long)value >> 16) & 0xFFFF));
    }

    private bool IsMouseOverVideoHost()
    {
        if (!GetCursorPos(out var point))
        {
            return false;
        }

        var position = VideoHost.PointFromScreen(new System.Windows.Point(point.X, point.Y));
        return position.X >= 0 &&
            position.Y >= 0 &&
            position.X <= VideoHost.ActualWidth &&
            position.Y <= VideoHost.ActualHeight;
    }

    private void OpenPlayerContextMenu(UIElement placementTarget)
    {
        BuildPlayerContextMenu();
        PlayerContextMenu.PlacementTarget = placementTarget;
        PlayerContextMenu.Placement = PlacementMode.MousePoint;
        PlayerContextMenu.IsOpen = true;
    }

    private void OnPositionMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingPosition = true;
    }

    private void OnPositionMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingPosition = false;
        _ = SeekToSliderPositionAsync();
    }

    private void OnPositionValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isDraggingPosition)
        {
            TimeText.Text = FormatTime(SliderPositionToTime(), _currentLengthMs);
        }
    }

    private void OnVolumeValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _ = RunPlayerCommandAsync(() => _player?.SetVolumeAsync(e.NewValue, CancellationToken.None) ?? Task.CompletedTask);
    }

    private async void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (_player is null || _isDraggingPosition || _isClosing || _isPollingPosition)
        {
            return;
        }

        _isPollingPosition = true;
        try
        {
            var state = await _player.GetStateAsync(CancellationToken.None).ConfigureAwait(true);
            _currentTimeMs = state.TimeMs;
            _currentLengthMs = state.DurationMs;
            _isPlayerPaused = state.Paused;
            _isPlayerIdle = state.IdleActive;

            if (state.EofReached && !_manualStopRequested && !_hasHandledEnd)
            {
                _hasHandledEnd = true;
                OnMediaEnded();
                return;
            }

            if (_currentLengthMs > 0)
            {
                PositionSlider.Value = Math.Clamp((double)_currentTimeMs / _currentLengthMs * PositionSlider.Maximum, 0, PositionSlider.Maximum);
            }

            TimeText.Text = FormatTime(_currentTimeMs, _currentLengthMs);
            SetPlaybackStatus(_isPlayerIdle
                ? _localization["PlayerStatusStopped"]
                : _isPlayerPaused
                    ? _localization["PlayerStatusPaused"]
                    : _localization["PlayerStatusPlaying"]);
        }
        catch (Exception exception)
        {
            FileEventLog.User.Warning("Player", "Failed to poll mpv playback state.", exception.Message);
        }
        finally
        {
            _isPollingPosition = false;
        }
    }

    private async Task SeekToSliderPositionAsync()
    {
        if (_player is null || _currentLengthMs <= 0)
        {
            return;
        }

        var target = Math.Clamp(SliderPositionToTime(), 0, Math.Max(0, _currentLengthMs - 1));
        await SeekToTimeAsync(target).ConfigureAwait(true);
    }

    private bool CanSeek()
    {
        return _player is not null && _currentLengthMs > 0;
    }

    private void SeekRelative(long deltaMs)
    {
        if (_player is null || _currentLengthMs <= 0)
        {
            return;
        }

        _ = SeekToTimeAsync(_currentTimeMs + deltaMs);
    }

    private async Task SeekToTimeAsync(long targetMs)
    {
        if (_player is null || _currentLengthMs <= 0)
        {
            return;
        }

        var target = Math.Clamp(targetMs, 0, Math.Max(0, _currentLengthMs - 1));
        try
        {
            await _player.SeekAbsoluteAsync(target, CancellationToken.None).ConfigureAwait(true);
            _currentTimeMs = target;
            PositionSlider.Value = Math.Clamp((double)target / _currentLengthMs * PositionSlider.Maximum, 0, PositionSlider.Maximum);
            TimeText.Text = FormatTime(target, _currentLengthMs);
        }
        catch (Exception exception)
        {
            FileEventLog.User.Warning("Player", "Failed to seek mpv playback.", exception.Message);
        }
    }

    private void ToggleFullscreenFromPointer()
    {
        var now = Environment.TickCount64;
        if (now - _lastPointerFullscreenToggleTicks < 500)
        {
            return;
        }

        _lastPointerFullscreenToggleTicks = now;
        ToggleFullscreen();
    }

    private void ToggleFullscreen()
    {
        SetFullscreen(!_isFullscreen);
    }

    private void SetFullscreen(bool fullscreen)
    {
        if (_isFullscreen == fullscreen)
        {
            return;
        }

        if (fullscreen)
        {
            _windowStateBeforeFullscreen = WindowState;
            _windowStyleBeforeFullscreen = WindowStyle;
            _resizeModeBeforeFullscreen = ResizeMode;
            _topmostBeforeFullscreen = Topmost;
            _sidebarWidthBeforeFullscreen = PlayerSidebarColumn.Width;
            _leftBeforeFullscreen = Left;
            _topBeforeFullscreen = Top;
            _widthBeforeFullscreen = Width;
            _heightBeforeFullscreen = Height;

            _isFullscreen = true;
            PlayerSidebar.Visibility = Visibility.Collapsed;
            PlayerControlsBar.Visibility = Visibility.Collapsed;
            PlayerSidebarColumn.Width = new GridLength(0);
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ApplyFullscreenWindowPlacement();
        }
        else
        {
            _isFullscreen = false;
            PlayerSidebar.Visibility = Visibility.Visible;
            PlayerControlsBar.Visibility = Visibility.Visible;
            PlayerSidebarColumn.Width = _sidebarWidthBeforeFullscreen;
            WindowState = WindowState.Normal;
            WindowStyle = _windowStyleBeforeFullscreen;
            ResizeMode = _resizeModeBeforeFullscreen;
            Topmost = _topmostBeforeFullscreen;
            Left = _leftBeforeFullscreen;
            Top = _topBeforeFullscreen;
            Width = _widthBeforeFullscreen;
            Height = _heightBeforeFullscreen;
            WindowState = _windowStateBeforeFullscreen;
            ApplyRestoredWindowZOrder();
        }

        UpdateFullscreenButton();
    }

    private void ApplyFullscreenWindowPlacement()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var bounds = System.Windows.Forms.Screen.FromHandle(handle).Bounds;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
        Activate();

        if (!SetWindowPos(
            handle,
            HwndTopmost,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            SetWindowPosShowWindow | SetWindowPosFrameChanged))
        {
            FileEventLog.User.Warning("Player", "Failed to apply fullscreen window placement.", Marshal.GetLastWin32Error().ToString());
        }
    }

    private void ApplyRestoredWindowZOrder()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var zOrder = _topmostBeforeFullscreen ? HwndTopmost : HwndNotTopmost;
        if (!SetWindowPos(
            handle,
            zOrder,
            0,
            0,
            0,
            0,
            SetWindowPosNoMove | SetWindowPosNoSize | SetWindowPosShowWindow | SetWindowPosFrameChanged))
        {
            FileEventLog.User.Warning("Player", "Failed to restore window z-order.", Marshal.GetLastWin32Error().ToString());
        }
    }

    private void UpdateFullscreenButton()
    {
        FullscreenButton.Content = _isFullscreen ? IconWindowed : IconFullscreen;
        FullscreenButton.ToolTip = _isFullscreen
            ? _localization["PlayerExitFullscreen"]
            : _localization["PlayerEnterFullscreen"];
    }

    private async Task ReapplyTrackTimingAsync()
    {
        if (_player is null)
        {
            return;
        }

        await _player.SetAudioDelayAsync(AudioDelaySlider.Value / 1000d, CancellationToken.None).ConfigureAwait(true);
        await _player.SetSubtitleDelayAsync(SubtitleDelaySlider.Value / 1000d, CancellationToken.None).ConfigureAwait(true);
    }

    private void BuildPlayerContextMenu()
    {
        var hasPlayer = _player is not null;
        var isPlaying = hasPlayer && !_isPlayerIdle && !_isPlayerPaused;
        PlayerContextMenu.Items.Clear();
        PlayerContextMenu.Items.Add(CreateMenuItem(isPlaying ? _localization["ActionPause"] : _localization["ActionPlay"], OnPlayPause, hasPlayer));
        PlayerContextMenu.Items.Add(CreateMenuItem(_localization["ActionStop"], OnStop, hasPlayer));
        PlayerContextMenu.Items.Add(CreateMenuItem(_localization["PlayerSeekBackward"], (_, _) => SeekRelative(-30000), CanSeek()));
        PlayerContextMenu.Items.Add(CreateMenuItem(_localization["PlayerSeekForward"], (_, _) => SeekRelative(30000), CanSeek()));
        PlayerContextMenu.Items.Add(new Separator());
        PlayerContextMenu.Items.Add(CreateMenuItem(_localization["PlayerPreviousEpisode"], OnPrevious, PreviousButton.IsEnabled));
        PlayerContextMenu.Items.Add(CreateMenuItem(_localization["PlayerNextEpisode"], OnNext, NextButton.IsEnabled));
        PlayerContextMenu.Items.Add(CreatePlaylistMenu());
        PlayerContextMenu.Items.Add(new Separator());
        PlayerContextMenu.Items.Add(CreateMenuItem(_isFullscreen ? _localization["PlayerExitFullscreen"] : _localization["PlayerEnterFullscreen"], OnFullscreen, true));
        PlayerContextMenu.Items.Add(new Separator());
        PlayerContextMenu.Items.Add(CreateTrackMenu(_localization["PlayerAudioTrack"], AudioTrackCombo));
        PlayerContextMenu.Items.Add(CreateTrackMenu(_localization["PlayerVideoTrack"], VideoTrackCombo));
        PlayerContextMenu.Items.Add(CreateTrackMenu(_localization["PlayerSubtitleTrack"], SubtitleTrackCombo));
        PlayerContextMenu.Items.Add(CreateTrackMenu(_localization["PlayerAspectRatio"], AspectRatioCombo));
        PlayerContextMenu.Items.Add(CreateDelayMenu(
            _localization["PlayerAudioDelay"],
            _localization["PlayerAudioDelayDecrease"],
            _localization["PlayerAudioDelayReset"],
            _localization["PlayerAudioDelayIncrease"],
            AudioDelaySlider));
        PlayerContextMenu.Items.Add(CreateDelayMenu(
            _localization["PlayerSubtitleDelay"],
            _localization["PlayerSubtitleDelayDecrease"],
            _localization["PlayerSubtitleDelayReset"],
            _localization["PlayerSubtitleDelayIncrease"],
            SubtitleDelaySlider));
        PlayerContextMenu.Items.Add(CreateVolumeMenu());
        PlayerContextMenu.Items.Add(new Separator());
        PlayerContextMenu.Items.Add(CreateMenuItem(_localization["ActionClose"], OnClose, true));
    }

    private static MenuItem CreateMenuItem(string header, RoutedEventHandler click, bool isEnabled)
    {
        var item = new MenuItem
        {
            Header = header,
            IsEnabled = isEnabled
        };
        item.Click += click;
        return item;
    }

    private MenuItem CreatePlaylistMenu()
    {
        var menu = new MenuItem
        {
            Header = _localization["PlayerPlaylist"],
            IsEnabled = _playlist.Count > 0
        };

        for (var index = 0; index < _playlist.Count; index++)
        {
            var itemIndex = index;
            var playlistItem = _playlist[index];
            var item = new MenuItem
            {
                Header = $"{playlistItem.NumberText} {playlistItem.Title}",
                IsCheckable = true,
                IsChecked = itemIndex == _currentPlaylistIndex
            };
            item.Click += (_, _) => PlayPlaylistIndex(itemIndex);
            menu.Items.Add(item);
        }

        return menu;
    }

    private MenuItem CreateTrackMenu(string header, ComboBox comboBox)
    {
        var menu = new MenuItem
        {
            Header = header,
            IsEnabled = comboBox.Items.Count > 0
        };

        foreach (var option in comboBox.Items.OfType<PlayerOption>())
        {
            var optionItem = option;
            var item = new MenuItem
            {
                Header = option.Name,
                IsEnabled = comboBox.IsEnabled,
                IsCheckable = true,
                IsChecked = IsSelectedOption(comboBox.SelectedItem as PlayerOption, option)
            };
            item.Click += (_, _) => comboBox.SelectedItem = optionItem;
            menu.Items.Add(item);
        }

        return menu;
    }

    private MenuItem CreateDelayMenu(string header, string decreaseText, string resetText, string increaseText, Slider slider)
    {
        var menu = new MenuItem
        {
            Header = header
        };
        menu.Items.Add(CreateMenuItem(decreaseText, (_, _) => AdjustDelay(slider, -250), true));
        menu.Items.Add(CreateMenuItem(resetText, (_, _) => SetDelay(slider, 0), true));
        menu.Items.Add(CreateMenuItem(increaseText, (_, _) => AdjustDelay(slider, 250), true));
        return menu;
    }

    private MenuItem CreateVolumeMenu()
    {
        var menu = new MenuItem
        {
            Header = _localization["FieldVolume"]
        };
        menu.Items.Add(CreateMenuItem(_localization["PlayerVolumeDown"], (_, _) => AdjustVolume(-10), true));
        menu.Items.Add(CreateMenuItem(_localization["PlayerVolumeUp"], (_, _) => AdjustVolume(10), true));
        return menu;
    }

    private static bool IsSelectedOption(PlayerOption? selected, PlayerOption option)
    {
        return selected is not null &&
            selected.Id == option.Id &&
            string.Equals(selected.Value, option.Value, StringComparison.Ordinal);
    }

    private void AdjustDelay(Slider slider, double delta)
    {
        SetDelay(slider, slider.Value + delta);
    }

    private static void SetDelay(Slider slider, double value)
    {
        slider.Value = Math.Clamp(value, slider.Minimum, slider.Maximum);
    }

    private void AdjustVolume(double delta)
    {
        VolumeSlider.Value = Math.Clamp(VolumeSlider.Value + delta, VolumeSlider.Minimum, VolumeSlider.Maximum);
    }

    private long SliderPositionToTime()
    {
        return _currentLengthMs <= 0
            ? 0
            : (long)(_currentLengthMs * Math.Clamp(PositionSlider.Value / PositionSlider.Maximum, 0, 1));
    }

    private void SetPlaybackStatus(string status)
    {
        var isPlaying = _player is not null && !_isPlayerIdle && !_isPlayerPaused;
        StatusText.Text = status;
        StatusText.Visibility = isPlaying
            ? Visibility.Collapsed
            : Visibility.Visible;
        if (_nativeStatusLabel is not null)
        {
            _nativeStatusLabel.Text = status;
            _nativeStatusLabel.Visible = !isPlaying;
        }

        PlayPauseButton.Content = isPlaying ? IconPause : IconPlay;
        PlayPauseButton.ToolTip = isPlaying
            ? _localization["ActionPause"]
            : _localization["ActionPlay"];
    }

    private async Task RunPlayerCommandAsync(Func<Task> command)
    {
        try
        {
            await command().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            FileEventLog.User.Warning("Player", "mpv command failed.", exception.Message);
        }
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

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public readonly int X;

        public readonly int Y;
    }
}

public sealed class PlayerPlaylistItem : INotifyPropertyChanged
{
    private int _number;

    public PlayerPlaylistItem(int number, string title, Uri uri)
    {
        _number = number;
        Title = string.IsNullOrWhiteSpace(title) ? uri.ToString() : title;
        Uri = uri;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Number
    {
        get => _number;
        set
        {
            if (_number == value)
            {
                return;
            }

            _number = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Number)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NumberText)));
        }
    }

    public string NumberText => Number.ToString("00");

    public string Title { get; }

    public Uri Uri { get; }
}

public sealed class PlayerOption
{
    public PlayerOption(int id, string name)
    {
        Id = id;
        Name = name;
        Value = string.Empty;
    }

    public PlayerOption(string value, string name)
    {
        Id = 0;
        Value = value;
        Name = name;
    }

    public int Id { get; }

    public string Value { get; }

    public string Name { get; }

    public override string ToString()
    {
        return Name;
    }
}
