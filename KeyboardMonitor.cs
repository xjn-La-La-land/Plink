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

        // Command modifiers. Shift is intentionally not in this set — it's
        // part of normal typing (capitals, shifted punctuation). VK_CONTROL
        // and VK_MENU cover both left and right variants for GetAsyncKeyState.
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        // Minimum interval between sounds for the same key. Caps the rate
        // during hold-press so it sounds like a steady typewriter cadence
        // instead of following the (much faster) OS auto-repeat rate.
        private const int SameKeyMinIntervalMs = 110;

        private readonly LowLevelKeyboardProc _proc;
        private IntPtr _hook;
        private int _lastVkCode = -1;
        private DateTime _lastEventTimeUtc = DateTime.MinValue;

        public event EventHandler KeyPressed;

        public KeyboardMonitor()
        {
            _proc = HookCallback;
        }

        public bool Start()
        {
            if (_hook != IntPtr.Zero)
                return true;

            _lastVkCode = -1;
            _lastEventTimeUtc = DateTime.MinValue;
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

            // Clear _hook before Unhook so any callback racing through
            // HookCallback sees "stopped" and won't be re-entered after the
            // handle is freed. CallNextHookEx's first arg is ignored on
            // modern Windows, so passing the cached oldHook is fine.
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

                    if (!IsInjected(data.flags) &&
                        IsCharacterKey(data.vkCode) &&
                        !IsCommandModifierHeld())
                    {
                        DateTime now = DateTime.UtcNow;
                        bool tooSoon =
                            data.vkCode == _lastVkCode &&
                            (now - _lastEventTimeUtc).TotalMilliseconds < SameKeyMinIntervalMs;

                        // Only advance state when we actually fire — suppressed
                        // events must not push the timestamp forward, or the
                        // window slides with the OS repeat rate and never opens.
                        if (!tooSoon)
                        {
                            _lastVkCode = data.vkCode;
                            _lastEventTimeUtc = now;

                            EventHandler handler = KeyPressed;
                            if (handler != null)
                                handler(this, EventArgs.Empty);
                        }
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

        // Allow-list of "typing" keys: letters, digits (top row + numpad),
        // numpad operators, space/Enter/Tab/Backspace, and US punctuation.
        // Everything else (arrows, F1-F12, Esc, Home/End/PgUp/PgDn, Insert,
        // Delete, Print Screen, media keys, IME keys, modifiers, locks…) is
        // silent.
        private static bool IsCharacterKey(int vkCode)
        {
            if (vkCode >= 0x41 && vkCode <= 0x5A) return true;  // A–Z
            if (vkCode >= 0x30 && vkCode <= 0x39) return true;  // 0–9
            if (vkCode >= 0x60 && vkCode <= 0x6F) return true;  // numpad 0-9 * + sep - . /
            switch (vkCode)
            {
                case 0x08:  // Backspace
                case 0x09:  // Tab
                case 0x0D:  // Enter (also numpad Enter — same vkCode)
                case 0x20:  // Space
                case 0x2E:  // Delete
                case 0xBA:  // ;:
                case 0xBB:  // =+
                case 0xBC:  // ,<
                case 0xBD:  // -_
                case 0xBE:  // .>
                case 0xBF:  // /?
                case 0xC0:  // `~
                case 0xDB:  // [{
                case 0xDC:  // \|
                case 0xDD:  // ]}
                case 0xDE:  // '"
                case 0xDF:  // OEM 8 (layout-specific)
                    return true;
            }
            return false;
        }

        // Suppresses sound when Ctrl/Alt/Win is held — those make the press
        // a command (Ctrl+C, Alt+Tab, Win+L), not typing. Shift is excluded
        // on purpose: it's a typing modifier.
        private static bool IsCommandModifierHeld()
        {
            return (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0
                || (GetAsyncKeyState(VK_MENU) & 0x8000) != 0
                || (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0
                || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;
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

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
