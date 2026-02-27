using PotatoVillage.Services;

namespace PotatoVillage
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

            // Keep screen on when app starts
            window.Created += (s, e) => ScreenWakeService.KeepScreenOn();

            // Handle app lifecycle - keep screen on when resumed, allow off when stopped
            window.Resumed += (s, e) => ScreenWakeService.KeepScreenOn();
            window.Stopped += (s, e) => ScreenWakeService.AllowScreenOff();
            window.Destroying += (s, e) => ScreenWakeService.AllowScreenOff();

            return window;
        }
    }
}