using System;
using System.Runtime.InteropServices;

namespace Plink
{
    // Global low-level keyboard hook. It intentionally exposes only a generic
    // "key was pressed" event, never the key value itself.
    internal sealed class KeyboardMonitor : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int HC_ACTION = 0;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int LLKHF_LOWER_IL_INJECTED = 0x00000002;
        private const int LLKHF_INJECTED = 0x00000010;

        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int VK_CAPITAL = 0x14;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_NUMLOCK = 0x90;
        private const int VK_SCROLL = 0x91;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LMENU = 0xA4;
        private const int VK_RMENU = 0xA5;

        private readonly LowLevelKeyboardProc _proc;
        private IntPtr _hook;

        public event EventHandler KeyPressed;

        public KeyboardMonitor()
        {
            _proc = HookCallback;
        }

        public bool IsRunning
        {
            get { return _hook != IntPtr.Zero; }
        }

        public bool Start()
        {
            if (_hook != IntPtr.Zero)
                return true;

            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
            if (_hook == IntPtr.Zero)
            {
                DebugLog.Write("keyboard hook install failed: " + Marshal.GetLastWin32Error());
                return false;
            }

            DebugLog.Write("keyboard hook installed");
            return true;
        }

        public void Stop()
        {
            if (_hook == IntPtr.Zero)
                return;

            IntPtr oldHook = _hook;
            _hook = IntPtr.Zero;
            if (!UnhookWindowsHookEx(oldHook))
                DebugLog.Write("keyboard hook uninstall failed: " + Marshal.GetLastWin32Error());
            else
                DebugLog.Write("keyboard hook uninstalled");
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode == HC_ACTION &&
                    (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
                {
                    KeyboardEvent data = (KeyboardEvent)Marshal.PtrToStructure(
                        lParam, typeof(KeyboardEvent));

                    if (!IsInjected(data.flags) && !IsIgnoredKey(data.vkCode))
                    {
                        EventHandler handler = KeyPressed;
                        if (handler != null)
                            handler(this, EventArgs.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog.Write("keyboard hook callback failed: " + ex.Message);
            }

            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private static bool IsInjected(int flags)
        {
            return (flags & (LLKHF_INJECTED | LLKHF_LOWER_IL_INJECTED)) != 0;
        }

        private static bool IsIgnoredKey(int vkCode)
        {
            switch (vkCode)
            {
                case VK_SHIFT:
                case VK_CONTROL:
                case VK_MENU:
                case VK_LWIN:
                case VK_RWIN:
                case VK_LSHIFT:
                case VK_RSHIFT:
                case VK_LCONTROL:
                case VK_RCONTROL:
                case VK_LMENU:
                case VK_RMENU:
                case VK_CAPITAL:
                case VK_NUMLOCK:
                case VK_SCROLL:
                    return true;
                default:
                    return false;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardEvent
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(
            int idHook,
            LowLevelKeyboardProc lpfn,
            IntPtr hMod,
            uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(
            IntPtr hhk,
            int nCode,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
