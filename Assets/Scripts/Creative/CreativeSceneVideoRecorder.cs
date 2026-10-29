using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace PixelGame.Creative
{
    [Serializable]
    public class RecordingMeta
    {
        public int fps = 60;
        public int width = 1080;
        public int height = 1920;
        public int frameCount = 0;
        public int sampleRate = 48000;
        public int channels = 2;
        public string audioFile = "audio_f32le.raw";
        public string lastFrame = "last_frame.jpg";
        public string videoFile = "gameplay_60fps.mp4";
    }

    public struct FramePacket
    {
        public int Index;
        public byte[] Data;
    }

    [RequireComponent(typeof(Camera))]
    public class CreativeSceneVideoRecorder : MonoBehaviour
    {
        public static CreativeSceneVideoRecorder Instance { get; private set; }

        public bool AutoStartOnAwake = true;
        public bool IsRecording { get; private set; } = false;

        private FileStream m_AudioStream;
        private RenderTexture m_RecordRT;
        private Camera m_Camera;
        private int m_FrameIndex = 0;
        private string m_OutputFolder;
        private int m_SampleRate = 48000;
        private int m_Channels = 2;

        private Process m_FfmpegProcess;
        private Stream m_FfmpegStream;
        private BlockingCollection<FramePacket> m_FrameQueue;
        private ConcurrentBag<byte[]> m_BufferPool = new ConcurrentBag<byte[]>();
        private Thread m_WriterThread;
        private int m_PendingReadbacks = 0;

        private void Awake()
        {
            Instance = this;
            m_Camera = GetComponent<Camera>();

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Time.captureFramerate = 60;
        }

        private void Start()
        {
            string flagPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../scratch/is_recording.flag"));
            bool flagExists = File.Exists(flagPath);
            if (flagExists)
            {
                try { File.Delete(flagPath); } catch {}
            }

            if (flagExists || AutoStartOnAwake)
            {
                StartRecording();
            }
        }

        public void StartRecording()
        {
            if (IsRecording) return;

            m_OutputFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "../scratch/gameplay_recording"));
            if (Directory.Exists(m_OutputFolder))
            {
                try { Directory.Delete(m_OutputFolder, true); } catch {}
            }
            Directory.CreateDirectory(m_OutputFolder);

            m_SampleRate = AudioSettings.outputSampleRate;
            string audioPath = Path.Combine(m_OutputFolder, "audio_f32le.raw");
            m_AudioStream = new FileStream(audioPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 65536);

            m_RecordRT = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32);
            m_RecordRT.name = "Recorder_RT_1080x1920";
            m_RecordRT.Create();

            if (m_Camera != null)
            {
                m_Camera.targetTexture = m_RecordRT;
            }

            // Start ffmpeg direct rawvideo pipe (RGBA 60 FPS)
            string videoPath = Path.Combine(m_OutputFolder, "gameplay_60fps.mp4");
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -loglevel warning -f rawvideo -pix_fmt rgba -s 1080x1920 -r 60 -i - -vf vflip -c:v libx264 -preset ultrafast -crf 16 -pix_fmt yuv420p \"{videoPath}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };
            m_FfmpegProcess = Process.Start(psi);
            m_FfmpegStream = m_FfmpegProcess.StandardInput.BaseStream;

            m_FrameQueue = new BlockingCollection<FramePacket>(boundedCapacity: 120);
            m_WriterThread = new Thread(FfmpegWriterWorker) { IsBackground = true, Name = "FfmpegVideoWriter" };
            m_WriterThread.Start();

            m_FrameIndex = 0;
            m_PendingReadbacks = 0;
            IsRecording = true;

            UnityEngine.Debug.Log($"<color=#00FFAA><b>[VideoRecorder]</b></color> Direct 60 FPS Async Streaming Başladı: 1080x1920 @ 60fps -> {videoPath}");
            StartCoroutine(CaptureLoop());
        }

        private byte[] GetPooledBuffer(int size)
        {
            if (m_BufferPool.TryTake(out byte[] buf) && buf.Length == size)
                return buf;
            return new byte[size];
        }

        private void ReturnPooledBuffer(byte[] buf)
        {
            if (m_BufferPool.Count < 60)
                m_BufferPool.Add(buf);
        }

        private void FfmpegWriterWorker()
        {
            int nextFrame = 0;
            var pending = new SortedDictionary<int, byte[]>();

            try
            {
                while (!m_FrameQueue.IsCompleted || pending.Count > 0)
                {
                    if (m_FrameQueue.TryTake(out FramePacket packet, 200))
                    {
                        pending[packet.Index] = packet.Data;
                    }

                    while (pending.TryGetValue(nextFrame, out byte[] data))
                    {
                        pending.Remove(nextFrame);
                        if (m_FfmpegStream != null && data != null)
                        {
                            m_FfmpegStream.Write(data, 0, data.Length);
                        }
                        ReturnPooledBuffer(data);
                        nextFrame++;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[VideoRecorder] Writer worker exception: " + ex.Message);
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            m_Channels = channels;
            if (!IsRecording || m_AudioStream == null) return;

            try
            {
                byte[] byteBuf = new byte[data.Length * 4];
                Buffer.BlockCopy(data, 0, byteBuf, 0, byteBuf.Length);
                m_AudioStream.Write(byteBuf, 0, byteBuf.Length);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[VideoRecorder] Audio write exception: " + ex.Message);
            }
        }

        private IEnumerator CaptureLoop()
        {
            while (IsRecording)
            {
                yield return new WaitForEndOfFrame();
                if (!IsRecording) break;

                int currentIdx = m_FrameIndex++;
                Interlocked.Increment(ref m_PendingReadbacks);

                AsyncGPUReadback.Request(m_RecordRT, 0, TextureFormat.RGBA32, req =>
                {
                    try
                    {
                        if (req.hasError || !IsRecording) return;
                        var rawData = req.GetData<byte>();
                        byte[] buffer = GetPooledBuffer(rawData.Length);
                        rawData.CopyTo(buffer);
                        m_FrameQueue.TryAdd(new FramePacket { Index = currentIdx, Data = buffer });
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning("[VideoRecorder] Readback callback exception: " + ex.Message);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref m_PendingReadbacks);
                    }
                });
            }
        }

        public void StopRecording()
        {
            if (!IsRecording) return;
            IsRecording = false;

            StartCoroutine(FinalizeRoutine());
        }

        private IEnumerator FinalizeRoutine()
        {
            UnityEngine.Debug.Log($"<color=#00FFAA><b>[VideoRecorder]</b></color> Kayıt durduruldu. {m_FrameIndex} kare talep edildi, kalan GPU readback'ler bekleniyor...");

            // Kalan GPU readback isteklerinin tamamlanmasını bekle (en fazla 2 sn)
            float waitTimer = 0f;
            while (m_PendingReadbacks > 0 && waitTimer < 2.0f)
            {
                waitTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            // Time.captureFramerate normale döndür
            Time.captureFramerate = 0;

            if (m_Camera != null)
            {
                m_Camera.targetTexture = null;
            }

            // Queue flush
            if (m_FrameQueue != null)
            {
                m_FrameQueue.CompleteAdding();
            }

            if (m_WriterThread != null && m_WriterThread.IsAlive)
            {
                m_WriterThread.Join(15000);
            }

            if (m_FfmpegStream != null)
            {
                try
                {
                    m_FfmpegStream.Flush();
                    m_FfmpegStream.Close();
                }
                catch {}
                m_FfmpegStream = null;
            }

            if (m_FfmpegProcess != null)
            {
                try
                {
                    m_FfmpegProcess.WaitForExit(10000);
                    m_FfmpegProcess.Dispose();
                }
                catch {}
                m_FfmpegProcess = null;
            }

            if (m_AudioStream != null)
            {
                try
                {
                    m_AudioStream.Flush();
                    m_AudioStream.Close();
                }
                catch {}
                m_AudioStream = null;
            }

            if (m_RecordRT != null)
            {
                m_RecordRT.Release();
                Destroy(m_RecordRT);
            }

            // Extract pristine last frame from gameplay_60fps.mp4 using ffmpeg
            string videoPath = Path.Combine(m_OutputFolder, "gameplay_60fps.mp4");
            string lastFramePath = Path.Combine(m_OutputFolder, "last_frame.jpg");
            try
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -loglevel warning -sseof -0.05 -i \"{videoPath}\" -frames:v 1 -update 1 \"{lastFramePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                p?.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[VideoRecorder] Failed to extract last_frame.jpg with ffmpeg: " + ex.Message);
            }

            // Metadata kaydet
            var meta = new RecordingMeta
            {
                fps = 60,
                width = 1080,
                height = 1920,
                frameCount = m_FrameIndex,
                sampleRate = m_SampleRate,
                channels = m_Channels,
                audioFile = "audio_f32le.raw",
                lastFrame = "last_frame.jpg",
                videoFile = "gameplay_60fps.mp4"
            };

            string metaJson = JsonUtility.ToJson(meta, true);
            File.WriteAllText(Path.Combine(m_OutputFolder, "metadata.json"), metaJson);
            File.WriteAllText(Path.Combine(m_OutputFolder, "DONE.flag"), "done");

            UnityEngine.Debug.Log($"<color=#00FFAA><b>[VideoRecorder]</b></color> Tamamlandı! Gerçek 60 FPS Video: {m_OutputFolder}/gameplay_60fps.mp4");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
            yield break;
        }

        private void OnDestroy()
        {
            Time.captureFramerate = 0;
            if (IsRecording)
            {
                StopRecording();
            }
        }
    }
}
