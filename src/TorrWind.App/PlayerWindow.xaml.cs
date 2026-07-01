using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using LibVLCSharp.Shared.Structures;
using TorrWind.Core.Localization;
using TorrWind.Core.Models;
using TorrWind.Core.Services;
using ComboBox = System.Windows.Controls.ComboBox;
using MenuItem = System.Windows.Controls.MenuItem;

namespace TorrWind.App;

public partial class PlayerWindow : Window
{
    private const string IconPlay = "\uE768";
    private const string IconPause = "\uE769";
    private const string IconFullscreen = "\uE740";
    private const string IconWindowed = "\uE73F";
    private readonly Uri _mediaUri;
    private readonly string _mediaTitle;
    private readonly JsonLocalizationService _localization;
    private readonly ServerProfile? _server;
    private readonly DispatcherTimer _positionTimer;
    private readonly DispatcherTimer _trackRefreshTimer;
    private readonly ObservableCollection<PlayerPlaylistItem> _playlist = [];
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private HttpClient? _httpClient;
    private int _currentPlaylistIndex = -1;
    private int _trackRefreshAttempts;
    private bool _isDraggingPosition;
    private bool _isClosing;
    private bool _isUpdatingPlaylistSelection;
    private bool _isUpdatingTrackControls;
    private bool _manualStopRequested;
    private bool _isFullscreen;
    private WindowState _windowStateBeforeFullscreen;
    private WindowStyle _windowStyleBeforeFullscreen;
    private ResizeMode _resizeModeBeforeFullscreen;
    private bool _topmostBeforeFullscreen;
    private GridLength _sidebarWidthBeforeFullscreen;

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

        ConfigureText();
        ConfigureStaticOptions();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LibVLCSharp.Shared.Core.Initialize();
            _libVlc = new LibVLC("--no-video-title-show");
            _mediaPlayer = new MediaPlayer(_libVlc);
            _mediaPlayer.Playing += (_, _) => Dispatcher.Invoke(OnMediaPlaying);
            _mediaPlayer.Paused += (_, _) => Dispatcher.Invoke(() => SetPlaybackStatus(_localization["PlayerStatusPaused"]));
            _mediaPlayer.Stopped += (_, _) => Dispatcher.Invoke(() => SetPlaybackStatus(_localization["PlayerStatusStopped"]));
            _mediaPlayer.EndReached += (_, _) => Dispatcher.BeginInvoke(OnMediaEnded);
            _mediaPlayer.EncounteredError += (_, _) => Dispatcher.Invoke(() => SetPlaybackStatus(_localization["PlayerStatusError"]));
            VideoView.MediaPlayer = _mediaPlayer;

