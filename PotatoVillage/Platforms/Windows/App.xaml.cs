using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PotatoVillage.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            // WinUI exceptions (including ones that would become 0xc000027b
            // "stowed exception" process crashes) don't reach
            // AppDomain.UnhandledException - this is the only hook that sees
            // them with a usable stack.
            this.UnhandledException += (s, e) =>
            {
                PotatoVillage.Services.CrashLogService.Write(
                    $"WinUI.UnhandledException (Message: {e.Message})", e.Exception);

                // Safety net for the known .NET MAUI bug: RecalculateSpanPositions
                // (span hit-region calculation on Windows) can loop until a List
                // overflows int.MaxValue and throws a bogus OutOfMemoryException -
                // memory is actually fine. The regions it computes are only used
                // for Span tap gestures, which this app never uses, so aborting
                // the calculation is harmless while letting it crash kills the
                // whole game. Gated tightly on that exact signature.
                if (e.Exception is OutOfMemoryException &&
                    (e.Exception.StackTrace?.Contains("RecalculateSpanPositions") ?? false))
                {
                    e.Handled = true;
                }
            };
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}
