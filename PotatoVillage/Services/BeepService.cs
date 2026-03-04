using System;
using System.Threading.Tasks;

#if WINDOWS
using Windows.Media.Core;
using Windows.Media.Playback;
#endif

namespace PotatoVillage.Services
{
    /// <summary>
    /// Cross-platform service for playing warning beep sounds.
    /// </summary>
    public static class BeepService
    {
        private static bool _isPlaying = false;

        /// <summary>
        /// Plays a warning beep sound.
        /// </summary>
        public static async Task PlayWarningBeepAsync()
        {
            if (_isPlaying) return;
            _isPlaying = true;

            try
            {
#if WINDOWS
                await PlayWindowsBeepAsync();
#elif ANDROID
                await PlayAndroidBeepAsync();
#elif IOS || MACCATALYST
                await PlayiOSBeepAsync();
#else
                // Fallback - just delay
                await Task.Delay(100);
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BeepService error: {ex.Message}");
            }
            finally
            {
                _isPlaying = false;
            }
        }

#if WINDOWS
        private static async Task PlayWindowsBeepAsync()
        {
            try
            {
                // Play multiple beeps for attention
                for (int i = 0; i < 1; i++)
                {
                    Console.Beep(1000, 150); // 1000 Hz for 150ms
                    await Task.Delay(100);
                }
            }
            catch
            {
                // Console.Beep might not work in all contexts
            }
        }
#endif

#if ANDROID
        private static async Task PlayAndroidBeepAsync()
        {
            try
            {
                var context = Android.App.Application.Context;
                var toneGenerator = new Android.Media.ToneGenerator(
                    Android.Media.Stream.Alarm, 
                    100 // Volume percentage
                );
                
                for (int i = 0; i < 1; i++)
                {
                    toneGenerator.StartTone(Android.Media.Tone.PropBeep, 150);
                    await Task.Delay(250);
                }
                
                toneGenerator.Release();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Android beep error: {ex.Message}");
            }
        }
#endif

#if IOS || MACCATALYST
        private static bool _audioSessionConfigured = false;

        private static void ConfigureAudioSession()
        {
            if (_audioSessionConfigured) return;

            try
            {
                var audioSession = AVFoundation.AVAudioSession.SharedInstance();

                Foundation.NSError? setCategoryError;
                audioSession.SetCategory(AVFoundation.AVAudioSession.CategoryPlayback, out setCategoryError);
                if (setCategoryError != null)
                {
                    System.Diagnostics.Debug.WriteLine($"iOS SetCategory error: {setCategoryError.LocalizedDescription}");
                    return;
                }

                Foundation.NSError? setActiveError;
                audioSession.SetActive(true, out setActiveError);
                if (setActiveError != null)
                {
                    System.Diagnostics.Debug.WriteLine($"iOS SetActive error: {setActiveError.LocalizedDescription}");
                    return;
                }

                _audioSessionConfigured = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to configure iOS audio session: {ex.Message}");
            }
        }

        private static async Task PlayiOSBeepAsync()
        {
            try
            {
                // iOS audio operations should be on main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        // Configure audio session first
                        ConfigureAudioSession();

                        // Use AudioToolbox for system sound
                        AudioToolbox.SystemSound.Vibrate.PlayAlertSound();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"iOS beep error (main thread): {ex.Message}");
                    }
                });

                await Task.Delay(250);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"iOS beep error: {ex.Message}");
            }
        }
#endif
    }
}
