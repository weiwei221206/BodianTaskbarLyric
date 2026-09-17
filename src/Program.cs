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
    public class Program {
        private static Mutex _appMutex;
        private static NotifyIcon _trayIcon;
        private static EventWaitHandle _wakeEvent;
        private static EventWaitHandle _exitEvent;
        private const string WAKE_EVENT_NAME = @"Global\BodianTaskbarLyric_WakeEvent_2026";
        private const string EXIT_EVENT_NAME = @"Global\BodianTaskbarLyric_ExitEvent_2026";
        private const string MUTEX_NAME = @"Global\BodianTaskbarLyric_Unique_Mutex_2026";
        [ThreadStatic]
        private static bool _inLog;
        private static string _appDir;

        public static string AppDir {
            get {
                if (_appDir == null) {
                    try {
                        string loc = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        if (!string.IsNullOrEmpty(loc)) _appDir = Path.GetDirectoryName(loc);
                    } catch { }
                    if (string.IsNullOrEmpty(_appDir)) _appDir = AppDomain.CurrentDomain.BaseDirectory;
                }
                return _appDir;
            }
        }

                public static void Log(string msg) {
            if (_inLog) return;
            try {
                _inLog = true;
                string logPath = Path.Combine(AppDir, "run.log");
                FileInfo fi = new FileInfo(logPath);
                if (fi.Exists && fi.Length > 5 * 1024 * 1024) {
                    File.WriteAllText(logPath, string.Empty);
                }
                File.AppendAllText(logPath, string.Format("[{0:HH:mm:ss.fff}] {1}\r\n", DateTime.Now, msg));
            } catch { }
            finally {
                _inLog = false;
            }
        }

        public static void ActivateSettingsWindow(SettingsWindow settingsWin) {
            try {
                if (settingsWin == null) return;
                settingsWin.Show();
                if (settingsWin.WindowState == WindowState.Minimized) {
                    settingsWin.WindowState = WindowState.Normal;
                }
                settingsWin.Activate();
                settingsWin.Topmost = true;
                settingsWin.Topmost = false;
                settingsWin.Focus();

                IntPtr hWnd = new WindowInteropHelper(settingsWin).Handle;
                if (hWnd != IntPtr.Zero) {
                    Win32.SwitchToThisWindow(hWnd, true);
                    Win32.SetForegroundWindow(hWnd);
                }
            } catch (Exception ex) {
                Log("ActivateSettingsWindow error: " + ex.Message);
            }
        }

        [STAThread]
        public static void Main(string[] args) {
            try {
                // 支持命令行退出指令
                if (args != null && args.Length > 0 && (args[0].Equals("--exit", StringComparison.OrdinalIgnoreCase) || args[0].Equals("-exit", StringComparison.OrdinalIgnoreCase))) {
                    try {
                        using (var exitWait = EventWaitHandle.OpenExisting(EXIT_EVENT_NAME)) {
                            exitWait.Set();
                            Log("Signaled running instance to exit via CLI.");
                            return;
                        }
                    } catch { return; }
                }

                AppDomain.CurrentDomain.ProcessExit += (s, e) => {
                    Log("PROCESS EXIT EVENT FIRED! Stack:\r\n" + Environment.StackTrace);
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                    Log("UNHANDLED EXCEPTION: " + (e.ExceptionObject != null ? e.ExceptionObject.ToString() : "null"));
                };

                Log("Program starting v" + AppConfig.APP_VERSION + "...");

                try {
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Tls | SecurityProtocolType.Ssl3;
                    ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslErrors) => true;
                    Log("[Init] SecurityProtocol initialized with TLS 1.2/1.1.");
                } catch (Exception secEx) {
                    Log("[Init] SecurityProtocol init EX: " + secEx.Message);
                }
                try {
                    using (var wake = EventWaitHandle.OpenExisting(WAKE_EVENT_NAME)) {
                        wake.Set();
                        Log("Signaled running instance to wake up.");
                        return;
                    }
                } catch (WaitHandleCannotBeOpenedException) {
                    // 没有已有实例在运行，正常启动
                } catch (Exception ex) {
                    Log("Wake check: " + ex.Message);
                }

                string appDir = AppDir;
                string configPath = Path.Combine(appDir, "config.json");
                AppConfig config = AppConfig.Load(configPath);
                Log("Config loaded (v" + config.Version + ").");

                //默认以管理员权限运行开关
                if (config.RunAsAdmin && !Win32.IsAdministrator() && (args == null || Array.IndexOf(args, "--no-elevation") < 0)) {
                    try {
                        ProcessStartInfo psi = new ProcessStartInfo {
                            FileName = Process.GetCurrentProcess().MainModule.FileName,
                            WorkingDirectory = appDir,
                            UseShellExecute = true,
                            Verb = "runas"
                        };
                        Process.Start(psi);
                        Log("Elevated instance launched, exiting unprivileged process.");
                        return;
                    } catch (Exception uacEx) {
                        Log("User cancelled UAC elevation: " + uacEx.Message);
                    }
                }
                bool isNewInstance;
                EventWaitHandleSecurity sec = new EventWaitHandleSecurity();
                SecurityIdentifier everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                sec.AddAccessRule(new EventWaitHandleAccessRule(everyone, EventWaitHandleRights.FullControl, AccessControlType.Allow));
                _wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WAKE_EVENT_NAME, out isNewInstance, sec);

                if (!isNewInstance) {
                    Log("WakeEvent already exists, signaling existing instance.");
                    _wakeEvent.Set();
                    return;
                }

                bool isExitNew;
                _exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EXIT_EVENT_NAME, out isExitNew, sec);
                _exitEvent.Reset();

                _appMutex = new Mutex(true, MUTEX_NAME, out isNewInstance);
                Log("Mutex acquired: " + isNewInstance);
                if (!isNewInstance) {
                    Log("Mutex already held, exiting.");
                    return;
                }

                if (config.DisableHardwareAcceleration) {
                    RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
                    Log("[Init] ProcessRenderMode set to SoftwareOnly (GPU hardware acceleration disabled).");
                } else {
                    RenderOptions.ProcessRenderMode = RenderMode.Default;
                    Log("[Init] ProcessRenderMode set to Default (GPU hardware acceleration enabled).");
                }

                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                app.DispatcherUnhandledException += (s, e) => {
                    Log("DISPATCHER EXCEPTION: " + e.Exception.ToString());
                    e.Handled = true;
                };

                // 1. 同步预加载数据库最新记录
                BodianEngine engine = new BodianEngine();
                engine.Init();
                Log("Engine inited.");

                SettingsWindow settingsWin = null;

                // 2. 初始化任务栏浮窗与控制中心
                TaskbarOverlayWindow overlay = new TaskbarOverlayWindow(config, configPath, engine, () => {
                    if (settingsWin != null) {
                        ActivateSettingsWindow(settingsWin);
                    }
                });
                Log("Overlay created.");

                settingsWin = new SettingsWindow(config, configPath, engine, overlay);
                Log("SettingsWindow created.");
                overlay.SetSettingsWindowOpen(settingsWin.IsVisible && settingsWin.WindowState != WindowState.Minimized);

                // 启动后台线程监听唤醒事件
                Thread wakeThread = new Thread(() => {
                    while (true) {
                        try {
                            if (_wakeEvent.WaitOne()) {
                                Log("Wake event received, activating SettingsWindow...");
                                if (Application.Current != null && Application.Current.Dispatcher != null) {
                                    Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                                        ActivateSettingsWindow(settingsWin);
                                    }));
                                }
                            }
                        } catch (ThreadAbortException) {
                            break;
                        } catch (Exception ex) {
                            Log("Wake thread error: " + ex.Message);
                        }
                    }
                });
                wakeThread.IsBackground = true;
                wakeThread.Start();

                // 启动后台线程监听退出事件
                Thread exitThread = new Thread(() => {
                    while (true) {
                        try {
                            if (_exitEvent.WaitOne()) {
                                Log("Exit event received, shutting down application...");
                                if (Application.Current != null && Application.Current.Dispatcher != null) {
                                    Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                                        Application.Current.Shutdown();
                                    }));
                                }
                                break;
                            }
                        } catch (ThreadAbortException) {
                            break;
                        } catch { }
                    }
                });
                exitThread.IsBackground = true;
                exitThread.Start();

                InitTrayIcon(settingsWin);
                Log("TrayIcon created.");

                overlay.Show();
                if (!config.SilentStart) {
                    settingsWin.Show();
                }
                Log("Windows shown (SilentStart=" + config.SilentStart + ").");

                // 3. 启动后台线程监听
                engine.Start();
                Log("Engine started, entering app.Run()...");

                app.Run();
                Log("app.Run() returned.");

                if (_trayIcon != null) {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
            } catch (Exception ex) {
                Log("EXCEPTION: " + ex.ToString());
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_err.log"), ex.ToString());
            }
        }

        private static void InitTrayIcon(SettingsWindow settingsWin) {
            try {
                _trayIcon = new NotifyIcon();
                _trayIcon.Text = "波点音乐 - 任务栏歌词 v" + AppConfig.APP_VERSION;
                try {
                    _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
                } catch {
                    _trayIcon.Icon = System.Drawing.SystemIcons.Application;
                }
                _trayIcon.Visible = true;

                _trayIcon.DoubleClick += (s, e) => {
                    ActivateSettingsWindow(settingsWin);
                };

                ContextMenuStrip trayMenu = new ContextMenuStrip();
                trayMenu.Items.Add("⚙️ 打开控制中心", null, (s, e) => {
                    ActivateSettingsWindow(settingsWin);
                });
                trayMenu.Items.Add(new ToolStripSeparator());
                trayMenu.Items.Add("❌ 退出程序", null, (s, e) => {
                    Application.Current.Shutdown();
                });

                _trayIcon.ContextMenuStrip = trayMenu;
            } catch { }
        }
    }

}
