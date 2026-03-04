using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PotatoVillage.Services
{
    /// <summary>
    /// Service for playing voiceover audio by matching text segments to audio files.
    /// Text like "女巫请睁眼" will be broken down into matching segments (e.g., "女巫" + "请睁眼")
    /// and played in sequence.
    /// </summary>
    public class VoiceoverService
    {
        private static VoiceoverService? _instance;
        public static VoiceoverService Instance => _instance ??= new VoiceoverService();

        private bool _isEnabled = false;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        // Dictionary mapping text segments to audio file names (without extension)
        // Audio files should be placed in Resources/Raw folder
        private readonly Dictionary<string, string> _voiceClips = new()
        {
            // Role names
            { "狼人", "langren" },
            { "女巫", "nvwu" },
            { "预言家", "yuyanjia" },
            { "猎人", "lieren" },
            { "守卫", "shouwei" },
            { "白痴", "baichi" },
            { "平民", "pingmin" },
            { "假面", "jiamian" },
            { "舞者", "wuzhe" },
            { "老鼠", "laoshu" },
            { "大猫", "damao" },
            { "幸运儿", "xingyuner" },
            { "摄梦人", "shemengren" },
            { "狼枪", "langqiang" },
            { "熊", "xiong" },
            { "转换者", "zhuanhuanzhe" },
            { "通灵师", "tonglingshi" },
            { "盗宝大师", "daobaodashi" },
            { "警长", "jingzhang" },
            
            // Common phrases
            { "请睁眼", "qing_zhengyan" },
            { "请闭眼", "qing_biyan" },
            { "天亮了", "tianliang" },
            { "天黑", "tianhei" },
            { "胜利", "shengli" },
            { "失败", "shibai" },
            { "好人", "haoren" },
            { "坏人", "huairen" },
            { "昨夜死亡", "zuoye_siwang" },
            { "所有人", "suoyouren" },
            { "请私下查看", "qing_sixia_chakan" },
            { "投票结果", "toupiao_jieguo" },
            { "请放下设备", "qing_fangxia_shebei" },
            { "游戏结束", "youxi_jieshu" },
            { "没有", "meiyou" },
            { "咆哮", "paoxiao" },
            
            // Numbers (for player references)
            { "一号", "yihao" },
            { "二号", "erhao" },
            { "三号", "sanhao" },
            { "四号", "sihao" },
            { "五号", "wuhao" },
            { "六号", "liuhao" },
            { "七号", "qihao" },
            { "八号", "bahao" },
            { "九号", "jiuhao" },
            { "十号", "shihao" },
            { "十一号", "shiyihao" },
            { "十二号", "shierhao" },
            { "1号", "yihao" },
            { "2号", "erhao" },
            { "3号", "sanhao" },
            { "4号", "sihao" },
            { "5号", "wuhao" },
            { "6号", "liuhao" },
            { "7号", "qihao" },
            { "8号", "bahao" },
            { "9号", "jiuhao" },
            { "10号", "shihao" },
            { "11号", "shiyihao" },
            { "12号", "shierhao" },
            { "1", "yihao" },
            { "2", "erhao" },
            { "3", "sanhao" },
            { "4", "sihao" },
            { "5", "wuhao" },
            { "6", "liuhao" },
            { "7", "qihao" },
            { "8", "bahao" },
            { "9", "jiuhao" },
            { "10", "shihao" },
            { "11", "shiyihao" },
            { "12", "shierhao" },
        };

        // Sorted keys by length (longest first) for greedy matching
        private List<string>? _sortedKeys;
        private List<string> SortedKeys => _sortedKeys ??= _voiceClips.Keys
            .OrderByDescending(k => k.Length)
            .ToList();

        private VoiceoverService() { }

        /// <summary>
        /// Registers a new voice clip mapping.
        /// </summary>
        /// <param name="text">The text segment to match</param>
        /// <param name="audioFileName">The audio file name (without extension)</param>
        public void RegisterVoiceClip(string text, string audioFileName)
        {
            _voiceClips[text] = audioFileName;
            _sortedKeys = null; // Invalidate cache
        }

        /// <summary>
        /// Removes a voice clip mapping.
        /// </summary>
        /// <param name="text">The text segment to remove</param>
        public void UnregisterVoiceClip(string text)
        {
            _voiceClips.Remove(text);
            _sortedKeys = null; // Invalidate cache
        }

        /// <summary>
        /// Parses input text and returns a list of matched segments with their audio file names.
        /// Uses greedy matching - tries to match the longest possible segments first.
        /// </summary>
        /// <param name="text">The text to parse</param>
        /// <returns>List of tuples containing (matched text, audio file name)</returns>
        public List<(string Text, string AudioFile)> ParseText(string text)
        {
            var result = new List<(string Text, string AudioFile)>();
            int position = 0;

            while (position < text.Length)
            {
                bool matched = false;

                // Try to match the longest possible segment
                foreach (var key in SortedKeys)
                {
                    if (position + key.Length <= text.Length &&
                        text.Substring(position, key.Length) == key)
                    {
                        result.Add((key, _voiceClips[key]));
                        position += key.Length;
                        matched = true;
                        break;
                    }
                }

                // If no match found, skip this character
                if (!matched)
                {
                    position++;
                }
            }

            return result;
        }

        /// <summary>
        /// Plays voiceover for the given text by matching and playing audio segments in sequence.
        /// </summary>
        /// <param name="text">The text to speak</param>
        public async Task PlayAsync(string text)
        {
            if (!IsEnabled)
                return;

            var segments = ParseText(text);
            
            foreach (var segment in segments)
            {
                await PlayAudioFileAsync(segment.AudioFile);
            }
        }

        /// <summary>
        /// Plays a single audio file. Override this method to implement actual audio playback.
        /// </summary>
        /// <param name="audioFileName">The audio file name (without extension)</param>
        protected virtual async Task PlayAudioFileAsync(string audioFileName)
        {
            try
            {
                // Try to play the audio file from Resources/Raw
                var audioFile = $"{audioFileName}.mp3";
                
                using var stream = await FileSystem.OpenAppPackageFileAsync($"Audio/{audioFile}");
                if (stream != null)
                {
                    await PlayStreamAsync(stream, audioFile);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to play audio file '{audioFileName}': {ex.Message}");
            }
        }

        private async Task PlayStreamAsync(Stream stream, string fileName)
        {
            // Use platform-specific audio playback
            // This is a basic implementation - can be enhanced with Plugin.Maui.Audio
            try
            {
#if ANDROID
                await PlayAudioAndroidAsync(stream);
#elif IOS
                await PlayAudioiOSAsync(stream);
#elif WINDOWS
                await PlayAudioWindowsAsync(stream);
#else
                await Task.Delay(500); // Fallback delay for unsupported platforms
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Audio playback error: {ex.Message}");
            }
        }

#if WINDOWS
        private Windows.Media.Playback.MediaPlayer? _windowsMediaPlayer;

        private async Task PlayAudioWindowsAsync(Stream stream)
        {
            var tempFile = Path.Combine(FileSystem.CacheDirectory, $"temp_audio_{Guid.NewGuid()}.mp3");
            try
            {
                using (var fileStream = File.Create(tempFile))
                {
                    await stream.CopyToAsync(fileStream);
                }

                var tcs = new TaskCompletionSource<bool>();

                _windowsMediaPlayer?.Dispose();
                _windowsMediaPlayer = new Windows.Media.Playback.MediaPlayer();

                var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(tempFile);
                _windowsMediaPlayer.Source = Windows.Media.Core.MediaSource.CreateFromStorageFile(storageFile);

                _windowsMediaPlayer.MediaEnded += (s, e) => tcs.TrySetResult(true);
                _windowsMediaPlayer.MediaFailed += (s, e) => tcs.TrySetResult(false);
                _windowsMediaPlayer.Play();

                await tcs.Task;
            }
            finally
            {
                _windowsMediaPlayer?.Dispose();
                _windowsMediaPlayer = null;
                try { File.Delete(tempFile); } catch { }
            }
        }
#endif

#if ANDROID
        private Android.Media.MediaPlayer? _mediaPlayer;
        
        private async Task PlayAudioAndroidAsync(Stream stream)
        {
            var tempFile = Path.Combine(FileSystem.CacheDirectory, $"temp_audio_{Guid.NewGuid()}.mp3");
            try
            {
                using (var fileStream = File.Create(tempFile))
                {
                    await stream.CopyToAsync(fileStream);
                }

                var tcs = new TaskCompletionSource<bool>();
                
                _mediaPlayer?.Release();
                _mediaPlayer = new Android.Media.MediaPlayer();
                _mediaPlayer.SetDataSource(tempFile);
                _mediaPlayer.Prepare();
                _mediaPlayer.Completion += (s, e) => tcs.TrySetResult(true);
                _mediaPlayer.Error += (s, e) => tcs.TrySetResult(false);
                _mediaPlayer.Start();

                await tcs.Task;
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
#endif

#if IOS
        private bool _audioSessionConfigured = false;

        private void ConfigureAudioSession()
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

        private async Task PlayAudioiOSAsync(Stream stream)
        {
            var tempFile = Path.Combine(FileSystem.CacheDirectory, $"temp_audio_{Guid.NewGuid()}.mp3");
            AVFoundation.AVAudioPlayer? player = null;

            try
            {
                // Write stream to temp file (can be done off main thread)
                using (var fileStream = File.Create(tempFile))
                {
                    await stream.CopyToAsync(fileStream);
                }

                var tcs = new TaskCompletionSource<bool>();

                // All AVFoundation operations must be on main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        // Configure audio session on main thread
                        ConfigureAudioSession();

                        var url = Foundation.NSUrl.FromFilename(tempFile);
                        if (url == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"iOS audio: Failed to create URL from {tempFile}");
                            tcs.TrySetResult(false);
                            return;
                        }

                        player = AVFoundation.AVAudioPlayer.FromUrl(url, out var error);

                        if (error != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"iOS audio error: {error.LocalizedDescription}");
                            tcs.TrySetResult(false);
                            return;
                        }

                        if (player == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"iOS audio: AVAudioPlayer is null");
                            tcs.TrySetResult(false);
                            return;
                        }

                        // Use delegate pattern instead of event to avoid disposal issues
                        player.FinishedPlaying += (s, e) => 
                        {
                            tcs.TrySetResult(e.Status);
                        };

                        if (!player.PrepareToPlay())
                        {
                            System.Diagnostics.Debug.WriteLine($"iOS audio: PrepareToPlay failed");
                            tcs.TrySetResult(false);
                            return;
                        }

                        if (!player.Play())
                        {
                            System.Diagnostics.Debug.WriteLine($"iOS audio: Play failed");
                            tcs.TrySetResult(false);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"iOS audio setup error: {ex.Message}");
                        tcs.TrySetResult(false);
                    }
                });

                // Wait for playback to complete with timeout (outside of main thread invoke)
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    System.Diagnostics.Debug.WriteLine($"iOS audio: Playback timed out");
                    player?.Stop();
                }

                // Small delay to ensure callback has completed before cleanup
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"iOS audio playback error: {ex.Message}");
            }
            finally
            {
                // Dispose player on main thread after a delay to ensure callbacks are done
                if (player != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        try
                        {
                            player.Stop();
                            player.Dispose();
                        }
                        catch { }
                    });
                }

                try { File.Delete(tempFile); } catch { }
            }
        }
#endif

        /// <summary>
        /// Gets all registered voice clips.
        /// </summary>
        public IReadOnlyDictionary<string, string> GetVoiceClips() => _voiceClips;

        /// <summary>
        /// Checks if a specific text segment has a registered voice clip.
        /// </summary>
        public bool HasVoiceClip(string text) => _voiceClips.ContainsKey(text);
    }
}
