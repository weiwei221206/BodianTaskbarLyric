using System;
using System.Reflection;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Ellipse = System.Windows.Shapes.Ellipse;
using Rectangle = System.Windows.Shapes.Rectangle;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using System.Windows.Forms;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using Orientation = System.Windows.Controls.Orientation;
using Cursors = System.Windows.Input.Cursors;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using System.Security.Principal;
using System.Security.AccessControl;

namespace BodianTaskbarLyric {
    public class BodianEngine {
        private string _dbPath;
        private string _logDir;
        private string _cacheDir;

        private long _lastOrd = -1;
        private string _lastSongTimeStr = "";
        public SongInfo CurrentSong { get; private set; }
        private ImageSource _currentCover;
        private List<LyricLine> _lyrics = new List<LyricLine>();
        private int _currentLyricIndex = -1;

        private Stopwatch _playStopwatch = new Stopwatch();
        public bool IsPlaying { get; private set; }
        private double _playTimeOffset = 0;
        private object _timeLock = new object();
        private long _lastProcessCheckMs = 0;
        private bool _playerProcessPresent = false;
        private double _lastMemPts = -1;
        private long _lastMemPtsChangeMs = 0;
        private volatile bool _memoryPtsStale = false;
        private volatile bool _waitingForTrackReset = false;
        private long _trackSwitchTick = 0;

        private MpvMemoryReader _memReader = new MpvMemoryReader();

        public bool IsMemorySyncActive {
            get { return _memReader != null && _memReader.IsAvailable && _memReader.CurrentPts >= 0 && !_memoryPtsStale && !_waitingForTrackReset; }
        }

        public bool IsMemoryAccessDenied {
            get { return _memReader != null && _memReader.IsAccessDenied; }
        }

        public event Action<SongInfo, ImageSource> OnSongChanged;
        public event Action<ImageSource> OnCoverChanged;
        public event Action<LyricLine, int> OnLyricChanged;
        public event Action<bool> OnPlayStateChanged;
        public event Action<bool> OnProcessStateChanged;

        // 默认波点黑胶唱片图标 
        public static readonly ImageSource DefaultCover = CreateDefaultBodianIcon(96);

        public double CurrentPlaybackSeconds {
            get {
                lock (_timeLock) {
                    if (IsMemorySyncActive) {
                        return Math.Max(0, _memReader.CurrentPts);
                    }
                    return Math.Max(0, _playTimeOffset + (IsPlaying ? _playStopwatch.Elapsed.TotalSeconds : 0));
                }
            }
        }

        public bool Seek(double seconds) {
            if (seconds < 0) seconds = 0;
            if (CurrentSong != null && seconds > CurrentSong.Duration) seconds = CurrentSong.Duration;

            lock (_timeLock) {
                _playTimeOffset = seconds;
                _playStopwatch.Restart();
                _lastMemPts = seconds;
                _lastMemPtsChangeMs = Environment.TickCount;
            }

            bool success = false;
            if (_memReader != null) {
                success = _memReader.Seek(seconds);
            }
            UpdatePlaybackTime();
            return success;
        }

        public bool IsBodianRunning {
            get { return _playerProcessPresent; }
        }

        public BodianEngine() {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _dbPath = Path.Combine(localAppData, @"cn.wenyu.bodian\bodian_pc\database\songDB.db");
            _logDir = Path.Combine(localAppData, @"cn.wenyu.bodian\bodian_pc\bdlog");
            _cacheDir = Path.Combine(localAppData, @"BodianTaskbarLyric\cache");
            if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);
            _playerProcessPresent = IsBodianProcessRunning();
        }

        public bool IsBodianProcessRunning() {
            try {
                return Process.GetProcessesByName("bodian_pc").Length > 0 || Process.GetProcessesByName("bodian").Length > 0;
            } catch {
                return false;
            }
        }

        private void SetPlayingState(bool playing) {
            bool changed;
            lock (_timeLock) {
                changed = IsPlaying != playing;
                if (!changed) return;

                if (playing) {
                    if (CurrentSong != null && _playTimeOffset <= 0.5) {
                        double elapsed = GetElapsedFromSongStart(_lastSongTimeStr, CurrentSong.Duration);
                        if (elapsed > 0.5) _playTimeOffset = elapsed;
                    }
                    _playStopwatch.Restart();
                } else {
                    if (_playStopwatch.IsRunning) {
                        _playTimeOffset += _playStopwatch.Elapsed.TotalSeconds;
                    }
                    _playStopwatch.Reset();
                }
                IsPlaying = playing;
            }

            if (OnPlayStateChanged != null) OnPlayStateChanged(playing);
        }

