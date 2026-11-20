using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using PomodoroFocus.Core.Services;

namespace PomodoroFocus.App.Interop
{
    public sealed class HotkeyManager : IDisposable
    {
        public const int IdToggle = 9001;
        public const int IdSkip = 9002;
        public const int IdMini = 9003;
        public const int IdMain = 9004;

        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly bool _disabledByCli;
        private IntPtr _hwnd;
        private HwndSource _source;
        private readonly Dictionary<int, bool> _registered = new Dictionary<int, bool>();
        private readonly Dictionary<int, Action> _callbacks = new Dictionary<int, Action>();

        public HotkeyManager(bool disabledByCli)
        {
            _disabledByCli = disabledByCli;
        }

        public void Attach(Window window)
        {
            if (window == null) return;
            _hwnd = new WindowInteropHelper(window).Handle;
            if (_hwnd != IntPtr.Zero)
            {
                _source = HwndSource.FromHwnd(_hwnd);
                if (_source != null)
                {
                    _source.AddHook(WndProc);
                }
            }
        }

        public void SetAction(int id, Action callback)
        {
            _callbacks[id] = callback;
        }

        public bool Register(int id, string gestureText, bool enabled)
        {
            Unregister(id);
            if (_disabledByCli || !enabled || _hwnd == IntPtr.Zero)
            {
                _registered[id] = false;
                return false;
            }

            HotkeyGesture gesture;
            if (!HotkeyGesture.TryParse(gestureText, out gesture))
            {
                _registered[id] = false;
                return false;
            }

            uint mods = (uint)gesture.Modifiers | MOD_NOREPEAT;
            bool ok = RegisterHotKey(_hwnd, id, mods, (uint)gesture.VirtualKey);
            _registered[id] = ok;
            return ok;
        }

        public void Unregister(int id)
        {
            bool wasReg;
            if (_registered.TryGetValue(id, out wasReg) && wasReg && _hwnd != IntPtr.Zero)
            {
                UnregisterHotKey(_hwnd, id);
            }
            _registered[id] = false;
        }

        public void UnregisterAll()
        {
            var keys = new List<int>(_registered.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                Unregister(keys[i]);
            }
        }

        public bool IsRegistered(int id)
        {
            bool ok;
            return _registered.TryGetValue(id, out ok) && ok;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                Action cb;
                if (_callbacks.TryGetValue(id, out cb) && cb != null)
                {
                    cb();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            UnregisterAll();
            if (_source != null)
            {
                _source.RemoveHook(WndProc);
                _source = null;
            }
        }
    }
}
