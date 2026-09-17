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
    public static class PlayerController {
        public static void PreviousTrack() {
            SendMediaCommand(Win32.APPCOMMAND_MEDIA_PREVIOUSTRACK, Win32.VK_MEDIA_PREV_TRACK);
        }

        public static void PlayOrPause() {
            SendMediaCommand(Win32.APPCOMMAND_MEDIA_PLAY_PAUSE, Win32.VK_MEDIA_PLAY_PAUSE);
        }

        public static void NextTrack() {
            SendMediaCommand(Win32.APPCOMMAND_MEDIA_NEXTTRACK, Win32.VK_MEDIA_NEXT_TRACK);
        }

        public static void VolumeDown() {
            try {
                Win32.keybd_event(Win32.VK_VOLUME_DOWN, 0, 0, UIntPtr.Zero);
                Win32.keybd_event(Win32.VK_VOLUME_DOWN, 0, Win32.KEYEVENTF_KEYUP, UIntPtr.Zero);
            } catch { }
        }

        public static void VolumeUp() {
            try {
                Win32.keybd_event(Win32.VK_VOLUME_UP, 0, 0, UIntPtr.Zero);
                Win32.keybd_event(Win32.VK_VOLUME_UP, 0, Win32.KEYEVENTF_KEYUP, UIntPtr.Zero);
            } catch { }
        }

        public static void OpenBodianApp() {
            try {
                Process[] procs = Process.GetProcessesByName("bodian_pc");
                if (procs.Length == 0) procs = Process.GetProcessesByName("bodian");

                string exePath = "";
                if (procs.Length > 0) {
                    try {
                        exePath = procs[0].MainModule.FileName;
                    } catch { }

                    var pids = new HashSet<uint>();
                    foreach (var p in procs) {
                        try { pids.Add((uint)p.Id); } catch { }
                    }

                    bool restored = false;
                    Win32.EnumWindows((hWnd, lParam) => {
                        uint pid;
                        Win32.GetWindowThreadProcessId(hWnd, out pid);
                        if (pids.Contains(pid)) {
                            Win32.ShowWindow(hWnd, Win32.SW_RESTORE);
                            Win32.SetForegroundWindow(hWnd);
                            Win32.SwitchToThisWindow(hWnd, true);
                            restored = true;
                        }
                        return true;
                    }, IntPtr.Zero);

                    if (restored) return;
                }

                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath)) {
                    Process.Start(new ProcessStartInfo {
                        FileName = exePath,
                        UseShellExecute = true
                    });
                    return;
                }

                string[] candidates = new string[] {
                    @"D:\Program Files\bodian\bodian_pc.exe",
                    @"C:\Program Files\bodian\bodian_pc.exe",
                    @"C:\Program Files (x86)\bodian\bodian_pc.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\bodian\bodian_pc.exe")
                };
                foreach (var path in candidates) {
                    if (File.Exists(path)) {
                        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                        return;
                    }
                }
            } catch { }
        }

        private static void SendMediaCommand(int appCommand, byte vkCode) {
            try {
                var targetPids = new HashSet<uint>();
                Process[] procs = Process.GetProcesses();
                foreach (var p in procs) {
                    try {
                        string name = p.ProcessName.ToLower();
                        if (name.Contains("bodian")) {
                            targetPids.Add((uint)p.Id);
                        }
                    } catch { }
                }

                if (targetPids.Count > 0) {
                    var hwnds = new List<IntPtr>();
                    Win32.EnumWindows((hWnd, lParam) => {
                        uint pid;
                        Win32.GetWindowThreadProcessId(hWnd, out pid);
                        if (targetPids.Contains(pid)) {
                            hwnds.Add(hWnd);
                        }
                        return true;
                    }, IntPtr.Zero);

                    foreach (var h in hwnds) {
                        IntPtr cmdLParam = (IntPtr)(appCommand << 16);
                        Win32.PostMessage(h, Win32.WM_APPCOMMAND, h, cmdLParam);
                    }
                }
            } catch { }

            try {
                Win32.keybd_event(vkCode, 0, 0, UIntPtr.Zero);
                Win32.keybd_event(vkCode, 0, Win32.KEYEVENTF_KEYUP, UIntPtr.Zero);
            } catch { }
        }
    }

}