        private double GetElapsedFromSongStart(string timeStr, int duration) {
            try {
                DateTime songStart;
                if (!string.IsNullOrEmpty(timeStr) && DateTime.TryParse(timeStr, out songStart)) {
                    double elapsed = (DateTime.Now - songStart).TotalSeconds;
                    if (elapsed >= 0 && (duration <= 0 || elapsed < duration + 15)) return elapsed;
                }
            } catch { }
            return 0;
        }

        private void HandlePositionLogLine(string line) {
            try {
                if (_memReader.IsVerifiedPlaybackField) return;

                Match m = Regex.Match(line, @"play old song info id:(\d+).*?position:([0-9]+).*?new song info id:(\d+)", RegexOptions.IgnoreCase);
                if (!m.Success || CurrentSong == null) return;

                long oldSongId;
                long newSongId;
                double position;
                if (!long.TryParse(m.Groups[1].Value, out oldSongId) ||
                    !double.TryParse(m.Groups[2].Value, out position) ||
                    !long.TryParse(m.Groups[3].Value, out newSongId) ||
                    oldSongId != CurrentSong.Id || newSongId != CurrentSong.Id) {
                    return;
                }

                lock (_timeLock) {
                    _playTimeOffset = Math.Max(0, position);
                    _memoryPtsStale = !_memReader.IsVerifiedPlaybackField;
                    _currentLyricIndex = -1;
                    if (IsPlaying) _playStopwatch.Restart();
                    else _playStopwatch.Reset();
                }
                Program.Log(string.Format("[LogSync] Position={0:F2}s for song {1}; lyric index reset.", position, CurrentSong.Id));
                UpdatePlaybackTime();
            } catch { }
        }

        private void HandleSongLogLine(string line) {
            try {
                if (string.IsNullOrEmpty(line)) return;

                // 场景 1：歌曲完整参数信息
                int idxFull = line.IndexOf("歌曲的完整参数信息:");
                if (idxFull >= 0) {
                    string payload = line.Substring(idxFull + "歌曲的完整参数信息:".Length).Trim();
                    Match mId = Regex.Match(payload, @"\bid:\s*(\d+)");
                    if (!mId.Success) return;

                    long id = long.Parse(mId.Groups[1].Value);
                    if (CurrentSong != null && CurrentSong.Id == id && _lyrics != null && _lyrics.Count > 0) {
                        return;
                    }

                    SongInfo song = new SongInfo();
                    song.Id = id;

                    Match mName = Regex.Match(payload, @"\bname:\s*(.*?)(?=,\s*[a-zA-Z0-9_]+:|\}$)");
                    if (mName.Success) song.Title = mName.Groups[1].Value.Trim();

                    Match mArtist = Regex.Match(payload, @"\bartist:\s*(.*?)(?=,\s*[a-zA-Z0-9_]+:|\}$)");
                    if (mArtist.Success) song.Artist = mArtist.Groups[1].Value.Trim();

                    Match mAlbum = Regex.Match(payload, @"\balbum:\s*(.*?)(?=,\s*[a-zA-Z0-9_]+:|\}$)");
                    if (mAlbum.Success) song.Album = mAlbum.Groups[1].Value.Trim();

                    Match mDur = Regex.Match(payload, @"\bduration:\s*(\d+)");
                    if (mDur.Success) {
                        int dur;
                        if (int.TryParse(mDur.Groups[1].Value, out dur)) song.Duration = dur;
                    }

                    Match mPic120 = Regex.Match(payload, @"\balbumPic120:\s*([^,\s\}]+)");
                    if (mPic120.Success) {
                        song.PicUrl = mPic120.Groups[1].Value.Trim();
                    } else {
                        Match mPic = Regex.Match(payload, @"\balbumPic:\s*([^,\s\}]+)");
                        if (mPic.Success) song.PicUrl = mPic.Groups[1].Value.Trim();
                    }

                    Match mArtistPic = Regex.Match(payload, @"\bpic:\s*(https?://[^\s,\]\}]+\.(?:png|jpg|jpeg))");
                    if (mArtistPic.Success) {
                        song.BackupPicUrl = mArtistPic.Groups[1].Value.Trim();
                    } else {
                        Match mHead = Regex.Match(payload, @"\bartistPic:\s*([^,\s\}]+)");
                        if (mHead.Success) song.BackupPicUrl = mHead.Groups[1].Value.Trim();
                    }

                    Program.Log(string.Format("[LogMonitor] Song detected from log (full info): id={0}, name='{1}', artist='{2}'",
                        song.Id, song.Title, song.Artist));
                    SwitchToSong(song, null, false);
                }
            } catch (Exception ex) {
                Program.Log("[LogMonitor] HandleSongLogLine EX: " + ex.ToString());
            }
        }

