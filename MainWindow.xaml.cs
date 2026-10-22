using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;
using TrackType = NovaPlayer.Models.TrackType;
using System.Windows.Shell;
using Microsoft.Win32;
using NovaPlayer.Models;
using NovaPlayer.Services;

namespace NovaPlayer;

public partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, uint action, IntPtr changeInfo);

    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(IntPtr hWnd, bool fAccept);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, System.Text.StringBuilder? lpszFile, uint cch);

    [DllImport("shell32.dll")]
    private static extern void DragFinish(IntPtr hDrop);

    private const uint WM_DROPFILES = 0x0233;
    private const uint WM_COPYDATA = 0x004A;
    private const uint WM_COPYGLOBALDATA = 0x0049;
    private const uint MSGFLT_ALLOW = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;

    private POINT _lastMousePos;
    private DispatcherTimer? _cursorPollTimer;

    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;

    private readonly PlaylistService _playlistService = new();
    private readonly SubtitleService _subtitleService = new();
    private readonly RecentFilesService _recentFilesService = new();

    private bool _isFullscreen = false;
    private Rect _preFullscreenBounds;
    private WindowState _preFullscreenWindowState;
    private ResizeMode _preFullscreenResizeMode;
    private WindowChrome? _savedWindowChrome;

    private DispatcherTimer? _hudTimer;
    private DispatcherTimer? _toastTimer;

    private long _lastSeekTime = -1;
    private long _suppressTimeUpdatesUntil = 0;
    private CancellationTokenSource? _seekCts;
    private readonly object _seekLock = new();
    private long _lastThrottledSeekTime = 0;
    private bool _isRefreshingTracks = false;
    private Point _videoMouseDownPos;
    private bool _isPotentialVideoClick = false;
    private DispatcherTimer? _videoClickTimer;

    private bool _isDraggingSeek = false;
    private bool _isMuted = false;
    private int _volumeBeforeMute = 100;
    private long _currentSubtitleDelayMs = 0;
    private string _currentVideoPath = string.Empty;
    private string _currentMovieHash = string.Empty;
    private string _selectedLanguageFilter = "es";
    private bool _isHudPinned = false;

    private readonly float[] _speeds = [0.25f, 0.5f, 0.75f, 1.0f, 1.25f, 1.5f, 2.0f, 3.0f];
    private int _speedIndex = 3; // 1.0x

    private readonly string[] _aspectRatios = ["Default", "16:9", "4:3", "21:9", "1:1"];
    private int _aspectIndex = 0;

    private bool _isPipMode = false;
    private Rect _prePipBounds;

    public MainWindow()
    {
        InitializeComponent();

        InitLibVLC();
        InitTimers();

        _playlistService.CurrentItemChanged += OnPlaylistItemChanged;
        ListPlaylistItems.ItemsSource = _playlistService.Items;
        TxtPlaylistCount.Text = $"({_playlistService.Items.Count})";

        UpdateRecentFilesUI();

        Loaded += (s, e) =>
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                var filesToOpen = args.Skip(1).Where(File.Exists).ToArray();
                if (filesToOpen.Length > 0)
                {
                    HandleDroppedFiles(filesToOpen);
                }
            }
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).EnsureHandle());
            if (source != null)
            {
                source.AddHook(WndProc);
                IntPtr hwnd = source.Handle;
                ChangeWindowMessageFilterEx(hwnd, WM_DROPFILES, MSGFLT_ALLOW, IntPtr.Zero);
                ChangeWindowMessageFilterEx(hwnd, WM_COPYDATA, MSGFLT_ALLOW, IntPtr.Zero);
                ChangeWindowMessageFilterEx(hwnd, WM_COPYGLOBALDATA, MSGFLT_ALLOW, IntPtr.Zero);
                DragAcceptFiles(hwnd, true);
            }
        }
        catch { }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg == WM_DROPFILES)
        {
            IntPtr hDrop = wParam;
            uint count = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
            if (count > 0)
            {
                var droppedFiles = new List<string>();
                var sb = new System.Text.StringBuilder(260);
                for (uint i = 0; i < count; i++)
                {
                    uint len = DragQueryFile(hDrop, i, sb, (uint)sb.Capacity);
                    if (len > 0)
                    {
                        droppedFiles.Add(sb.ToString());
                    }
                }
                DragFinish(hDrop);
                if (droppedFiles.Count > 0)
                {
                    Dispatcher.Invoke(() => HandleDroppedFiles(droppedFiles.ToArray()));
                    handled = true;
                }
            }
        }
        return IntPtr.Zero;
    }

    private void InitLibVLC()
    {
        try
        {
            Core.Initialize();

            // High-performance options optimized for huge files (15GB+ 4K/HEVC)
            string[] options =
            [
                "--file-caching=3000",
                "--live-caching=3000",
                "--disc-caching=3000",
                "--network-caching=3000",
                "--avcodec-hw=any",
                "--avcodec-threads=0",
                "--avcodec-fast",
                "--no-video-title-show",
                "--sub-autodetect-fuzzy=2",
                "--sub-autodetect-file"
            ];

            _libVLC = new LibVLC(enableDebugLogs: false, options);
            _mediaPlayer = new MediaPlayer(_libVLC);
            _mediaPlayer.EnableMouseInput = false;
            _mediaPlayer.EnableKeyInput = false;
            PlayerVideoView.MediaPlayer = _mediaPlayer;

            // LibVLC Event handlers
            _mediaPlayer.TimeChanged += MediaPlayer_TimeChanged;
            _mediaPlayer.LengthChanged += MediaPlayer_LengthChanged;
            _mediaPlayer.Playing += MediaPlayer_Playing;
            _mediaPlayer.Paused += MediaPlayer_Paused;
            _mediaPlayer.Stopped += MediaPlayer_Stopped;
            _mediaPlayer.EndReached += MediaPlayer_EndReached;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error inicializando el motor de vídeo LibVLC:\n{ex.Message}", "Error de inicialización", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void InitTimers()
    {
        // HUD auto-hide timer (fades out after 5.0 seconds of mouse inactivity)
        _hudTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5.0) };
        _hudTimer.Tick += (s, e) =>
        {
            if (_isHudPinned) return;

            bool isMenuOpen = (TopMainMenu != null && TopMainMenu.Items.OfType<MenuItem>().Any(m => m.IsSubmenuOpen)) ||
                              (PlayerContextMenu != null && PlayerContextMenu.IsOpen);

            if (_mediaPlayer?.IsPlaying == true &&
                !isMenuOpen &&
                SubtitlesFlyout.Visibility != Visibility.Visible &&
                AudioFlyout.Visibility != Visibility.Visible &&
                OnlineSubtitlesDrawer.Visibility != Visibility.Visible &&
                PlaylistDrawer.Visibility != Visibility.Visible)
            {
                FadeElement(TopBar, 0);
                FadeElement(BottomBar, 0);
                Mouse.OverrideCursor = Cursors.None;
            }
        };

        // Native cursor movement detection timer (guarantees HUD wakeup during native HWND video playback)
        _cursorPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _cursorPollTimer.Tick += (s, e) =>
        {
            if (GetCursorPos(out POINT pt))
            {
                if (pt.X != _lastMousePos.X || pt.Y != _lastMousePos.Y)
                {
                    _lastMousePos = pt;
                    if (IsCursorInsideWindow(pt))
                    {
                        ShowHUD();
                    }
                }
            }
        };
        _cursorPollTimer.Start();

        // Notification toast timer
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _toastTimer.Tick += (s, e) =>
        {
            _toastTimer.Stop();
            FadeElement(NotificationToast, 0);
        };

        // Video single-click debounce timer (distinguishes single-click Play/Pause from double-click Fullscreen)
        _videoClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _videoClickTimer.Tick += (s, e) =>
        {
            _videoClickTimer.Stop();
            TogglePlayPause();
        };
    }

    private bool IsCursorInsideWindow(POINT pt)
    {
        try
        {
            Point winPt = PointFromScreen(new Point(pt.X, pt.Y));
            return winPt.X >= 0 && winPt.X <= ActualWidth && winPt.Y >= 0 && winPt.Y <= ActualHeight;
        }
        catch
        {
            return false;
        }
    }

    #region Media Playback & Control

    public void PlayMedia(string filePath)
    {
        if (_libVLC == null || _mediaPlayer == null || !File.Exists(filePath)) return;

        try
        {
            _currentVideoPath = filePath;
            _currentMovieHash = string.Empty;
            Task.Run(() =>
            {
                try
                {
                    _currentMovieHash = VideoHashCalculator.ComputeMovieHash(filePath);
                }
                catch { }
            });

            using var media = new Media(_libVLC, new Uri(filePath));
            media.AddOption(":file-caching=3000");
            media.AddOption(":live-caching=3000");
            media.AddOption(":disc-caching=3000");
            media.AddOption(":network-caching=3000");
            media.AddOption(":avcodec-hw=any");
            media.AddOption(":avcodec-threads=0");
            media.AddOption(":no-video-title-show");
            _mediaPlayer.Play(media);

            WelcomePanel.Visibility = Visibility.Collapsed;
            TxtMediaTitle.Text = Path.GetFileName(filePath);
            _currentSubtitleDelayMs = 0;
            TxtSubtitleDelay.Text = "0 ms";

            _recentFilesService.AddFile(filePath);
            UpdateRecentFilesUI();

            // Update titlebar and info
            string ext = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
            TxtMediaInfo.Text = $"{ext} • HwAcc";
            BadgeMediaInfo.Visibility = Visibility.Visible;

            ShowNotification($"Reproduciendo: {Path.GetFileNameWithoutExtension(filePath)}");

            // Look for matching local subtitles and add to tracks
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                Dispatcher.Invoke(() =>
                {
                    RefreshTracks();
                    AutoLoadLocalSubtitles();
                });
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al reproducir el archivo:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnPlaylistItemChanged(PlaylistItem item)
    {
        Dispatcher.Invoke(() =>
        {
            PlayMedia(item.FilePath);
            TxtPlaylistCount.Text = $"({_playlistService.Items.Count})";
        });
    }

    private void MediaPlayer_Playing(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            PathPlayPauseIcon.Data = (Geometry)FindResource("PathPause");
            PathPlayPauseIcon.Fill = (Brush)FindResource("VoidBrush");
            PathPlayPauseIcon.Margin = new Thickness(0);
            _hudTimer?.Start();
        });
    }

    private void MediaPlayer_Paused(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            PathPlayPauseIcon.Data = (Geometry)FindResource("PathPlay");
            PathPlayPauseIcon.Fill = (Brush)FindResource("VoidBrush");
            PathPlayPauseIcon.Margin = new Thickness(2, 0, 0, 0);
            ShowHUD();
        });
    }

    private void MediaPlayer_Stopped(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            PathPlayPauseIcon.Data = (Geometry)FindResource("PathPlay");
            PathPlayPauseIcon.Fill = (Brush)FindResource("VoidBrush");
            PathPlayPauseIcon.Margin = new Thickness(2, 0, 0, 0);
            SeekSlider.Value = 0;
            TxtCurrentTime.Text = "00:00:00";
            ShowHUD();
        });
    }

    private void MediaPlayer_EndReached(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _playlistService.Next();
        });
    }

    private void MediaPlayer_LengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            TimeSpan duration = TimeSpan.FromMilliseconds(e.Length);
            TxtTotalTime.Text = FormatTime(duration);
            SeekSlider.Maximum = e.Length;
        });
    }

    private void MediaPlayer_TimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
    {
        if (_isDraggingSeek) return;

        long now = Environment.TickCount64;
        if (now < _suppressTimeUpdatesUntil)
        {
            // Drop obsolete time updates arriving from LibVLC's pipeline right after a seek
            if (Math.Abs(e.Time - _lastSeekTime) < 4000)
            {
                _suppressTimeUpdatesUntil = 0; // Seek converged to target!
            }
            else
            {
                return; // Discard pre-seek bounce back
            }
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (_isDraggingSeek) return;
            TimeSpan current = TimeSpan.FromMilliseconds(e.Time);
            TxtCurrentTime.Text = FormatTime(current);
            SeekSlider.Value = e.Time;
        });
    }

    private void TogglePlayPause()
    {
        if (_mediaPlayer == null) return;

        if (_mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Pause();
            ShowNotification("Pausa");
        }
        else if (_mediaPlayer.Media != null)
        {
            _mediaPlayer.Play();
            ShowNotification("Reproduciendo");
        }
        else if (_playlistService.CurrentItem != null)
        {
            PlayMedia(_playlistService.CurrentItem.FilePath);
        }
    }

    private void SeekRelative(int seconds)
    {
        if (_mediaPlayer == null) return;
        long totalLength = _mediaPlayer.Length > 0 ? _mediaPlayer.Length : (long)SeekSlider.Maximum;
        if (totalLength <= 0) return;

        long newTime = _mediaPlayer.Time + (seconds * 1000);
        newTime = Math.Clamp(newTime, 0, totalLength);
        _lastSeekTime = newTime;
        _suppressTimeUpdatesUntil = Environment.TickCount64 + 3000;
        SeekSlider.Value = newTime;
        TxtCurrentTime.Text = FormatTime(TimeSpan.FromMilliseconds(newTime));
        RequestSeek(newTime);

        ShowNotification(seconds > 0 ? $"+{seconds}s ({FormatTime(TimeSpan.FromMilliseconds(newTime))})" : $"{seconds}s ({FormatTime(TimeSpan.FromMilliseconds(newTime))})");
    }

    #endregion

    #region Subtitles & Audio Tracks

    private void RefreshTracks()
    {
        if (_mediaPlayer == null) return;

        _isRefreshingTracks = true;
        try
        {
            // Subtitles Tracks
            ComboSubtitles.Items.Clear();
            ComboSubtitles.Items.Add(new TrackItem { Id = -1, Name = "Desactivado", Type = TrackType.Subtitle });

            var spuDesc = _mediaPlayer.SpuDescription;
            if (spuDesc != null)
            {
                foreach (var spu in spuDesc)
                {
                    if (spu.Id != -1)
                    {
                        ComboSubtitles.Items.Add(new TrackItem
                        {
                            Id = spu.Id,
                            Name = spu.Name,
                            Type = TrackType.Subtitle,
                            IsSelected = (_mediaPlayer.Spu == spu.Id)
                        });
                    }
                }
            }

            // Add local subtitle files discovered next to video
            if (!string.IsNullOrEmpty(_currentVideoPath))
            {
                var locals = _subtitleService.FindLocalSubtitles(_currentVideoPath);
                foreach (var local in locals)
                {
                    ComboSubtitles.Items.Add(local);
                }
            }

            // Select current track
            int currentSpu = _mediaPlayer.Spu;
            var selected = ComboSubtitles.Items.OfType<TrackItem>().FirstOrDefault(t => t.Id == currentSpu) ?? (TrackItem)ComboSubtitles.Items[0];
            ComboSubtitles.SelectedItem = selected;

            // Audio Tracks
            ListAudioTracks.Items.Clear();
            var audioDesc = _mediaPlayer.AudioTrackDescription;
            if (audioDesc != null)
            {
                foreach (var audio in audioDesc)
                {
                    ListAudioTracks.Items.Add(new TrackItem
                    {
                        Id = audio.Id,
                        Name = audio.Id == -1 ? "Desactivado" : audio.Name,
                        Type = TrackType.Audio,
                        IsSelected = (_mediaPlayer.AudioTrack == audio.Id)
                    });
                }
            }
        }
        finally
        {
            _isRefreshingTracks = false;
        }
    }

    private void AutoLoadLocalSubtitles()
    {
        if (string.IsNullOrEmpty(_currentVideoPath) || _mediaPlayer == null) return;

        var locals = _subtitleService.FindLocalSubtitles(_currentVideoPath);
        if (locals.Count > 0 && _mediaPlayer.Spu == -1)
        {
            // Auto load first matching local subtitle
            var sub = locals[0];
            LoadExternalSubtitle(sub.FilePath);
        }
    }

    public void LoadExternalSubtitle(string subtitlePath)
    {
        if (_mediaPlayer == null || !File.Exists(subtitlePath)) return;

        try
        {
            var existingSpus = _mediaPlayer.SpuDescription?.Select(s => s.Id).ToHashSet() ?? new HashSet<int>();

            string uri = new Uri(Path.GetFullPath(subtitlePath)).AbsoluteUri;
            _mediaPlayer.AddSlave(MediaSlaveType.Subtitle, uri, select: true);

            ShowNotification($"Subtítulo cargado: {Path.GetFileName(subtitlePath)}");
            PathSubtitlesIcon.Fill = (Brush)FindResource("AccentBrush");

            // Query SPU tracks after slave is added, and explicitly select the newly added track
            Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(200);
                    var currentTracks = _mediaPlayer?.SpuDescription;
                    if (currentTracks != null && currentTracks.Length > 0)
                    {
                        int targetTrackId = -1;
                        foreach (var t in currentTracks)
                        {
                            if (t.Id != -1 && !existingSpus.Contains(t.Id))
                            {
                                targetTrackId = t.Id;
                                break;
                            }
                        }

                        if (targetTrackId == -1)
                        {
                            var last = currentTracks.LastOrDefault(t => t.Id != -1);
                            if (last.Id != -1) targetTrackId = last.Id;
                        }

                        if (targetTrackId != -1)
                        {
                            try { _mediaPlayer?.SetSpu(targetTrackId); } catch { }
                            break;
                        }
                    }
                }

                _ = Dispatcher.BeginInvoke(RefreshTracks);
            });
        }
        catch (Exception ex)
        {
            ShowNotification($"Error al cargar subtítulo: {ex.Message}");
        }
    }

    private void AdjustSubtitleDelay(long offsetMs)
    {
        if (_mediaPlayer == null) return;

        _currentSubtitleDelayMs += offsetMs;
        // LibVLC uses microseconds for SpuDelay!
        long microSec = _currentSubtitleDelayMs * 1000;
        Task.Run(() => { try { _mediaPlayer?.SetSpuDelay(microSec); } catch { } });

        string sign = _currentSubtitleDelayMs > 0 ? "+" : "";
        TxtSubtitleDelay.Text = $"{sign}{_currentSubtitleDelayMs} ms";
        ShowNotification($"Sincronización subtítulos: {sign}{_currentSubtitleDelayMs} ms");
    }

    private void ResetSubtitleDelay()
    {
        if (_mediaPlayer == null) return;
        _currentSubtitleDelayMs = 0;
        Task.Run(() => { try { _mediaPlayer?.SetSpuDelay(0); } catch { } });
        TxtSubtitleDelay.Text = "0 ms";
        ShowNotification("Sincronización de subtítulos restablecida (0 ms)");
    }

    #endregion

    #region Online Subtitles Search & Download

    private async void SearchOnlineSubtitles()
    {
        string query = TxtSearchSubtitlesQuery.Text.Trim();
        if (string.IsNullOrWhiteSpace(query) && !string.IsNullOrWhiteSpace(_currentVideoPath))
        {
            query = VideoHashCalculator.CleanTitle(_currentVideoPath);
            TxtSearchSubtitlesQuery.Text = query;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            ShowNotification("Introduce un título para buscar subtítulos");
            return;
        }

        SubSearchLoadingIndicator.Visibility = Visibility.Visible;
        TxtNoSubResults.Visibility = Visibility.Collapsed;
        ListOnlineSubtitles.ItemsSource = null;

        TxtHashStatus.Text = !string.IsNullOrEmpty(_currentMovieHash) 
            ? $"Hash de vídeo: {_currentMovieHash.Substring(0, Math.Min(8, _currentMovieHash.Length))}... (Coincidencia exacta)" 
            : "Búsqueda por título y release";

        try
        {
            var results = await _subtitleService.SearchSubtitlesAsync(query, _currentMovieHash, _selectedLanguageFilter);
            SubSearchLoadingIndicator.Visibility = Visibility.Collapsed;

            if (results.Count > 0)
            {
                ListOnlineSubtitles.ItemsSource = results;
                TxtNoSubResults.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtNoSubResults.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            SubSearchLoadingIndicator.Visibility = Visibility.Collapsed;
            TxtNoSubResults.Text = $"Error en la búsqueda: {ex.Message}";
            TxtNoSubResults.Visibility = Visibility.Visible;
        }
    }

    private async void BtnDownloadSubtitleItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is SubtitleSearchResult item)
        {
            btn.IsEnabled = false;
            btn.Content = "Descargando...";

            try
            {
                string localPath = await _subtitleService.DownloadSubtitleAsync(item);
                LoadExternalSubtitle(localPath);

                btn.Content = "✓ Aplicado";
                btn.Background = (Brush)FindResource("SuccessBrush");
                ShowNotification($"Subtítulo online aplicado: {item.LanguageName} ({item.Provider})");
            }
            catch (Exception ex)
            {
                btn.Content = "Error";
                btn.IsEnabled = true;
                ShowNotification($"Error al descargar subtítulo: {ex.Message}");
            }
        }
    }

    #endregion

    #region Window Controls & UI Helpers

    private void ShowHUD()
    {
        if (_isHudPinned)
        {
            TopBar.BeginAnimation(OpacityProperty, null);
            BottomBar.BeginAnimation(OpacityProperty, null);
            TopBar.Opacity = 1;
            BottomBar.Opacity = 1;
            TopBar.Visibility = Visibility.Visible;
            BottomBar.Visibility = Visibility.Visible;
            Mouse.OverrideCursor = null;
            _hudTimer?.Stop();
            return;
        }

        FadeElement(TopBar, 1);
        FadeElement(BottomBar, 1);
        Mouse.OverrideCursor = null;
        _hudTimer?.Stop();
        if (_mediaPlayer?.IsPlaying == true)
        {
            _hudTimer?.Start();
        }
    }

    private void ShowNotification(string message)
    {
        TxtNotification.Text = message;
        FadeElement(NotificationToast, 1);
        _toastTimer?.Stop();
        _toastTimer?.Start();
    }

    private void FadeElement(UIElement element, double targetOpacity)
    {
        var anim = new DoubleAnimation
        {
            To = targetOpacity,
            Duration = TimeSpan.FromMilliseconds(250),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        element.BeginAnimation(OpacityProperty, anim);
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.Hours > 0 
            ? time.ToString(@"h\:mm\:ss") 
            : time.ToString(@"mm\:ss");
    }

    private void SetHudPinned(bool pinned)
    {
        _isHudPinned = pinned;

        if (MenuPinHud != null) MenuPinHud.IsChecked = _isHudPinned;
        if (MenuAutoHud != null) MenuAutoHud.IsChecked = !_isHudPinned;
        if (ContextPinHud != null) ContextPinHud.IsChecked = _isHudPinned;
        if (ContextAutoHud != null) ContextAutoHud.IsChecked = !_isHudPinned;

        if (PathPinHudIcon != null)
        {
            PathPinHudIcon.Data = (Geometry)FindResource(_isHudPinned ? "PathLock" : "PathLockOpen");
        }

        if (BtnPinHud != null)
        {
            BtnPinHud.ToolTip = _isHudPinned
                ? "Barras fijas (clic para ocultar automáticamente tras 5s)"
                : "Barras automáticas (clic para fijar siempre visibles)";
            BtnPinHud.Foreground = _isHudPinned
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("TextSecondaryBrush");
        }

        if (_isHudPinned)
        {
            _hudTimer?.Stop();
            TopBar.BeginAnimation(OpacityProperty, null);
            BottomBar.BeginAnimation(OpacityProperty, null);
            TopBar.Opacity = 1;
            BottomBar.Opacity = 1;
            TopBar.Visibility = Visibility.Visible;
            BottomBar.Visibility = Visibility.Visible;
            Mouse.OverrideCursor = null;
            ShowNotification("Barras fijadas (siempre visibles)");
        }
        else
        {
            ShowNotification("Barras automáticas (se ocultan tras 5s de inactividad)");
            ShowHUD();
        }
    }

    private void TogglePinHud_Click(object sender, RoutedEventArgs e)
    {
        SetHudPinned(!_isHudPinned);
    }

    private void ToggleAutoHud_Click(object sender, RoutedEventArgs e)
    {
        SetHudPinned(false);
    }

    #endregion

    #region Event Handlers (UI Interactions)

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        ShowHUD();
    }

    private void PerformWindowDrag(MouseButtonEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            Point screenPoint = PointToScreen(e.GetPosition(this));
            double percentX = e.GetPosition(this).X / Math.Max(1, ActualWidth);

            WindowState = WindowState.Normal;
            PathMaxIcon.Data = (Geometry)FindResource("PathMaximize");

            Left = screenPoint.X - (Width * (double.IsNaN(percentX) || percentX <= 0 || percentX >= 1 ? 0.5 : percentX));
            Top = Math.Max(0, screenPoint.Y - 20);
        }

        try
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            ReleaseCapture();
            SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HT_CAPTION, IntPtr.Zero);
        }
        catch
        {
            try { DragMove(); } catch { }
        }
    }

    private void TopBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        if (dep != null && IsInteractiveControl(dep))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            BtnMaximize_Click(this, new RoutedEventArgs());
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            PerformWindowDrag(e);
        }
    }

    private void OverlayRoot_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        ShowHUD();

        if (_isPotentialVideoClick && e.LeftButton == MouseButtonState.Pressed)
        {
            Point current = e.GetPosition(OverlayRoot);
            if (Math.Abs(current.X - _videoMouseDownPos.X) > 8 || Math.Abs(current.Y - _videoMouseDownPos.Y) > 8)
            {
                _isPotentialVideoClick = false;
                _videoClickTimer?.Stop();
            }
        }
    }

    private void OverlayRoot_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        if (dep != null && IsInteractiveControl(dep))
        {
            return;
        }

        if (IsDescendantOf(dep, TopBar) || IsDescendantOf(dep, BottomBar))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            _videoClickTimer?.Stop();
            _isPotentialVideoClick = false;
            ToggleFullscreen();
            e.Handled = true;
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _videoMouseDownPos = e.GetPosition(OverlayRoot);
            _isPotentialVideoClick = true;
            Focus();
        }
    }

    private void OverlayRoot_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPotentialVideoClick)
        {
            _isPotentialVideoClick = false;
            _videoClickTimer?.Stop();
            _videoClickTimer?.Start();
        }
    }

    private void OverlayRoot_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        if (dep is System.Windows.Controls.Primitives.TextBoxBase)
        {
            return;
        }

        if (ContextPinHud != null) ContextPinHud.IsChecked = _isHudPinned;
        if (ContextAutoHud != null) ContextAutoHud.IsChecked = !_isHudPinned;
        if (ContextPinOnTop != null) ContextPinOnTop.IsChecked = Topmost;

        ShowHUD();

        if (PlayerContextMenu != null)
        {
            PlayerContextMenu.PlacementTarget = OverlayRoot;
            PlayerContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            PlayerContextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        if (dep != null && IsInteractiveControl(dep))
        {
            return;
        }

        if (IsDescendantOf(dep, TopBar))
        {
            if (e.ClickCount == 2)
            {
                BtnMaximize_Click(this, new RoutedEventArgs());
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                PerformWindowDrag(e);
            }
        }
    }

    private static bool IsInteractiveControl(DependencyObject? dep)
    {
        while (dep != null && dep is not Window)
        {
            if (dep is System.Windows.Controls.Primitives.ButtonBase ||
                dep is System.Windows.Controls.Primitives.ScrollBar ||
                dep is System.Windows.Controls.Primitives.TextBoxBase ||
                dep is Slider ||
                dep is Menu ||
                dep is MenuItem ||
                dep is ContextMenu ||
                dep is ComboBox ||
                dep is ListBoxItem ||
                dep is PasswordBox)
            {
                return true;
            }

            if (dep is FrameworkElement fe &&
                (fe.Name is "PlaylistDrawer" or "OnlineSubtitlesDrawer" or "SubtitlesFlyout" or "AudioFlyout"))
            {
                return true;
            }

            dep = GetParentSafe(dep);
        }
        return false;
    }

    private static bool IsDescendantOf(DependencyObject? dep, DependencyObject? target)
    {
        if (target == null) return false;
        while (dep != null && dep is not Window)
        {
            if (dep == target) return true;
            dep = GetParentSafe(dep);
        }
        return false;
    }

    private static DependencyObject? GetParentSafe(DependencyObject dep)
    {
        if (dep is Visual || dep is System.Windows.Media.Media3D.Visual3D)
        {
            return VisualTreeHelper.GetParent(dep) ?? LogicalTreeHelper.GetParent(dep);
        }
        return LogicalTreeHelper.GetParent(dep);
    }

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    public void HandleDroppedFiles(string[] files)
    {
        if (files == null || files.Length == 0) return;

        string first = files[0];
        string ext = Path.GetExtension(first).ToLowerInvariant();

        // If dropping subtitle file
        if (ext is ".srt" or ".vtt" or ".ass" or ".ssa" or ".sub")
        {
            LoadExternalSubtitle(first);
        }
        else
        {
            // Video file(s): add all to playlist and start playback of the first immediately
            _playlistService.AddFiles(files);
            PlayMedia(first);
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        try
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var data = e.Data.GetData(DataFormats.FileDrop);
                string[]? files = data as string[];
                if (files == null && data is IEnumerable<string> enumerable)
                {
                    files = enumerable.ToArray();
                }

                if (files != null && files.Length > 0)
                {
                    HandleDroppedFiles(files);
                }
            }
        }
        catch (Exception ex)
        {
            ShowNotification($"Error al abrir archivo arrastrado: {ex.Message}");
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        ProcessKeyboardInput(e);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        ProcessKeyboardInput(e);
    }

    private void ProcessKeyboardInput(KeyEventArgs e)
    {
        if (e.Handled) return;

        // Check real key in case e.Key is Key.System
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Don't intercept when user is typing in a search box or text field
        if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase || (TxtSearchSubtitlesQuery != null && TxtSearchSubtitlesQuery.IsFocused))
        {
            return;
        }

        switch (key)
        {
            case Key.Space or Key.K:
                TogglePlayPause();
                e.Handled = true;
                break;
            case Key.Left:
                SeekRelative(-10);
                e.Handled = true;
                break;
            case Key.Right:
                SeekRelative(10);
                e.Handled = true;
                break;
            case Key.Up:
                AdjustVolume(1);
                e.Handled = true;
                break;
            case Key.Down:
                AdjustVolume(-1);
                e.Handled = true;
                break;
            case Key.M:
                BtnVolume_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.F or Key.F11:
                ToggleFullscreen();
                e.Handled = true;
                break;
            case Key.Escape:
                if (_isFullscreen)
                {
                    ToggleFullscreen();
                    e.Handled = true;
                    return;
                }
                if (WindowState == WindowState.Maximized)
                {
                    WindowState = WindowState.Normal;
                }
                SubtitlesFlyout.Visibility = Visibility.Collapsed;
                AudioFlyout.Visibility = Visibility.Collapsed;
                OnlineSubtitlesDrawer.Visibility = Visibility.Collapsed;
                PlaylistDrawer.Visibility = Visibility.Collapsed;
                e.Handled = true;
                break;
            case Key.G:
                AdjustSubtitleDelay(-50);
                e.Handled = true;
                break;
            case Key.H:
                AdjustSubtitleDelay(50);
                e.Handled = true;
                break;
            case Key.S:
                BtnSnapshot_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.P:
                BtnPlaylistToggle_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.O when Keyboard.IsKeyDown(Key.LeftCtrl):
                BtnOpenFile_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
        }
    }

    #endregion

    #region Button Click Handlers

    private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Seleccionar archivo de vídeo",
            Filter = "Archivos de vídeo|*.mkv;*.mp4;*.avi;*.webm;*.mov;*.flv;*.wmv;*.ts;*.m2ts;*.vob;*.3gp|Todos los archivos (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            _playlistService.AddFile(dlg.FileName, playImmediately: true);
        }
    }

    private void BtnPlayPause_Click(object sender, RoutedEventArgs e) => TogglePlayPause();

    private void BtnRewind10_Click(object sender, RoutedEventArgs e) => SeekRelative(-10);

    private void BtnForward10_Click(object sender, RoutedEventArgs e) => SeekRelative(10);

    private void BtnPrevious_Click(object sender, RoutedEventArgs e) => _playlistService.Previous();

    private void BtnNext_Click(object sender, RoutedEventArgs e) => _playlistService.Next();

    private void BtnFrameStep_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaPlayer == null) return;
        _mediaPlayer.NextFrame();
        ShowNotification("Fotograma siguiente");
    }

    private void BtnVolume_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaPlayer == null) return;

        if (_isMuted)
        {
            _isMuted = false;
            VolumeSlider.Value = _volumeBeforeMute;
            _mediaPlayer.Volume = _volumeBeforeMute;
            PathVolumeIcon.Data = (Geometry)FindResource("PathVolumeHigh");
            ShowNotification($"Sonido activado ({_volumeBeforeMute}%)");
        }
        else
        {
            _isMuted = true;
            _volumeBeforeMute = (int)VolumeSlider.Value;
            VolumeSlider.Value = 0;
            _mediaPlayer.Volume = 0;
            PathVolumeIcon.Data = (Geometry)FindResource("PathVolumeMute");
            ShowNotification("Silenciado");
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_mediaPlayer == null) return;

        int vol = (int)e.NewValue;
        _mediaPlayer.Volume = vol;
        if (TxtVolumeLevel != null) TxtVolumeLevel.Text = $"{vol}%";

        if (PathVolumeIcon != null)
        {
            if (vol == 0)
                PathVolumeIcon.Data = (Geometry)FindResource("PathVolumeMute");
            else if (vol < 60)
                PathVolumeIcon.Data = (Geometry)FindResource("PathVolumeLow");
            else
                PathVolumeIcon.Data = (Geometry)FindResource("PathVolumeHigh");
        }

        _isMuted = (vol == 0);
    }

    private void RequestSeek(long targetMs)
    {
        if (_mediaPlayer == null) return;

        lock (_seekLock)
        {
            _seekCts?.Cancel();
            _seekCts = new CancellationTokenSource();
            var ct = _seekCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(35, ct);
                    if (ct.IsCancellationRequested) return;

                    if (_mediaPlayer != null)
                    {
                        _mediaPlayer.Time = targetMs;
                    }
                }
                catch (OperationCanceledException) { }
                catch { }
            }, ct);
        }
    }

    private void RequestThrottledSeek(long targetMs)
    {
        long now = Environment.TickCount64;
        if (now - _lastThrottledSeekTime > 150)
        {
            _lastThrottledSeekTime = now;
            RequestSeek(targetMs);
        }
    }

    private void SeekSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        _isDraggingSeek = true;
        SeekSlider.CaptureMouse();

        if (SeekSlider.ActualWidth > 0 && _mediaPlayer != null)
        {
            Point pt = e.GetPosition(SeekSlider);
            double ratio = Math.Clamp(pt.X / SeekSlider.ActualWidth, 0.0, 1.0);
            long targetVal = (long)(SeekSlider.Minimum + ratio * (SeekSlider.Maximum - SeekSlider.Minimum));

            SeekSlider.Value = targetVal;
            TxtCurrentTime.Text = FormatTime(TimeSpan.FromMilliseconds(targetVal));
            _lastSeekTime = targetVal;
            _suppressTimeUpdatesUntil = Environment.TickCount64 + 3000;

            RequestSeek(targetVal);
        }
        e.Handled = true;
    }

    private void SeekSlider_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingSeek && e.LeftButton == MouseButtonState.Pressed && SeekSlider.ActualWidth > 0 && _mediaPlayer != null)
        {
            Point pt = e.GetPosition(SeekSlider);
            double ratio = Math.Clamp(pt.X / SeekSlider.ActualWidth, 0.0, 1.0);
            long targetVal = (long)(SeekSlider.Minimum + ratio * (SeekSlider.Maximum - SeekSlider.Minimum));

            SeekSlider.Value = targetVal;
            TxtCurrentTime.Text = FormatTime(TimeSpan.FromMilliseconds(targetVal));
            _lastSeekTime = targetVal;
            _suppressTimeUpdatesUntil = Environment.TickCount64 + 3000;

            RequestThrottledSeek(targetVal);
            e.Handled = true;
        }
    }

    private void SeekSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingSeek)
        {
            _isDraggingSeek = false;
            SeekSlider.ReleaseMouseCapture();

            if (SeekSlider.ActualWidth > 0 && _mediaPlayer != null)
            {
                Point pt = e.GetPosition(SeekSlider);
                double ratio = Math.Clamp(pt.X / SeekSlider.ActualWidth, 0.0, 1.0);
                long targetVal = (long)(SeekSlider.Minimum + ratio * (SeekSlider.Maximum - SeekSlider.Minimum));

                SeekSlider.Value = targetVal;
                TxtCurrentTime.Text = FormatTime(TimeSpan.FromMilliseconds(targetVal));
                _lastSeekTime = targetVal;
                _suppressTimeUpdatesUntil = Environment.TickCount64 + 3000;

                RequestSeek(targetVal);
            }
            e.Handled = true;
        }
    }

    private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isDraggingSeek && _mediaPlayer != null)
        {
            TxtCurrentTime.Text = FormatTime(TimeSpan.FromMilliseconds(e.NewValue));
        }
    }

    private void BtnSpeed_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaPlayer == null) return;

        _speedIndex = (_speedIndex + 1) % _speeds.Length;
        float speed = _speeds[_speedIndex];
        Task.Run(() => { try { _mediaPlayer?.SetRate(speed); } catch { } });
        TxtSpeed.Text = $"{speed:0.0#}x";
        ShowNotification($"Velocidad: {speed:0.0#}x");
    }

    private void BtnAspectRatio_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaPlayer == null) return;

        _aspectIndex = (_aspectIndex + 1) % _aspectRatios.Length;
        string aspect = _aspectRatios[_aspectIndex];
        Task.Run(() => { try { if (_mediaPlayer != null) _mediaPlayer.AspectRatio = aspect == "Default" ? null : aspect; } catch { } });
        ShowNotification($"Relación de aspecto: {aspect}");
    }

    private void FlashShutterEffect()
    {
        if (SnapshotFlash == null) return;
        var anim = new DoubleAnimation
        {
            From = 0.7,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        SnapshotFlash.BeginAnimation(OpacityProperty, anim);
    }

    private void BtnSnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaPlayer == null || string.IsNullOrEmpty(_currentVideoPath))
        {
            ShowNotification("No hay ningún vídeo cargado");
            return;
        }

        try
        {
            string pics = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string folder = Path.Combine(pics, "NovaPlayer");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string fileName = $"Snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string file = Path.Combine(folder, fileName);

            FlashShutterEffect();
            _mediaPlayer.TakeSnapshot(0, file, 0, 0);

            ShowNotification($"📸 Captura guardada en:\nImágenes\\NovaPlayer\\{fileName}");

            Task.Run(async () =>
            {
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(150);
                    if (File.Exists(file))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                                bmp.BeginInit();
                                bmp.UriSource = new Uri(file);
                                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                                bmp.EndInit();
                                bmp.Freeze();
                                Clipboard.SetImage(bmp);
                                ShowNotification($"📸 Captura guardada y copiada al portapapeles:\n{fileName}");
                            }
                            catch { }
                        });
                        break;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            ShowNotification($"Error al guardar captura: {ex.Message}");
        }
    }

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            _preFullscreenBounds = new Rect(Left, Top, Width, Height);
            _preFullscreenWindowState = WindowState;
            _preFullscreenResizeMode = ResizeMode;
            _savedWindowChrome = WindowChrome.GetWindowChrome(this);

            WindowChrome.SetWindowChrome(this, null);
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Normal;

            IntPtr hwnd = new WindowInteropHelper(this).EnsureHandle();
            IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

            if (GetMonitorInfo(hMonitor, ref mi))
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                Left = mi.rcMonitor.left / dpi.DpiScaleX;
                Top = mi.rcMonitor.top / dpi.DpiScaleY;
                Width = (mi.rcMonitor.right - mi.rcMonitor.left) / dpi.DpiScaleX;
                Height = (mi.rcMonitor.bottom - mi.rcMonitor.top) / dpi.DpiScaleY;
            }
            else
            {
                Left = 0;
                Top = 0;
                Width = SystemParameters.PrimaryScreenWidth;
                Height = SystemParameters.PrimaryScreenHeight;
            }

            Topmost = true; // Fully covers the Windows taskbar
            _isFullscreen = true;

            PathFullscreenIcon.Data = (Geometry)FindResource("PathFullscreenExit");
            ShowNotification("Pantalla completa");
        }
        else
        {
            _isFullscreen = false;
            Topmost = MenuPinOnTop?.IsChecked == true;

            WindowChrome.SetWindowChrome(this, _savedWindowChrome ?? new WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(6),
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(0)
            });

            ResizeMode = _preFullscreenResizeMode;
            WindowState = _preFullscreenWindowState;
            Left = _preFullscreenBounds.Left;
            Top = _preFullscreenBounds.Top;
            Width = _preFullscreenBounds.Width;
            Height = _preFullscreenBounds.Height;

            PathFullscreenIcon.Data = (Geometry)FindResource("PathFullscreenEnter");
            ShowNotification("Pantalla completa desactivada");
        }
    }

    private void BtnFullscreen_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    private void BtnPip_Click(object sender, RoutedEventArgs e)
    {
        if (!_isPipMode)
        {
            // Enter PiP (Mini-Player)
            _prePipBounds = new Rect(Left, Top, Width, Height);
            Topmost = true;
            Width = 380;
            Height = 220;
            Left = SystemParameters.WorkArea.Right - Width - 20;
            Top = SystemParameters.WorkArea.Bottom - Height - 20;
            _isPipMode = true;
            ShowNotification("Modo Mini-Reproductor (PiP)");
        }
        else
        {
            // Restore from PiP
            Topmost = false;
            Left = _prePipBounds.Left;
            Top = _prePipBounds.Top;
            Width = _prePipBounds.Width;
            Height = _prePipBounds.Height;
            _isPipMode = false;
            ShowNotification("Modo normal");
        }
    }

    private void BtnPinOnTop_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        if (BtnPinOnTop != null)
            BtnPinOnTop.Foreground = Topmost ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("TextSecondaryBrush");
        if (MenuPinOnTop != null)
            MenuPinOnTop.IsChecked = Topmost;
        if (ContextPinOnTop != null)
            ContextPinOnTop.IsChecked = Topmost;

        ShowNotification(Topmost ? "Fijado en primer plano (Always on top)" : "Primer plano desactivado");
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            PathMaxIcon.Data = (Geometry)FindResource("PathMaximize");
        }
        else
        {
            WindowState = WindowState.Maximized;
            PathMaxIcon.Data = (Geometry)FindResource("PathRestore");
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        _mediaPlayer?.Stop();
        Close();
    }

    #endregion

    #region Top Menu & Context Menu Handlers

    private void OverlayRoot_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (ContextPinHud != null) ContextPinHud.IsChecked = _isHudPinned;
        if (ContextAutoHud != null) ContextAutoHud.IsChecked = !_isHudPinned;
        if (ContextPinOnTop != null) ContextPinOnTop.IsChecked = Topmost;

        ShowHUD();
    }

    private void MenuStop_Click(object sender, RoutedEventArgs e)
    {
        _mediaPlayer?.Stop();
        ShowNotification("Detenido");
    }

    private void AdjustVolume(int delta)
    {
        if (_mediaPlayer == null) return;

        int current = _isMuted ? 0 : (int)VolumeSlider.Value;
        if (_isMuted && delta > 0)
        {
            _isMuted = false;
            current = _volumeBeforeMute;
        }

        int newVol = Math.Clamp(current + delta, 0, 200);
        VolumeSlider.Value = newVol;
        _mediaPlayer.Volume = newVol;

        if (newVol > 0)
        {
            _isMuted = false;
        }

        ShowNotification($"Volumen: {newVol}%");
        ShowHUD();
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        if (IsInsideScrollableControl(dep))
        {
            return;
        }

        int step = e.Delta > 0 ? 5 : -5;
        AdjustVolume(step);
        e.Handled = true;
    }

    private static bool IsInsideScrollableControl(DependencyObject? dep)
    {
        while (dep != null && dep is not Window)
        {
            if (dep is ScrollViewer ||
                dep is ListBox ||
                dep is ListView ||
                dep is System.Windows.Controls.Primitives.TextBoxBase ||
                dep is ComboBox)
            {
                return true;
            }

            if (dep is FrameworkElement fe &&
                (fe.Name is "PlaylistDrawer" or "OnlineSubtitlesDrawer" or "ListPlaylistItems" or "ListAudioTracks" or "ListSubResults"))
            {
                return true;
            }

            dep = GetParentSafe(dep);
        }
        return false;
    }

    private void MenuVolUp_Click(object sender, RoutedEventArgs e) => AdjustVolume(1);

    private void MenuVolDown_Click(object sender, RoutedEventArgs e) => AdjustVolume(-1);

    private void BtnSubDelay_Plus50_Click(object sender, RoutedEventArgs e) => AdjustSubtitleDelay(50);
    private void BtnSubDelay_Minus50_Click(object sender, RoutedEventArgs e) => AdjustSubtitleDelay(-50);

    private void MenuSpeed_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string speedStr && float.TryParse(speedStr, System.Globalization.CultureInfo.InvariantCulture, out float rate))
        {
            if (_mediaPlayer == null) return;
            Task.Run(() => { try { _mediaPlayer?.SetRate(rate); } catch { } });
            TxtSpeed.Text = $"{rate:0.0#}x";
            ShowNotification($"Velocidad: {rate:0.0#}x");
        }
    }

    private void MenuAspect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string aspect)
        {
            if (_mediaPlayer == null) return;
            Task.Run(() => { try { if (_mediaPlayer != null) _mediaPlayer.AspectRatio = aspect == "Default" ? null : aspect; } catch { } });
            ShowNotification($"Relación de aspecto: {aspect}");
        }
    }

    private void PopulateAudioTracksMenu(MenuItem targetSubmenu)
    {
        if (targetSubmenu == null || _mediaPlayer == null) return;
        targetSubmenu.Items.Clear();

        var audioDesc = _mediaPlayer.AudioTrackDescription;
        if (audioDesc == null || !audioDesc.Any())
        {
            targetSubmenu.Items.Add(new MenuItem { Header = "Sin pistas de audio disponibles", IsEnabled = false });
            return;
        }

        int currentAudio = _mediaPlayer.AudioTrack;
        foreach (var audio in audioDesc)
        {
            var item = new MenuItem
            {
                Header = audio.Id == -1 ? "Desactivado" : audio.Name,
                IsCheckable = true,
                IsChecked = (currentAudio == audio.Id),
                Tag = audio.Id
            };
            item.Click += (s, args) =>
            {
                if (s is MenuItem clickedItem && clickedItem.Tag is int trackId)
                {
                    Task.Run(() => { try { _mediaPlayer?.SetAudioTrack(trackId); } catch { } });
                    ShowNotification($"Pista de audio: {clickedItem.Header}");
                }
            };
            targetSubmenu.Items.Add(item);
        }
    }

    private void MenuAudioTracks_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi)
        {
            PopulateAudioTracksMenu(mi);
        }
    }

    private void PopulateSubtitlesTracksMenu(MenuItem targetSubmenu)
    {
        if (targetSubmenu == null || _mediaPlayer == null) return;
        targetSubmenu.Items.Clear();

        var spuDesc = _mediaPlayer.SpuDescription;
        int currentSpu = _mediaPlayer.Spu;

        var disableItem = new MenuItem
        {
            Header = "Desactivado",
            IsCheckable = true,
            IsChecked = (currentSpu == -1),
            Tag = -1
        };
        disableItem.Click += (s, args) =>
        {
            _mediaPlayer.SetSpu(-1);
            PathSubtitlesIcon.Fill = (Brush)FindResource("TextSecondaryBrush");
            ShowNotification("Subtítulos desactivados");
        };
        targetSubmenu.Items.Add(disableItem);
        targetSubmenu.Items.Add(new Separator());

        if (spuDesc != null)
        {
            foreach (var spu in spuDesc)
            {
                if (spu.Id == -1) continue;
                var item = new MenuItem
                {
                    Header = spu.Name,
                    IsCheckable = true,
                    IsChecked = (currentSpu == spu.Id),
                    Tag = spu.Id
                };
                item.Click += (s, args) =>
                {
                    if (s is MenuItem clicked && clicked.Tag is int trackId)
                    {
                        _mediaPlayer.SetSpu(trackId);
                        PathSubtitlesIcon.Fill = (Brush)FindResource("AccentBrush");
                        ShowNotification($"Subtítulo: {clicked.Header}");
                    }
                };
                targetSubmenu.Items.Add(item);
            }
        }

        if (!string.IsNullOrEmpty(_currentVideoPath))
        {
            var locals = _subtitleService.FindLocalSubtitles(_currentVideoPath);
            if (locals.Count > 0)
            {
                targetSubmenu.Items.Add(new Separator());
                targetSubmenu.Items.Add(new MenuItem { Header = "Archivos locales:", IsEnabled = false });

                foreach (var local in locals)
                {
                    var localItem = new MenuItem
                    {
                        Header = local.Name,
                        Tag = local.FilePath
                    };
                    localItem.Click += (s, args) =>
                    {
                        if (s is MenuItem cl && cl.Tag is string path)
                        {
                            LoadExternalSubtitle(path);
                        }
                    };
                    targetSubmenu.Items.Add(localItem);
                }
            }
        }
    }

    private void MenuSubtitlesTracks_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi)
        {
            PopulateSubtitlesTracksMenu(mi);
        }
    }

    #endregion

    #region Subtitles & Audio Drawer / Popovers Handlers

    private void BtnSubtitles_Click(object sender, RoutedEventArgs e)
    {
        RefreshTracks();
        SubtitlesFlyout.Visibility = SubtitlesFlyout.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        AudioFlyout.Visibility = Visibility.Collapsed;
    }

    private void BtnCloseSubtitlesFlyout_Click(object sender, RoutedEventArgs e) => SubtitlesFlyout.Visibility = Visibility.Collapsed;

    private void ComboSubtitles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingTracks) return;
        if (_mediaPlayer == null || ComboSubtitles.SelectedItem is not TrackItem track) return;

        if (track.IsExternal)
        {
            LoadExternalSubtitle(track.FilePath);
        }
        else
        {
            Task.Run(() => { try { _mediaPlayer?.SetSpu(track.Id); } catch { } });
            PathSubtitlesIcon.Fill = track.Id == -1 ? (Brush)FindResource("TextSecondaryBrush") : (Brush)FindResource("AccentBrush");
        }
    }

    private void BtnSubDelay_Minus100_Click(object sender, RoutedEventArgs e) => AdjustSubtitleDelay(-100);
    private void BtnSubDelay_Minus25_Click(object sender, RoutedEventArgs e) => AdjustSubtitleDelay(-25);
    private void BtnSubDelay_Plus25_Click(object sender, RoutedEventArgs e) => AdjustSubtitleDelay(25);
    private void BtnSubDelay_Plus100_Click(object sender, RoutedEventArgs e) => AdjustSubtitleDelay(100);
    private void BtnSubDelayReset_Click(object sender, RoutedEventArgs e) => ResetSubtitleDelay();

    private void BtnLoadLocalSubtitle_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Seleccionar archivo de subtítulos",
            Filter = "Subtítulos (*.srt;*.vtt;*.ass;*.ssa;*.sub)|*.srt;*.vtt;*.ass;*.ssa;*.sub|Todos los archivos (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            LoadExternalSubtitle(dlg.FileName);
            SubtitlesFlyout.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnOpenOnlineSubtitles_Click(object sender, RoutedEventArgs e)
    {
        SubtitlesFlyout.Visibility = Visibility.Collapsed;
        OnlineSubtitlesDrawer.Visibility = Visibility.Visible;
        PlaylistDrawer.Visibility = Visibility.Collapsed;

        if (!string.IsNullOrEmpty(_currentVideoPath))
        {
            TxtSearchSubtitlesQuery.Text = VideoHashCalculator.CleanTitle(_currentVideoPath);
            SearchOnlineSubtitles();
        }
    }

    private void BtnCloseOnlineSubtitlesDrawer_Click(object sender, RoutedEventArgs e) => OnlineSubtitlesDrawer.Visibility = Visibility.Collapsed;

    private void BtnExecuteSubSearch_Click(object sender, RoutedEventArgs e) => SearchOnlineSubtitles();

    private void TxtSearchSubtitlesQuery_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SearchOnlineSubtitles();
    }

    private void LangFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string lang)
        {
            _selectedLanguageFilter = lang;
            if (OnlineSubtitlesDrawer.Visibility == Visibility.Visible && !string.IsNullOrEmpty(TxtSearchSubtitlesQuery.Text))
            {
                SearchOnlineSubtitles();
            }
        }
    }

    private void BtnAudioTracks_Click(object sender, RoutedEventArgs e)
    {
        RefreshTracks();
        AudioFlyout.Visibility = AudioFlyout.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        SubtitlesFlyout.Visibility = Visibility.Collapsed;
    }

    private void BtnCloseAudioFlyout_Click(object sender, RoutedEventArgs e) => AudioFlyout.Visibility = Visibility.Collapsed;

    private void ListAudioTracks_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingTracks) return;
        if (_mediaPlayer == null || ListAudioTracks.SelectedItem is not TrackItem track) return;
        Task.Run(() => { try { _mediaPlayer?.SetAudioTrack(track.Id); } catch { } });
        ShowNotification($"Pista de audio: {track.Name}");
        AudioFlyout.Visibility = Visibility.Collapsed;
    }

    private void BtnPlaylistToggle_Click(object sender, RoutedEventArgs e)
    {
        PlaylistDrawer.Visibility = PlaylistDrawer.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        OnlineSubtitlesDrawer.Visibility = Visibility.Collapsed;
        SubtitlesFlyout.Visibility = Visibility.Collapsed;
        AudioFlyout.Visibility = Visibility.Collapsed;
    }

    private void BtnClosePlaylistDrawer_Click(object sender, RoutedEventArgs e) => PlaylistDrawer.Visibility = Visibility.Collapsed;

    private void BtnAddPlaylistFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Añadir vídeos a la lista de reproducción",
            Multiselect = true,
            Filter = "Archivos de vídeo|*.mkv;*.mp4;*.avi;*.webm;*.mov;*.flv;*.wmv;*.ts;*.m2ts;*.vob;*.3gp|Todos los archivos (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            _playlistService.AddFiles(dlg.FileNames);
            TxtPlaylistCount.Text = $"({_playlistService.Items.Count})";
        }
    }

    private void BtnRepeatMode_Click(object sender, RoutedEventArgs e)
    {
        _playlistService.Repeat = _playlistService.Repeat switch
        {
            RepeatMode.None => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            _ => RepeatMode.None
        };

        PathRepeatIcon.Data = _playlistService.Repeat == RepeatMode.One 
            ? (Geometry)FindResource("PathRepeatOne") 
            : (Geometry)FindResource("PathRepeat");

        BtnRepeatMode.Foreground = _playlistService.Repeat == RepeatMode.None 
            ? (Brush)FindResource("TextSecondaryBrush") 
            : (Brush)FindResource("AccentBrush");

        ShowNotification($"Repetición: {_playlistService.Repeat}");
    }

    private void BtnShuffle_Click(object sender, RoutedEventArgs e)
    {
        _playlistService.Shuffle = !_playlistService.Shuffle;
        BtnShuffle.Foreground = _playlistService.Shuffle ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("TextSecondaryBrush");
        ShowNotification(_playlistService.Shuffle ? "Reproducción aleatoria activada" : "Reproducción aleatoria desactivada");
    }

    private void BtnClearPlaylist_Click(object sender, RoutedEventArgs e)
    {
        _playlistService.Clear();
        TxtPlaylistCount.Text = "(0)";
        ShowNotification("Lista de reproducción vaciada");
    }

    private void ListPlaylistItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ListPlaylistItems.SelectedItem is PlaylistItem item)
        {
            _playlistService.SetCurrent(ListPlaylistItems.SelectedIndex);
        }
    }

    #endregion

    #region Recent Files Handlers

    private void UpdateRecentFilesUI()
    {
        if (ListRecentFiles != null && BorderRecentFiles != null)
        {
            var items = _recentFilesService.RecentFiles.Take(5).ToList();
            ListRecentFiles.ItemsSource = items;
            BorderRecentFiles.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void RecentItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is RecentFileItem item)
        {
            if (File.Exists(item.FilePath))
            {
                PlayMedia(item.FilePath);
            }
            else
            {
                ShowNotification("El archivo ya no existe en el disco");
            }
        }
    }

    private void BtnClearRecent_Click(object sender, RoutedEventArgs e)
    {
        _recentFilesService.Clear();
        UpdateRecentFilesUI();
        ShowNotification("Historial de archivos recientes borrado");
    }

    private void MenuRecentFiles_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem target)
        {
            target.Items.Clear();
            if (_recentFilesService.RecentFiles.Count == 0)
            {
                target.Items.Add(new MenuItem { Header = "Sin archivos recientes", IsEnabled = false });
                return;
            }

            foreach (var item in _recentFilesService.RecentFiles.Take(10))
            {
                var mi = new MenuItem
                {
                    Header = item.Title,
                    ToolTip = item.FilePath,
                    Tag = item.FilePath
                };
                mi.Click += (s, args) =>
                {
                    if (s is MenuItem clicked && clicked.Tag is string path && File.Exists(path))
                    {
                        PlayMedia(path);
                    }
                };
                target.Items.Add(mi);
            }

            target.Items.Add(new Separator());
            var clearItem = new MenuItem { Header = "Borrar historial de recientes" };
            clearItem.Click += (s, args) =>
            {
                _recentFilesService.Clear();
                UpdateRecentFilesUI();
                ShowNotification("Historial de recientes borrado");
            };
            target.Items.Add(clearItem);
        }
    }

    #endregion

    protected override void OnClosed(EventArgs e)
    {
        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();
        base.OnClosed(e);
    }
}