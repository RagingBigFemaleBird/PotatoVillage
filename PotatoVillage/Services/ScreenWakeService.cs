namespace PotatoVillage.Services
{
    /// <summary>
    /// Service to keep the screen awake during gameplay.
    /// Prevents the device from automatically turning off the screen.
    /// </summary>
    public static class ScreenWakeService
    {
        /// <summary>
        /// Keeps the screen on and prevents it from turning off automatically.
        /// Call this when entering gameplay.
        /// </summary>
        public static void KeepScreenOn()
        {
#if ANDROID
            if (MainActivity.Instance != null)
            {
                MainActivity.Instance.RunOnUiThread(() =>
                {
                    MainActivity.Instance.Window?.AddFlags(Android.Views.WindowManagerFlags.KeepScreenOn);
                });
            }
#elif IOS || MACCATALYST
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UIKit.UIApplication.SharedApplication.IdleTimerDisabled = true;
            });
#endif
        }

        /// <summary>
        /// Allows the screen to turn off automatically again.
        /// Call this when leaving gameplay.
        /// </summary>
        public static void AllowScreenOff()
        {
#if ANDROID
            if (MainActivity.Instance != null)
            {
                MainActivity.Instance.RunOnUiThread(() =>
                {
                    MainActivity.Instance.Window?.ClearFlags(Android.Views.WindowManagerFlags.KeepScreenOn);
                });
            }
#elif IOS || MACCATALYST
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UIKit.UIApplication.SharedApplication.IdleTimerDisabled = false;
            });
#endif
        }
    }
}
