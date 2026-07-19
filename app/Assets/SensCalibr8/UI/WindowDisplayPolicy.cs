using UnityEngine;

namespace SensCalibr8.UI
{
    /// <summary>Menu-window behavior defined by DESIGN.md. Test-mode hosts may use the native fullscreen methods.</summary>
    public sealed class WindowDisplayPolicy
    {
        public const int MenuWidth = 960;
        public const int MenuHeight = 540;

        private int previousWindowWidth;
        private int previousWindowHeight;
        private bool testFullscreen;

        public bool IsTestFullscreen => testFullscreen;

        public void ApplyMenuWindow()
        {
            testFullscreen = false;
            Screen.SetResolution(MenuWidth, MenuHeight, FullScreenMode.Windowed);
        }

        public void ToggleMenuFullscreen()
        {
            if (testFullscreen) return;
            Screen.fullScreen = !Screen.fullScreen;
        }

        public void EnterTestFullscreen()
        {
            if (testFullscreen) return;
            previousWindowWidth = Screen.width;
            previousWindowHeight = Screen.height;
            Resolution native = Screen.currentResolution;
            Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);
            testFullscreen = true;
        }

        public void ExitTestFullscreen()
        {
            if (!testFullscreen) return;
            Screen.SetResolution(previousWindowWidth > 0 ? previousWindowWidth : MenuWidth,
                previousWindowHeight > 0 ? previousWindowHeight : MenuHeight, FullScreenMode.Windowed);
            testFullscreen = false;
        }

        public bool HandleEscape(System.Action pauseOrInterruptActiveTest)
        {
            if (!testFullscreen) return false;
            pauseOrInterruptActiveTest?.Invoke();
            ExitTestFullscreen();
            return true;
        }
    }
}