        public static ImageSource CreateDefaultBodianIcon(int size) {
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen()) {
                double center = size / 2.0;

   
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(28, 28, 30)), null, new Point(center, center), center, center);
                Pen groovePen1 = new Pen(new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)), 1.2);
                dc.DrawEllipse(null, groovePen1, new Point(center, center), size * 0.38, size * 0.38);
                Pen groovePen2 = new Pen(new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), 1.2);
                dc.DrawEllipse(null, groovePen2, new Point(center, center), size * 0.28, size * 0.28);


                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0, 210, 106)), null, new Point(center, center), size * 0.17, size * 0.17);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(28, 28, 30)), null, new Point(center, center), size * 0.05, size * 0.05);
            }

            RenderTargetBitmap rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        public static BitmapImage LoadEmbeddedBitmap(string resName) {
            try {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream stream = asm.GetManifestResourceStream(resName)) {
                    if (stream != null) {
                        BitmapImage bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.StreamSource = stream;
                        bi.EndInit();
                        bi.Freeze();
                        return bi;
                    }
                }
            } catch { }
            return null;
        }

        public static ImageSource GetAppIconImageSource() {
            try {
                BitmapImage embedded = LoadEmbeddedBitmap("ico.png");
                if (embedded != null) return embedded;

                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName)) {
                    if (icon != null) {
                        using (var bmp = icon.ToBitmap()) {
                            var hBmp = bmp.GetHbitmap();
                            try {
                                var wpfBmp = Imaging.CreateBitmapSourceFromHBitmap(hBmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                                wpfBmp.Freeze();
                                return wpfBmp;
                            } finally {
                                Win32.DeleteObject(hBmp);
                            }
                        }
                    }
                }
            } catch { }
            return DefaultCover;
        }

        public static ImageSource GetBodianClientIconImageSource() {
            try {
                BitmapImage embedded = LoadEmbeddedBitmap("bodian_client.png");
                if (embedded != null) return embedded;

                Process[] procs = Process.GetProcessesByName("bodian_pc");
                if (procs != null && procs.Length > 0) {
                    try {
                        string exePath = procs[0].MainModule.FileName;
                        if (File.Exists(exePath)) {
                            using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath)) {
                                if (icon != null) {
                                    using (var bmp = icon.ToBitmap()) {
                                        var hBmp = bmp.GetHbitmap();
                                        try {
                                            var wpfBmp = Imaging.CreateBitmapSourceFromHBitmap(hBmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                                            wpfBmp.Freeze();
                                            return wpfBmp;
                                        } finally {
                                            Win32.DeleteObject(hBmp);
                                        }
                                    }
                                }
                            }
                        }
                    } catch { }
                }
            } catch { }
            return GetAppIconImageSource();
        }

        // 同步初始化：启动前立即拉取一次数据库记录
        public void Init() {
            try {
                CheckDatabase();
            } catch { }
        }

        // 启动后台持续监听线程
        public void Start() {
            Thread logWorker = new Thread(LogMonitorLoop) { IsBackground = true };
            logWorker.Start();

            Thread playWorker = new Thread(PlaybackLoop) { IsBackground = true };
            playWorker.Start();
        }

        public ImageSource GetCoverImage(SongInfo song) {
            if (_currentCover != null) return _currentCover;
            if (song == null) return DefaultCover;
            ImageSource img = LoadCoverImage(song.Id, song.PicUrl, song.BackupPicUrl);
            _currentCover = img != null ? img : DefaultCover;
            return _currentCover;
        }

        public ImageSource GetCoverImage(long songId, string url, string backupUrl = null) {
            if (_currentCover != null) return _currentCover;
            ImageSource img = LoadCoverImage(songId, url, backupUrl);
            _currentCover = img != null ? img : DefaultCover;
            return _currentCover;
        }

        private void PlaybackLoop() {
            while (true) {
                try {
                    long now = Environment.TickCount;
                    if (_lastProcessCheckMs == 0 || now - _lastProcessCheckMs >= 300) {
                        _lastProcessCheckMs = now;
                        bool running = IsBodianProcessRunning();
                        if (running != _playerProcessPresent) {
                            _playerProcessPresent = running;
                            if (!_playerProcessPresent) {
                                if (_memReader.IsAvailable) _memReader.Reset();
                                _lastMemPts = -1;
                                _lastMemPtsChangeMs = 0;
                                _memoryPtsStale = false;
                                SetPlayingState(false);
                            }
                            if (OnProcessStateChanged != null) OnProcessStateChanged(_playerProcessPresent);
                        }
                    }

                    _memReader.Tick();
                    if (_memReader.IsAvailable && _memReader.CurrentPts >= 0) {
                        double memPts = _memReader.CurrentPts;

                        if (_waitingForTrackReset) {
                            if (memPts <= 1.5) {
                                _waitingForTrackReset = false;
                                Program.Log(string.Format("[PlaybackLoop] MPV reset confirmed: memPts={0:F2}s", memPts));
                                _lastMemPts = memPts;
                                _lastMemPtsChangeMs = now;
                                _memoryPtsStale = false;
                                lock (_timeLock) {
                                    _playTimeOffset = memPts;
                                    if (IsPlaying) _playStopwatch.Restart();
                                    else _playStopwatch.Reset();
                                }
                            } else if (now - _trackSwitchTick > 2500) {
                                _waitingForTrackReset = false;
                                Program.Log(string.Format("[PlaybackLoop] Track reset wait timeout: memPts={0:F2}s", memPts));
                            } else {
                                UpdatePlaybackTime();
                                Thread.Sleep(25);
                                continue;
                            }
                        }

                        bool hadPreviousMemPts = _lastMemPts >= 0;
                        double previousMemPts = _lastMemPts;
                        double memDelta = hadPreviousMemPts ? memPts - previousMemPts : 0;
                        bool memPtsChanged = !hadPreviousMemPts || Math.Abs(memDelta) > 0.001;
                        if (memPtsChanged) {
                            _lastMemPts = memPts;
                            _lastMemPtsChangeMs = now;
                            _memoryPtsStale = false;
                        } else if (!_memReader.IsVerifiedPlaybackField && IsPlaying && _lastMemPtsChangeMs > 0 && now - _lastMemPtsChangeMs >= 1500) {
                            if (!_memoryPtsStale) {
                                _memoryPtsStale = true;
                                Program.Log("[PlaybackLoop] Memory PTS is stale; falling back to stopwatch sync.");
                            }
                        }

                        double currentEstimate;
                        lock (_timeLock) {
                            currentEstimate = Math.Max(0, _playTimeOffset + (IsPlaying ? _playStopwatch.Elapsed.TotalSeconds : 0));
                        }
                        if (!_memReader.IsVerifiedPlaybackField && !hadPreviousMemPts && memPts <= 0.5 && currentEstimate > 1.0) {
                            _memoryPtsStale = true;
                            Program.Log(string.Format("[PlaybackLoop] Ignoring implausible initial memory PTS={0:F2}; keeping local estimate={1:F2}.", memPts, currentEstimate));
                        }

                        if (!_memoryPtsStale && memPtsChanged) {
                            lock (_timeLock) {
                                double curEst = Math.Max(0, _playTimeOffset + (IsPlaying ? _playStopwatch.Elapsed.TotalSeconds : 0));
                                bool initialValueIsPlausible = hadPreviousMemPts || memPts > 0.5 || curEst <= 1.0;
                                bool seekDetected = hadPreviousMemPts && (memDelta < -0.25 || memDelta > 1.2);
                                bool initialPosition = !hadPreviousMemPts && initialValueIsPlausible && Math.Abs(memPts - curEst) > 1.0;
                                if (seekDetected || initialPosition) {
                                    Program.Log(string.Format("[PlaybackLoop] SEEK DETECTED: curEst={0:F2} -> memPts={1:F2} (delta={2:F2})", curEst, memPts, memDelta));
                                    _playTimeOffset = memPts;
                                    if (IsPlaying) _playStopwatch.Restart();
                                    else _playStopwatch.Reset();
                                    _currentLyricIndex = -1; // 进度拖动或跳变，强制刷新定位歌词
                                }
                            }
                        }
                    }

                    UpdatePlaybackTime();
                } catch (Exception ex) {
                    Program.Log("[PlaybackLoop] EX: " + ex.ToString());
                }
                Thread.Sleep(25);
            }
        }

        private void CheckDatabase() {
            if (!File.Exists(_dbPath)) return;

            IntPtr db = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;
            try {
                byte[] pathBytes = Encoding.UTF8.GetBytes(_dbPath + "\0");
                int rc = Win32.sqlite3_open_v2(pathBytes, out db, 0x00000001, IntPtr.Zero); // 0x00000001 = SQLITE_OPEN_READONLY
                if (rc != 0) return;

                Win32.sqlite3_busy_timeout(db, 1000);

                string sql = "SELECT ord, id, json, time FROM hist_song ORDER BY ord DESC LIMIT 1;";
                rc = Win32.sqlite3_prepare16_v2(db, sql, -1, out stmt, IntPtr.Zero);
                if (rc == 0 && Win32.sqlite3_step(stmt) == 100) {
                    long ord = Win32.sqlite3_column_int64(stmt, 0);
                    long id = Win32.sqlite3_column_int64(stmt, 1);
                    IntPtr textPtr = Win32.sqlite3_column_text16(stmt, 2);
                    string json = textPtr != IntPtr.Zero ? Marshal.PtrToStringUni(textPtr) : "";
                    IntPtr timePtr = Win32.sqlite3_column_text16(stmt, 3);
                    string timeStr = timePtr != IntPtr.Zero ? Marshal.PtrToStringUni(timePtr) : "";
                    _lastSongTimeStr = timeStr;

                    if (ord != _lastOrd) {
                        bool isFirstRun = (_lastOrd == -1);
                        _lastOrd = ord;
                        HandleNewSong(id, json, timeStr, isFirstRun);
                    }
                }
            } catch (Exception ex) {
                Program.Log("[CheckDatabase] EX: " + ex.ToString());
            } finally {
                if (stmt != IntPtr.Zero) Win32.sqlite3_finalize(stmt);
                if (db != IntPtr.Zero) Win32.sqlite3_close(db);
            }
        }

        private void HandleNewSong(long id, string json, string timeStr, bool isFirstRun) {
            try {
                JavaScriptSerializer js = new JavaScriptSerializer();
                Dictionary<string, object> dict = js.Deserialize<Dictionary<string, object>>(json);

                SongInfo song = new SongInfo();
                song.Id = id;
                if (dict != null) {
                    if (dict.ContainsKey("name") && dict["name"] != null) song.Title = dict["name"].ToString();
                    if (dict.ContainsKey("artist") && dict["artist"] != null) song.Artist = dict["artist"].ToString();
                    if (dict.ContainsKey("album") && dict["album"] != null) song.Album = dict["album"].ToString();
                    if (dict.ContainsKey("duration") && dict["duration"] != null) song.Duration = Convert.ToInt32(dict["duration"]);
                    if (dict.ContainsKey("albumPic120") && dict["albumPic120"] != null) song.PicUrl = dict["albumPic120"].ToString();
                    else if (dict.ContainsKey("albumPic") && dict["albumPic"] != null) song.PicUrl = dict["albumPic"].ToString();

                    if (dict.ContainsKey("artists") && dict["artists"] is System.Collections.ArrayList) {
                        var arr = (System.Collections.ArrayList)dict["artists"];
                        if (arr.Count > 0 && arr[0] is Dictionary<string, object>) {
                            var a0 = (Dictionary<string, object>)arr[0];
                            if (a0.ContainsKey("pic") && a0["pic"] != null) song.BackupPicUrl = a0["pic"].ToString();
                        }
                    }
                    if (string.IsNullOrEmpty(song.BackupPicUrl) && dict.ContainsKey("artistPic") && dict["artistPic"] != null) {
                        song.BackupPicUrl = dict["artistPic"].ToString();
                    }
                }

                SwitchToSong(song, timeStr, isFirstRun);
            } catch (Exception ex) {
                Program.Log("[Engine] HandleNewSong EX: " + ex.ToString());
            }
        }

        private void SwitchToSong(SongInfo song, string timeStr, bool isFirstRun) {
            if (song == null) return;
            try {
                if (!isFirstRun && CurrentSong != null && CurrentSong.Id == song.Id) {
                    if ((!string.IsNullOrEmpty(song.PicUrl) || !string.IsNullOrEmpty(song.BackupPicUrl)) && (_currentCover == null || _currentCover == DefaultCover)) {
                        ThreadPool.QueueUserWorkItem(state => {
                            try {
                                ImageSource loaded = LoadCoverImage(song.Id, song.PicUrl, song.BackupPicUrl);
                                if (loaded != null && CurrentSong != null && CurrentSong.Id == song.Id) {
                                    _currentCover = loaded;
                                    if (OnCoverChanged != null) OnCoverChanged(_currentCover);
                                }
                            } catch { }
                        });
                    }
                    return;
                }

                CurrentSong = song;
                _currentCover = null;
                _lastMemPts = -1;
                _lastMemPtsChangeMs = 0;
                _memoryPtsStale = false;
                if (!isFirstRun) {
                    _waitingForTrackReset = true;
                    _trackSwitchTick = Environment.TickCount;
                } else {
                    _waitingForTrackReset = false;
                }
                if (_memReader != null) _memReader.ResetPts();
                lock (_timeLock) {
                    _lyrics = new List<LyricLine>();
                    _currentLyricIndex = -1;
                }
                Program.Log(string.Format("[Engine] SwitchToSong: ord={0}, id={1}, title='{2}', artist='{3}', dur={4}, isFirstRun={5}",
                    _lastOrd, song.Id, song.Title, song.Artist, song.Duration, isFirstRun));

                bool wasPlaying = IsPlaying;
                if (isFirstRun) {
                    _playTimeOffset = wasPlaying ? GetElapsedFromSongStart(timeStr, song.Duration) : 0;
                    if (wasPlaying) _playStopwatch.Restart();
                    else _playStopwatch.Reset();
                } else {
                    _playTimeOffset = 0;
                    if (wasPlaying) _playStopwatch.Restart();
                    else _playStopwatch.Reset();
                }

                if (OnPlayStateChanged != null) OnPlayStateChanged(IsPlaying);

                if (OnSongChanged != null) {
                    OnSongChanged(song, DefaultCover);
                }

                ThreadPool.QueueUserWorkItem(state => {
                    try {
                        ImageSource loaded = LoadCoverImage(song.Id, song.PicUrl, song.BackupPicUrl);
                        _currentCover = loaded != null ? loaded : DefaultCover;
                        if (CurrentSong != null && CurrentSong.Id == song.Id) {
                            if (OnCoverChanged != null) OnCoverChanged(_currentCover);
                        }
                    } catch (Exception ex) {
                        Program.Log("[Engine] SwitchToSong Cover Worker EX: " + ex.Message);
                    }
                });

                ThreadPool.QueueUserWorkItem(state => {
                    try {
                        List<LyricLine> list = FetchLyrics(song.Id, song.Title);
                        if (CurrentSong != null && CurrentSong.Id == song.Id) {
                            lock (_timeLock) {
                                _lyrics = list;
                                _currentLyricIndex = -1;
                            }
                            Program.Log(string.Format("[Engine] FetchLyrics complete for '{0}' (count={1})", song.Title, list != null ? list.Count : 0));
                            UpdatePlaybackTime();
                        }
                    } catch { }
                });
            } catch (Exception ex) {
                Program.Log("[Engine] SwitchToSong EX: " + ex.ToString());
            }
        }

        private void UpdatePlaybackTime() {
            if (CurrentSong == null || _lyrics == null || _lyrics.Count == 0) return;

            double curSec = CurrentPlaybackSeconds;

            int activeIndex = -1;
            for (int i = 0; i < _lyrics.Count; i++) {
                if (curSec >= _lyrics[i].TimeSec) {
                    activeIndex = i;
                } else {
                    break;
                }
            }

            if (activeIndex != _currentLyricIndex) {
                _currentLyricIndex = activeIndex;
                if (activeIndex >= 0) {
                    if (OnLyricChanged != null) {
                        LyricLine line = _lyrics[activeIndex];
                        Program.Log(string.Format("[LyricChanged] Sec={0:F2}, idx={1}: {2}", curSec, activeIndex, line.Original));
                        OnLyricChanged(line, activeIndex);
                    }
                }
            }
        }

        private void LogMonitorLoop() {
            long lastSize = 0;
            string lastLogFile = "";

            while (true) {
                try {
                    if (Directory.Exists(_logDir)) {
                        string[] files = Directory.GetFiles(_logDir, "log_*.log");
                        if (files.Length > 0) {
                            Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                            string curFile = files[0];

                            if (curFile != lastLogFile) {
                                lastLogFile = curFile;
                                FileInfo fi = new FileInfo(curFile);
                                long curSize = fi.Length;

                                long seekStart = Math.Max(0, curSize - 65536);
                                using (FileStream fs = new FileStream(curFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                                    fs.Seek(seekStart, SeekOrigin.Begin);
                                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8)) {
                                        string line;
                                        bool? lastEvent = null;
                                        string lastSongLine = null;
                                        while ((line = sr.ReadLine()) != null) {
                                            if (line.Contains("歌曲的完整参数信息:")) {
                                                lastSongLine = line;
                                            }
                                            if (line.Contains("mpv playing event=true")) lastEvent = true;
                                            else if (line.Contains("mpv playing event=false")) lastEvent = false;
                                        }
                                        if (lastSongLine != null) {
                                            HandleSongLogLine(lastSongLine);
                                        }
                                        if (lastEvent.HasValue) {
                                            bool playState = lastEvent.Value && IsBodianProcessRunning();
                                            SetPlayingState(playState);
                                        }
                                    }
                                }
                                lastSize = curSize;
                            } else {
                                FileInfo fi = new FileInfo(curFile);
                                long curSize = fi.Length;
                                if (curSize < lastSize) {
                                    lastSize = 0;
                                }
                                if (curSize > lastSize) {
                                    using (FileStream fs = new FileStream(curFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                                        fs.Seek(lastSize, SeekOrigin.Begin);
                                        using (StreamReader sr = new StreamReader(fs, Encoding.UTF8)) {
                                            string line;
                                            while ((line = sr.ReadLine()) != null) {
                                                HandleSongLogLine(line);
                                                HandlePositionLogLine(line);
                                                if (line.Contains("mpv playing event=true") && IsBodianProcessRunning()) {
                                                    SetPlayingState(true);
                                                } else if (line.Contains("mpv playing event=false")) {
                                                    SetPlayingState(false);
                                                }
                                            }
                                        }
                                    }
                                    lastSize = curSize;
                                }
                            }
                        }
                    }
                } catch { }
                Thread.Sleep(150);
            }
        }

        private bool DownloadCoverFile(string url, string targetPath) {
            if (string.IsNullOrEmpty(url)) return false;
            try {
                if (File.Exists(targetPath)) {
                    FileInfo fi = new FileInfo(targetPath);
                    if (fi.Length > 100) return true;
                    try { File.Delete(targetPath); } catch { }
                }

                byte[] data = DownloadUrlBytes(url);
                if (data == null || data.Length <= 100) {
                    // 若 HTTPS 失败，且链接是 https://，尝试降级明文 http:// 回退
                    if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) {
                        string httpUrl = "http://" + url.Substring("https://".Length);
                        Program.Log(string.Format("[Cover] Retrying download via HTTP fallback: {0}", httpUrl));
                        data = DownloadUrlBytes(httpUrl);
                    }
                }

                if (data != null && data.Length > 100) {
                    string dir = Path.GetDirectoryName(targetPath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllBytes(targetPath, data);
                    Program.Log(string.Format("[Cover] Downloaded {0} bytes to '{1}'", data.Length, Path.GetFileName(targetPath)));
                    return true;
                }
            } catch (Exception ex) {
                Program.Log(string.Format("[Cover] DownloadCoverFile EX for '{0}': {1}", url, ex.Message));
            }
            return false;
        }

        private byte[] DownloadUrlBytes(string url) {
            try {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "okhttp/3.10.0";
                req.Timeout = 5000;
                req.ReadWriteTimeout = 5000;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse()) {
                    if (resp.StatusCode == HttpStatusCode.OK) {
                        using (Stream s = resp.GetResponseStream())
                        using (MemoryStream ms = new MemoryStream()) {
                            byte[] buffer = new byte[8192];
                            int read;
                            while ((read = s.Read(buffer, 0, buffer.Length)) > 0) {
                                ms.Write(buffer, 0, read);
                            }
                            return ms.ToArray();
                        }
                    }
                }
            } catch (Exception ex) {
                Program.Log(string.Format("[Cover] DownloadUrlBytes error ({0}): {1}", url, ex.Message));
            }
            return null;
        }

        private ImageSource DecodeCoverBytes(byte[] bytes, string label) {
            if (bytes == null || bytes.Length <= 100) return null;
            try {
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 96;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            } catch (NotSupportedException ex) {
                Program.Log(string.Format("[Cover] WebP codec not supported by Windows WIC ({0}). Please install 'Webp Image Extensions' from Microsoft Store: {1}", label, ex.Message));
            } catch (Exception ex) {
                Program.Log(string.Format("[Cover] Decode error ({0}): {1}", label, ex.Message));
            }
            return null;
        }

        private ImageSource LoadCoverImage(long songId, string url, string backupUrl = null) {
            try {
                // 1. 优先加载主封面 
                string cacheFile = Path.Combine(_cacheDir, string.Format("{0}.jpg", songId));


                if (File.Exists(cacheFile)) {
                    FileInfo fi = new FileInfo(cacheFile);
                    if (fi.Length <= 100) {
                        try { File.Delete(cacheFile); } catch { }
                    }
                }

                if (!File.Exists(cacheFile) && !string.IsNullOrEmpty(url)) {
                    DownloadCoverFile(url, cacheFile);
                }

                if (File.Exists(cacheFile)) {
                    byte[] bytes = File.ReadAllBytes(cacheFile);
                    ImageSource img = DecodeCoverBytes(bytes, "Primary:" + songId);
                    if (img != null) {
                        Program.Log(string.Format("[Cover] Primary cover loaded successfully for song {0}.", songId));
                        return img;
                    }
                }

                // 2. 若主封面解码失败
                if (!string.IsNullOrEmpty(backupUrl)) {
                    string backupExt = backupUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
                    string backupFile = Path.Combine(_cacheDir, string.Format("{0}_backup{1}", songId, backupExt));

                    if (File.Exists(backupFile)) {
                        FileInfo fi = new FileInfo(backupFile);
                        if (fi.Length <= 100) {
                            try { File.Delete(backupFile); } catch { }
                        }
                    }

                    if (!File.Exists(backupFile)) {
                        Program.Log(string.Format("[Cover] Primary cover unavailable/unsupported; attempting backup url for song {0}: {1}", songId, backupUrl));
                        DownloadCoverFile(backupUrl, backupFile);
                    }

                    if (File.Exists(backupFile)) {
                        byte[] bBytes = File.ReadAllBytes(backupFile);
                        ImageSource bImg = DecodeCoverBytes(bBytes, "Backup:" + songId);
                        if (bImg != null) {
                            Program.Log(string.Format("[Cover] Backup cover loaded successfully for song {0}.", songId));
                            return bImg;
                        }
                    }
                }
            } catch (Exception ex) {
                Program.Log(string.Format("[Cover] LoadCoverImage EX for song {0}: {1}", songId, ex.ToString()));
            }
            return null;
        }

        private static bool IsSongTitleLine(string text, double sec, string songTitle) {
            if (string.IsNullOrEmpty(text)) return true;
            if (sec > 5.0) return false;

            string t = text.Trim();
            if (t.Length == 0) return true;

            if (!string.IsNullOrEmpty(songTitle)) {
                string cleanTitle = songTitle.Trim();
                if (string.Equals(t, cleanTitle, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
                string baseTitle = Regex.Replace(cleanTitle, @"\s*[\(\[（【].*?[\)\]）】]", "").Trim();
                if (!string.IsNullOrEmpty(baseTitle) && string.Equals(t, baseTitle, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }

                string checkTitle = !string.IsNullOrEmpty(baseTitle) && baseTitle.Length >= 2 ? baseTitle : cleanTitle;
                if (checkTitle.Length >= 2 && t.IndexOf(checkTitle, StringComparison.OrdinalIgnoreCase) >= 0) {
                    if (t.Contains(" - ") || t.Contains(" — ") || t.Contains(" / ") || t.Contains(" · ") ||
                        t.StartsWith("歌名") || t.StartsWith("歌曲") || t.StartsWith("曲名") ||
                        t.StartsWith("Title", StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }
            }
            if (sec <= 2.5) {
                if (t.Contains(" - ") || t.Contains(" — ") ||
                    t.StartsWith("歌名") || t.StartsWith("歌曲") || t.StartsWith("曲名") ||
                    t.StartsWith("Title", StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        private List<LyricLine> FetchLyrics(long songId, string songTitle) {
            List<LyricLine> list = new List<LyricLine>();
            try {
                string rawQ = string.Format("type=lyric&req=2&lrcx=1&rid={0}&songname=&artist=&corp=kuwo&fromchannel=bodian", songId);
                string q = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawQ));
                string url = string.Format("http://mlyric.kuwo.cn/mobi.s?f=bodian&q={0}", q);

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "okhttp/3.10.0";
                req.Timeout = 4000;

                string rawLrc = "";
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) {
                    string json = sr.ReadToEnd();
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(json);
                    if (d.ContainsKey("data") && d["data"] is Dictionary<string, object>) {
                        var data = (Dictionary<string, object>)d["data"];
                        if (data.ContainsKey("content") && data["content"] != null) {
                            byte[] b64Bytes = Convert.FromBase64String(data["content"].ToString());
                            rawLrc = Encoding.UTF8.GetString(b64Bytes);
                        }
                    }
                }

                if (string.IsNullOrEmpty(rawLrc)) return list;

                Regex reg = new Regex(@"\[(\d{2}):(\d{2})\.(\d{2,3})\](.*)");
                LyricLine lastOriginalLine = null;

                string[] lines = rawLrc.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines) {
                    Match m = reg.Match(line.Trim());
                    if (m.Success) {
                        int min = int.Parse(m.Groups[1].Value);
                        int sec = int.Parse(m.Groups[2].Value);
                        int ms = int.Parse(m.Groups[3].Value);
                        if (m.Groups[3].Value.Length == 2) ms *= 10;
                        double totalSec = min * 60 + sec + ms / 1000.0;

                        string content = m.Groups[4].Value;

                        if (content.Contains("<0,0>")) {
                            string text = Regex.Replace(content, @"<[^>]+>", "").Trim();
                            if (!string.IsNullOrEmpty(text) && lastOriginalLine != null) {
                                lastOriginalLine.Translation = text;
                            }
                        } else {
                            string text = Regex.Replace(content, @"<[^>]+>", "").Trim();
                            bool isHeaderTitle = (list.Count == 0 || totalSec <= 1.0) && IsSongTitleLine(text, totalSec, songTitle);
                            if (!string.IsNullOrEmpty(text) && !text.StartsWith("[") && !isHeaderTitle) {
                                LyricLine cur = new LyricLine {
                                    TimeSec = totalSec,
                                    Original = text,
                                    Translation = ""
                                };
                                list.Add(cur);
                                lastOriginalLine = cur;
                            }
                        }
                    }
                }

                list.Sort((a, b) => a.TimeSec.CompareTo(b.TimeSec));

            } catch { }
            return list;
        }
    }

}
