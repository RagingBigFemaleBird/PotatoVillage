using Foundation;
using UIKit;

namespace PotatoVillage
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        public static UIInterfaceOrientationMask CurrentOrientation { get; set; } = UIInterfaceOrientationMask.Portrait;

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        [Export("application:supportedInterfaceOrientationsForWindow:")]
        public UIInterfaceOrientationMask GetSupportedInterfaceOrientations(UIApplication application, UIWindow forWindow)
        {
            return CurrentOrientation;
        }
    }
}
