using System;
using System.Reflection;
using Microsoft.Win32;

namespace PomodoroFocus.App.Interop
{
    public static class StartupRegistryService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "PomodoroFocus";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    object val = key.GetValue(ValueName);
                    return val != null && !string.IsNullOrWhiteSpace(val.ToString());
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool SetEnabled(bool enable, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null)
                    {
                        errorMessage = "无法打开开机自启注册表项";
                        return false;
                    }
                    if (enable)
                    {
                        string exePath = Assembly.GetExecutingAssembly().Location;
                        string cmd = "\"" + exePath + "\" --minimized";
                        key.SetValue(ValueName, cmd, RegistryValueKind.String);
                    }
                    else
                    {
                        if (key.GetValue(ValueName) != null)
                        {
                            key.DeleteValue(ValueName, false);
                        }
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "更新开机自启失败：" + ex.Message;
                return false;
            }
        }
    }
}
