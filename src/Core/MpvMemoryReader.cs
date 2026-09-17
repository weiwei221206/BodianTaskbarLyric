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
    public class MpvMemoryReader {
        private int _targetPid = 0;
        private IntPtr _hProcess = IntPtr.Zero;
        private IntPtr _eventLoopBase = IntPtr.Zero;
        private IntPtr _libmpvBase = IntPtr.Zero;
        private long _mpvHandle = 0;
        private long _mpvCommandStringOffset = 0;
        private long _mpctxAddr = 0;
        private long _lastAttemptTicks = 0;
        private int _readFailureCount = 0;
        private int _cachedMyHeadRva = 0xA1D8;

        public bool IsAvailable { get; private set; }
        public bool IsAccessDenied { get; private set; }
        public bool IsVerifiedPlaybackField { get; private set; }
        public double CurrentPts { get; set; }
        public int TargetPid { get { return _targetPid; } }
        public long MpvHandle { get { return _mpvHandle; } }

        public MpvMemoryReader() {
            CurrentPts = -1.0;
        }

        public void Reset() {
            if (_hProcess != IntPtr.Zero) {
                Win32.CloseHandle(_hProcess);
                _hProcess = IntPtr.Zero;
            }
            _targetPid = 0;
            _eventLoopBase = IntPtr.Zero;
            _libmpvBase = IntPtr.Zero;
            _mpvHandle = 0;
            _mpctxAddr = 0;
            _readFailureCount = 0;
            IsAvailable = false;
            IsAccessDenied = false;
            IsVerifiedPlaybackField = false;
            CurrentPts = -1.0;
        }

        public void ResetPts() {
            CurrentPts = -1.0;
        }

        private long _lastPtsLogTick = 0;

        private long ReadInt64(long address) {
            if (_hProcess == IntPtr.Zero || address == 0) return 0;
            byte[] buf = new byte[8];
            IntPtr bytesRead;
            if (Win32.ReadProcessMemory(_hProcess, new IntPtr(address), buf, 8, out bytesRead) && bytesRead.ToInt32() == 8) {
                return BitConverter.ToInt64(buf, 0);
            }
            return 0;
        }

        public void Tick() {
            try {
                long now = Environment.TickCount;

                // 1. 如果已锁定有效的 mpctx，进行官方标准 time-pos 直读
                if (_hProcess != IntPtr.Zero && _mpctxAddr != 0) {
                    // 读取覆盖 0x2E0 ~ 0x3E0，包含 0x328 (time-pos)、0x318 (fallback)、0x3D0 (audio time)、0x2F0 (playback_pts)
                    byte[] block = new byte[0x100];
                    IntPtr bytesRead;
                    if (Win32.ReadProcessMemory(_hProcess, new IntPtr(_mpctxAddr + 0x2E0), block, block.Length, out bytesRead) && bytesRead.ToInt32() == block.Length) {
                        double t328 = BitConverter.ToDouble(block, 0x328 - 0x2E0); // +0x48
                        double t318 = BitConverter.ToDouble(block, 0x318 - 0x2E0); // +0x38
                        double t3d0 = BitConverter.ToDouble(block, 0x3D0 - 0x2E0); // +0xF0
                        double t2f0 = BitConverter.ToDouble(block, 0x2F0 - 0x2E0); // +0x10

                        double chosenPos = -1.0;
                        if (!double.IsNaN(t328) && !double.IsInfinity(t328) && t328 >= 0.0 && t328 < 86400.0) {
                            chosenPos = t328;
                        } else if (!double.IsNaN(t318) && !double.IsInfinity(t318) && t318 >= 0.0 && t318 < 86400.0) {
                            chosenPos = t318;
                        } else if (!double.IsNaN(t3d0) && !double.IsInfinity(t3d0) && t3d0 >= 0.0 && t3d0 < 86400.0) {
                            chosenPos = t3d0;
                        } else if (!double.IsNaN(t2f0) && !double.IsInfinity(t2f0) && t2f0 >= 0.0 && t2f0 < 86400.0) {
                            chosenPos = t2f0;
                        }

                        if (chosenPos >= 0.0) {
                            CurrentPts = chosenPos;
                            IsAvailable = true;
                            _readFailureCount = 0;

                            if (now - _lastPtsLogTick > 4000) {
                                _lastPtsLogTick = now;
                                Program.Log(string.Format("[MemReader] Tick: Pos={0:F2}s (328={1:F2}, 318={2:F2}, 2f0={3:F2}), mpctx=0x{4:X}", CurrentPts, t328, t318, t2f0, _mpctxAddr));
                            }
                            return;
                        }
                    }

                    _readFailureCount++;
                    // 连续 10 次读取失败（如播放实例重建或进程重启），立即重置重新连接
                    if (_readFailureCount > 10) {
                        Program.Log("[MemReader] Read failure threshold reached; resetting reader state.");
                        Reset();
                    }
                    return;
                }

                // 2. 尝试定位波点音乐进程与 mpctx
                if (_hProcess == IntPtr.Zero || _mpctxAddr == 0) {
                    if (now - _lastAttemptTicks < 1000) return;
                    _lastAttemptTicks = now;
                    TryAttach();
                    return;
                }
            } catch { }
        }

        private void TryAttach() {
            try {
                if (_hProcess != IntPtr.Zero) {
                    uint exitCode;
                    if (Win32.GetExitCodeProcess(_hProcess, out exitCode) && exitCode != Win32.STILL_ACTIVE) {
                        Reset();
                        return;
                    }
                }

                // 1. 获取波点音乐进程与基址
                if (_hProcess == IntPtr.Zero) {
                    Process[] procs = Process.GetProcessesByName("bodian_pc");
                    if (procs.Length == 0) procs = Process.GetProcessesByName("bodian");
                    if (procs.Length == 0) {
                        IsAccessDenied = false;
                        Reset();
                        return;
                    }

                    Process p = procs[0];
                    _targetPid = p.Id;

                    _eventLoopBase = IntPtr.Zero;
                    _libmpvBase = IntPtr.Zero;
                    try {
                        foreach (ProcessModule m in p.Modules) {
                            if (m.ModuleName.IndexOf("media_kit_native_event_loop", StringComparison.OrdinalIgnoreCase) >= 0) {
                                _eventLoopBase = m.BaseAddress;
                            }
                            if (m.ModuleName.IndexOf("libmpv-2", StringComparison.OrdinalIgnoreCase) >= 0) {
                                _libmpvBase = m.BaseAddress;
                            }
                        }
                    } catch (System.ComponentModel.Win32Exception wEx) {
                        if (wEx.NativeErrorCode == 5) IsAccessDenied = true;
                        return;
                    } catch {
                        return;
                    }

                    if (_eventLoopBase == IntPtr.Zero) return;

                    // 解析 libmpv-2.dll 导出的 mpv_command_string 偏移
                    if (_libmpvBase != IntPtr.Zero && _mpvCommandStringOffset == 0) {
                        try {
                            string mpvDllPath = Path.Combine(Path.GetDirectoryName(p.MainModule.FileName), "libmpv-2.dll");
                            if (File.Exists(mpvDllPath)) {
                                IntPtr localLib = Win32.LoadLibrary(mpvDllPath);
                                if (localLib != IntPtr.Zero) {
                                    IntPtr localFunc = Win32.GetProcAddress(localLib, "mpv_command_string");
                                    if (localFunc != IntPtr.Zero) {
                                        _mpvCommandStringOffset = localFunc.ToInt64() - localLib.ToInt64();
                                        Program.Log(string.Format("[MemReader] Resolved mpv_command_string offset: +0x{0:X}", _mpvCommandStringOffset));
                                    }
                                }
                            }
                        } catch (Exception ex) {
                            Program.Log("[MemReader] Failed to resolve mpv_command_string offset: " + ex.Message);
                        }
                    }

                    uint desiredAccess = Win32.PROCESS_VM_READ | Win32.PROCESS_QUERY_INFORMATION | Win32.PROCESS_VM_WRITE | Win32.PROCESS_VM_OPERATION | Win32.PROCESS_CREATE_THREAD;
                    _hProcess = Win32.OpenProcess(desiredAccess, false, _targetPid);
                    if (_hProcess == IntPtr.Zero) {
                        _hProcess = Win32.OpenProcess(Win32.PROCESS_VM_READ | Win32.PROCESS_QUERY_INFORMATION, false, _targetPid);
                    }
                    if (_hProcess == IntPtr.Zero) {
                        int err = Marshal.GetLastWin32Error();
                        if (err == 5) IsAccessDenied = true;
                        return;
                    }

                    IsAccessDenied = false;
                }
                // 1. media_kit_native_event_loop.dll 中在 0xA1D0 维护 std::unordered_map
                // 2. 0xA1D8 为 myHead 头指针
                // 3. firstNode + 0x10 为 Map Key
                // 4. 偏移 +0x48 为 struct MPContext*
                // 5. struct MPContext 偏移 +0x328 处即为实时的播放时间戳 double time-pos
                if (_eventLoopBase != IntPtr.Zero && _hProcess != IntPtr.Zero) {
                    long myHead = ReadInt64(_eventLoopBase.ToInt64() + _cachedMyHeadRva);
                    long firstNode = myHead != 0 ? ReadInt64(myHead) : 0;
                    long mpvHandle = (firstNode != 0 && firstNode != myHead) ? ReadInt64(firstNode + 0x10) : 0;
                    bool isValid = false;

                    if (IsLikelyUserPointer(mpvHandle)) {
                        byte[] nameBuf = new byte[8];
                        IntPtr bytesRead;
                        if (Win32.ReadProcessMemory(_hProcess, new IntPtr(mpvHandle), nameBuf, 8, out bytesRead) && bytesRead.ToInt32() >= 4) {
                            if (nameBuf[0] == 'm' && nameBuf[1] == 'a' && nameBuf[2] == 'i' && nameBuf[3] == 'n') {
                                isValid = true;
                            }
                        }
                    }

                    if (!isValid) {
                        // 客户端版本更新自愈机制：通过特征码从 DLL 二进制动态重定位 singleton 与 myHead RVA
                        int dynamicRva = ResolveMyHeadRvaDynamically(_eventLoopBase);
                        if (dynamicRva != _cachedMyHeadRva) {
                            _cachedMyHeadRva = dynamicRva;
                            myHead = ReadInt64(_eventLoopBase.ToInt64() + _cachedMyHeadRva);
                            firstNode = myHead != 0 ? ReadInt64(myHead) : 0;
                            mpvHandle = (firstNode != 0 && firstNode != myHead) ? ReadInt64(firstNode + 0x10) : 0;
                            if (IsLikelyUserPointer(mpvHandle)) {
                                byte[] nameBuf = new byte[8];
                                IntPtr bytesRead;
                                if (Win32.ReadProcessMemory(_hProcess, new IntPtr(mpvHandle), nameBuf, 8, out bytesRead) && bytesRead.ToInt32() >= 4) {
                                    if (nameBuf[0] == 'm' && nameBuf[1] == 'a' && nameBuf[2] == 'i' && nameBuf[3] == 'n') {
                                        isValid = true;
                                    }
                                }
                            }
                        }
                    }

                    if (!isValid) return;

                    // 读取 MPContext*
                    long mpctx = ReadInt64(mpvHandle + 0x48);
                    if (!IsLikelyUserPointer(mpctx)) return;

                    _mpvHandle = mpvHandle;
                    _mpctxAddr = mpctx;
                    IsVerifiedPlaybackField = true;
                    IsAvailable = true;
                    Program.Log(string.Format("[MemReader] Attached successfully! mpvHandle=0x{0:X}, mpctx=0x{1:X}, time-pos=+0x328", mpvHandle, mpctx));
                }
            } catch (Exception ex) {
                Program.Log("[MemReader] TryAttach EX: " + ex.Message);
            }
        }

        private int ResolveMyHeadRvaDynamically(IntPtr modBase) {
            try {
                if (_hProcess == IntPtr.Zero || modBase == IntPtr.Zero) return 0xA1D8;
                byte[] mem = new byte[0x10000];
                IntPtr bytesRead;
                if (Win32.ReadProcessMemory(_hProcess, modBase, mem, mem.Length, out bytesRead)) {
                    int len = bytesRead.ToInt32();
                    for (int i = 0; i < len - 13; i++) {
                        if (mem[i] == 0x48 && mem[i + 1] == 0x8D && mem[i + 2] == 0x05 &&
                            mem[i + 7] == 0x48 && mem[i + 8] == 0x83 && mem[i + 9] == 0xC4 &&
                            mem[i + 10] == 0x20 && mem[i + 11] == 0x5B && mem[i + 12] == 0xC3) {
                            int disp = BitConverter.ToInt32(mem, i + 3);
                            int singletonRva = (i + 7) + disp;
                            int myHeadRva = singletonRva + 0x58;
                            Program.Log(string.Format("[MemReader] Dynamically discovered myHead RVA: 0x{0:X} (singleton=0x{1:X})", myHeadRva, singletonRva));
                            return myHeadRva;
                        }
                    }
                }
            } catch (Exception ex) {
                Program.Log("[MemReader] ResolveMyHeadRvaDynamically EX: " + ex.Message);
            }
            return 0xA1D8;
        }

        public bool Seek(double targetSeconds) {
            try {
                if (targetSeconds < 0) targetSeconds = 0;
                if (_targetPid == 0 || _mpvHandle == 0 || _libmpvBase == IntPtr.Zero || _mpvCommandStringOffset == 0) {
                    Program.Log(string.Format("[MemReader] Seek failed: state invalid (pid={0}, handle=0x{1:X}, libmpv=0x{2:X}, funcOff=0x{3:X})",
                        _targetPid, _mpvHandle, _libmpvBase.ToInt64(), _mpvCommandStringOffset));
                    return false;
                }

                long remoteFunc = _libmpvBase.ToInt64() + _mpvCommandStringOffset;
                string cmd = string.Format(System.Globalization.CultureInfo.InvariantCulture, "seek {0:F2} absolute", targetSeconds);
                byte[] cmdBytes = Encoding.UTF8.GetBytes(cmd + "\0");

                int shellcodeLen = 41;
                int totalLen = shellcodeLen + cmdBytes.Length;

                IntPtr hProc = Win32.OpenProcess(Win32.PROCESS_VM_OPERATION | Win32.PROCESS_VM_WRITE | Win32.PROCESS_VM_READ | Win32.PROCESS_CREATE_THREAD | Win32.PROCESS_QUERY_INFORMATION, false, _targetPid);
                if (hProc == IntPtr.Zero) {
                    hProc = _hProcess;
                }
                if (hProc == IntPtr.Zero) {
                    Program.Log("[MemReader] Seek failed: unable to open process");
                    return false;
                }

                bool closeOnExit = (hProc != _hProcess);

                try {
                    IntPtr remoteMem = Win32.VirtualAllocEx(hProc, IntPtr.Zero, (uint)totalLen, Win32.MEM_COMMIT | Win32.MEM_RESERVE, Win32.PAGE_EXECUTE_READWRITE);
                    if (remoteMem == IntPtr.Zero) {
                        Program.Log("[MemReader] Seek failed: VirtualAllocEx failed");
                        return false;
                    }

                    try {
                        long cmdAddr = remoteMem.ToInt64() + shellcodeLen;

                        byte[] code = new byte[totalLen];
                        // sub rsp, 0x28
                        code[0] = 0x48; code[1] = 0x83; code[2] = 0xEC; code[3] = 0x28;
                        // mov rcx, mpvHandle
                        code[4] = 0x48; code[5] = 0xB9;
                        Array.Copy(BitConverter.GetBytes(_mpvHandle), 0, code, 6, 8);
                        // mov rdx, cmdAddr
                        code[14] = 0x48; code[15] = 0xBA;
                        Array.Copy(BitConverter.GetBytes(cmdAddr), 0, code, 16, 8);
                        // mov rax, remoteFunc
                        code[24] = 0x48; code[25] = 0xB8;
                        Array.Copy(BitConverter.GetBytes(remoteFunc), 0, code, 26, 8);
                        // call rax
                        code[34] = 0xFF; code[35] = 0xD0;
                        // add rsp, 0x28
                        code[36] = 0x48; code[37] = 0x83; code[38] = 0xC4; code[39] = 0x28;
                        // ret
                        code[40] = 0xC3;

                        Array.Copy(cmdBytes, 0, code, shellcodeLen, cmdBytes.Length);

                        IntPtr written;
                        if (!Win32.WriteProcessMemory(hProc, remoteMem, code, code.Length, out written)) {
                            Program.Log("[MemReader] Seek failed: WriteProcessMemory failed");
                            return false;
                        }

                        uint thId;
                        IntPtr hThread = Win32.CreateRemoteThread(hProc, IntPtr.Zero, 0, remoteMem, IntPtr.Zero, 0, out thId);
                        if (hThread == IntPtr.Zero) {
                            Program.Log("[MemReader] Seek failed: CreateRemoteThread failed");
                            return false;
                        }

                        Win32.WaitForSingleObject(hThread, 500);
                        Win32.CloseHandle(hThread);

                        CurrentPts = targetSeconds;
                        Program.Log(string.Format("[MemReader] Seek({0:F2}s) command dispatched successfully via mpv_command_string", targetSeconds));
                        return true;
                    } finally {
                        Win32.VirtualFreeEx(hProc, remoteMem, 0, Win32.MEM_RELEASE);
                    }
                } finally {
                    if (closeOnExit && hProc != IntPtr.Zero) {
                        Win32.CloseHandle(hProc);
                    }
                }
            } catch (Exception ex) {
                Program.Log("[MemReader] Seek EX: " + ex.Message);
                return false;
            }
        }

        private static bool IsLikelyUserPointer(long value) {
            ulong u = unchecked((ulong)value);
            return u >= 0x10000UL && u < 0x0000800000000000UL;
        }
    }

}