            PlaylistList.ItemsSource = _playlist;
            await LoadPlaylistAsync().ConfigureAwait(true);
            PlayPlaylistIndex(0);
            _positionTimer.Start();
        }
        catch (Exception exception)
        {
            StatusText.Text = exception.Message;
            FileEventLog.User.Error("Player", "Built-in LibVLC player failed to start.", exception, _mediaUri.ToString());
        }
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

        if (LooksLikeM3u(_mediaUri))
        {
            try
            {
                var playlistText = await ReadPlaylistTextAsync(_mediaUri).ConfigureAwait(true);
                foreach (var item in ParseM3u(playlistText, _mediaUri))
                {
                    _playlist.Add(item);
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

    private static bool LooksLikeM3u(Uri uri)
    {
        var path = uri.AbsolutePath;
        return path.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ||
            uri.Query.Contains("m3u", StringComparison.OrdinalIgnoreCase);
    }

    private Task<string> ReadPlaylistTextAsync(Uri uri)
    {
        return uri.IsFile
            ? File.ReadAllTextAsync(uri.LocalPath)
            : GetHttpClient().GetStringAsync(uri);
    }

    private static IEnumerable<PlayerPlaylistItem> ParseM3u(string playlistText, Uri playlistUri)
    {
        var pendingTitle = string.Empty;
        var number = 1;
        using var reader = new StringReader(playlistText);
        while (reader.ReadLine() is { } rawLine)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                var comma = line.IndexOf(',');
                pendingTitle = comma >= 0 && comma < line.Length - 1
                    ? line[(comma + 1)..].Trim()
                    : string.Empty;
                continue;
            }

            if (line.StartsWith('#'))
            {
                continue;
            }

            if (!Uri.TryCreate(line, UriKind.Absolute, out var itemUri) &&
                !Uri.TryCreate(playlistUri, line, out itemUri))
            {
                continue;
            }

            var title = string.IsNullOrWhiteSpace(pendingTitle)
                ? ResolveTitleFromUri(itemUri, number)
                : pendingTitle;
            yield return new PlayerPlaylistItem(number++, title, itemUri);
            pendingTitle = string.Empty;
        }
    }

    private static string ResolveTitleFromUri(Uri uri, int number)
    {
        var fileName = WebUtility.UrlDecode(Path.GetFileName(uri.LocalPath));
        return string.IsNullOrWhiteSpace(fileName)
            ? "Episode " + number
            : fileName;
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
        if (_libVlc is null || _mediaPlayer is null || index < 0 || index >= _playlist.Count)
        {
            return;
        }

        _manualStopRequested = false;
        _currentPlaylistIndex = index;
        var item = _playlist[index];
        TitleText.Text = $"{item.NumberText} {item.Title}";
        Title = string.Format(_localization["PlayerWindowTitle"], item.Title);
        StatusText.Text = _localization["PlayerStatusReady"];
        StatusText.Visibility = Visibility.Visible;
        PositionSlider.Value = 0;
        TimeText.Text = FormatTime(0, 0);
        UpdatePlaylistSelection(index);
        UpdatePlaylistButtons();

        using var media = new Media(_libVlc, item.Uri);
        ApplyMediaOptions(media);
        _mediaPlayer.Play(media);
        _mediaPlayer.Volume = (int)VolumeSlider.Value;
        ResetTrackControls();
        StartTrackRefresh();
        FileEventLog.User.Info("Player", "Built-in player opened playlist item.", item.Uri.ToString());
    }

    private void ApplyMediaOptions(Media media)
    {
        media.AddOption(":http-reconnect");
        media.AddOption(":network-caching=1500");
        media.AddOption(":file-caching=1000");
        media.AddOption(":clock-synchro=1");

        if (_server is not null && !string.IsNullOrWhiteSpace(_server.Username))
        {
            media.AddOption(":http-user=" + _server.Username);
            media.AddOption(":http-pwd=" + (_server.Password ?? string.Empty));
        }
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

    private void OnMediaPlaying()
    {
        SetPlaybackStatus(_localization["PlayerStatusPlaying"]);
        RefreshTrackControls();
        StartTrackRefresh();
    }

    private void StartTrackRefresh()
    {
        _trackRefreshAttempts = 0;
        _trackRefreshTimer.Start();
    }

    private void OnTrackRefreshTimerTick(object? sender, EventArgs e)
    {
        RefreshTrackControls();
        _trackRefreshAttempts++;
        if (_trackRefreshAttempts >= 8)
        {
            _trackRefreshTimer.Stop();
        }
    }

    private void RefreshTrackControls()
    {
        if (_mediaPlayer is null)
        {
            return;
        }

        _isUpdatingTrackControls = true;
        try
        {
            SetTrackItems(AudioTrackCombo, _mediaPlayer.AudioTrackDescription, _mediaPlayer.AudioTrack, _localization["PlayerNoAudioTracks"]);
            SetTrackItems(VideoTrackCombo, _mediaPlayer.VideoTrackDescription, _mediaPlayer.VideoTrack, _localization["PlayerNoVideoTracks"]);
            SetTrackItems(SubtitleTrackCombo, _mediaPlayer.SpuDescription, _mediaPlayer.Spu, _localization["PlayerNoSubtitleTracks"]);
        }
        finally
        {
            _isUpdatingTrackControls = false;
        }
    }

    private static void SetTrackItems(
        ComboBox comboBox,
        IEnumerable<TrackDescription>? descriptions,
        int selectedId,
        string emptyLabel)
    {
        var items = descriptions?
            .Select(track => new PlayerOption(track.Id, string.IsNullOrWhiteSpace(track.Name) ? track.Id.ToString() : track.Name))
            .ToList() ?? [];

        if (items.Count == 0)
        {
            items.Add(new PlayerOption(-1, emptyLabel));
        }

        comboBox.ItemsSource = items;
        comboBox.SelectedItem = items.FirstOrDefault(item => item.Id == selectedId) ?? items.FirstOrDefault();
        comboBox.IsEnabled = items.Count > 1 || items[0].Id >= 0;
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
        }
        finally
        {
            _isUpdatingTrackControls = false;
        }
    }

    private void OnAudioTrackSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isUpdatingTrackControls && _mediaPlayer is not null && AudioTrackCombo.SelectedItem is PlayerOption option)
        {
            _mediaPlayer.SetAudioTrack(option.Id);
        }
    }

    private void OnVideoTrackSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isUpdatingTrackControls && _mediaPlayer is not null && VideoTrackCombo.SelectedItem is PlayerOption option)
        {
            _mediaPlayer.SetVideoTrack(option.Id);
        }
    }

    private void OnSubtitleTrackSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isUpdatingTrackControls && _mediaPlayer is not null && SubtitleTrackCombo.SelectedItem is PlayerOption option)
        {
            _mediaPlayer.SetSpu(option.Id);
        }
    }

    private void OnAspectRatioSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_mediaPlayer is not null && AspectRatioCombo.SelectedItem is PlayerOption option)
        {
            _mediaPlayer.AspectRatio = string.IsNullOrWhiteSpace(option.Value) ? null : option.Value;
        }
    }

    private void OnAudioDelayChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_mediaPlayer is not null)
        {
            _mediaPlayer.SetAudioDelay((long)Math.Round(e.NewValue) * 1000);
            AudioDelayLabel.Text = string.Format(_localization["PlayerAudioDelayWithValue"], (int)e.NewValue);
        }
    }

    private void OnSubtitleDelayChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_mediaPlayer is not null)
        {
            _mediaPlayer.SetSpuDelay((long)Math.Round(e.NewValue) * 1000);
            SubtitleDelayLabel.Text = string.Format(_localization["PlayerSubtitleDelayWithValue"], (int)e.NewValue);
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _isClosing = true;
        _positionTimer.Stop();
        _trackRefreshTimer.Stop();

        try
        {
            if (_mediaPlayer is not null)
            {
                _mediaPlayer.Stop();
                VideoView.MediaPlayer = null;
                _mediaPlayer.Dispose();
            }

            _httpClient?.Dispose();
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

        _manualStopRequested = false;
        _mediaPlayer.Play();
    }

    private void OnStop(object sender, RoutedEventArgs e)
    {
        _manualStopRequested = true;
        _mediaPlayer?.Stop();
        PositionSlider.Value = 0;
        TimeText.Text = FormatTime(0, _mediaPlayer?.Length ?? 0);
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
            ToggleFullscreen();
            e.Handled = true;
        }
    }

    private void OnPlayerRightClick(object sender, MouseButtonEventArgs e)
    {
        BuildPlayerContextMenu();
        PlayerContextMenu.PlacementTarget = sender as UIElement ?? RootLayout;
        PlayerContextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void OnPlayerContextMenuOpened(object sender, RoutedEventArgs e)
    {
        BuildPlayerContextMenu();
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
            PositionSlider.Value = Math.Clamp((double)time / length * PositionSlider.Maximum, 0, PositionSlider.Maximum);
        }

        TimeText.Text = FormatTime(time, length);
    }

    private void SeekToSliderPosition()
    {
        if (_mediaPlayer is null)
        {
            return;
        }

        var length = _mediaPlayer.Length;
        if (length > 0)
        {
            var target = Math.Clamp(SliderPositionToTime(), 0, Math.Max(0, length - 1));
            SeekToTime(target);
            return;
        }

        _mediaPlayer.Position = (float)Math.Clamp(PositionSlider.Value / PositionSlider.Maximum, 0, 1);
        ReapplyTrackTiming();
    }

    private bool CanSeek()
    {
        return _mediaPlayer is not null && _mediaPlayer.Length > 0;
    }

    private void SeekRelative(long deltaMs)
    {
        if (_mediaPlayer is null || _mediaPlayer.Length <= 0)
        {
            return;
        }

        SeekToTime(_mediaPlayer.Time + deltaMs);
    }

    private void SeekToTime(long targetMs)
    {
        if (_mediaPlayer is null)
        {
            return;
        }

        var length = _mediaPlayer.Length;
        if (length <= 0)
        {
            return;
        }

        var target = Math.Clamp(targetMs, 0, Math.Max(0, length - 1));
        _mediaPlayer.Time = target;
        PositionSlider.Value = Math.Clamp((double)target / length * PositionSlider.Maximum, 0, PositionSlider.Maximum);
        TimeText.Text = FormatTime(target, length);
        ReapplyTrackTiming();
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

            _isFullscreen = true;
            PlayerSidebar.Visibility = Visibility.Collapsed;
            PlayerControlsBar.Visibility = Visibility.Collapsed;
            PlayerSidebarColumn.Width = new GridLength(0);
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            WindowState = WindowState.Maximized;
        }
        else
        {
            _isFullscreen = false;
            PlayerSidebar.Visibility = Visibility.Visible;
            PlayerControlsBar.Visibility = Visibility.Visible;
            PlayerSidebarColumn.Width = _sidebarWidthBeforeFullscreen;
            WindowStyle = _windowStyleBeforeFullscreen;
            ResizeMode = _resizeModeBeforeFullscreen;
            Topmost = _topmostBeforeFullscreen;
            WindowState = _windowStateBeforeFullscreen;
        }

        UpdateFullscreenButton();
    }

    private void UpdateFullscreenButton()
    {
        FullscreenButton.Content = _isFullscreen ? IconWindowed : IconFullscreen;
        FullscreenButton.ToolTip = _isFullscreen
            ? _localization["PlayerExitFullscreen"]
            : _localization["PlayerEnterFullscreen"];
    }

    private void ReapplyTrackTiming()
    {
        if (_mediaPlayer is null)
        {
            return;
        }

        _mediaPlayer.SetAudioDelay((long)Math.Round(AudioDelaySlider.Value) * 1000);
        _mediaPlayer.SetSpuDelay((long)Math.Round(SubtitleDelaySlider.Value) * 1000);
    }

    private void BuildPlayerContextMenu()
    {
        PlayerContextMenu.Items.Clear();
        PlayerContextMenu.Items.Add(CreateMenuItem(_mediaPlayer?.IsPlaying == true ? _localization["ActionPause"] : _localization["ActionPlay"], OnPlayPause, _mediaPlayer is not null));
        PlayerContextMenu.Items.Add(CreateMenuItem(_localization["ActionStop"], OnStop, _mediaPlayer is not null));
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
        PlayPauseButton.Content = _mediaPlayer?.IsPlaying == true ? IconPause : IconPlay;
        PlayPauseButton.ToolTip = _mediaPlayer?.IsPlaying == true
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
