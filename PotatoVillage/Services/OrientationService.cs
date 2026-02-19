namespace PotatoVillage.Services
{
    public static class OrientationService
    {
        public static void LockPortrait()
        {
#if ANDROID
            if (MainActivity.Instance != null)
            {
                MainActivity.Instance.SetOrientation(Android.Content.PM.ScreenOrientation.Portrait);
            }
#elif IOS
            AppDelegate.CurrentOrientation = UIKit.UIInterfaceOrientationMask.Portrait;
            SetiOSOrientation(UIKit.UIInterfaceOrientationMask.Portrait, UIKit.UIInterfaceOrientation.Portrait);
#endif
        }

        public static void LockLandscape()
        {
#if ANDROID
            if (MainActivity.Instance != null)
            {
                MainActivity.Instance.SetOrientation(Android.Content.PM.ScreenOrientation.Landscape);
            }
#elif IOS
            AppDelegate.CurrentOrientation = UIKit.UIInterfaceOrientationMask.Landscape;
            SetiOSOrientation(UIKit.UIInterfaceOrientationMask.Landscape, UIKit.UIInterfaceOrientation.LandscapeRight);
#endif
        }

#if IOS
        private static void SetiOSOrientation(UIKit.UIInterfaceOrientationMask orientationMask, UIKit.UIInterfaceOrientation orientation)
        {
            if (OperatingSystem.IsIOSVersionAtLeast(16))
            {
                var windowScene = UIKit.UIApplication.SharedApplication?.ConnectedScenes?
                    .ToArray<UIKit.UIScene>()?
                    .FirstOrDefault(s => s is UIKit.UIWindowScene) as UIKit.UIWindowScene;

                if (windowScene != null)
                {
                    var geometryPreferences = new UIKit.UIWindowSceneGeometryPreferencesIOS(orientationMask);
                    windowScene.RequestGeometryUpdate(geometryPreferences, null);
                }
            }
            else
            {
                UIKit.UIDevice.CurrentDevice.SetValueForKey(
                    Foundation.NSNumber.FromInt32((int)orientation),
                    new Foundation.NSString("orientation"));
            }
        }
#endif
    }
}
