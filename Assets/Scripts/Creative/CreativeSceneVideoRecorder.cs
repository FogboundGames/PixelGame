using System;
using System.Collections;
using System.IO;
using System.Threading;
using UnityEngine;

namespace PixelGame.Creative
{
    [Serializable]
    public class RecordingMeta
    {
        public int fps = 30;
        public int width = 1080;
        public int height = 1920;
        public int frameCount = 0;
        public int sampleRate = 48000;
        public int channels = 2;
        public string audioFile = "audio_f32le.raw";
        public string lastFrame = "";
    }

    [RequireComponent(typeof(Camera))]
    public class CreativeSceneVideoRecorder : MonoBehaviour
    {
        public static CreativeSceneVideoRecorder Instance { get; private set; }

        public bool AutoStartOnAwake = true;
        public bool IsRecording { get; private set; } = false;

        private FileStream m_AudioStream;
        private RenderTexture m_RecordRT;
        private Texture2D m_ReadTex;
        private Camera m_Camera;
        private int m_FrameIndex = 0;
        private string m_OutputFolder;
        private int m_SampleRate = 48000;
        private int m_Channels = 2;
        private int m_PendingWrites = 0;

        private void Awake()
        {
            Instance = this;
            m_Camera = GetComponent<Camera>();

            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;
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
            Directory.CreateDirectory(Path.Combine(m_OutputFolder, "frames"));

            m_SampleRate = AudioSettings.outputSampleRate;
            string audioPath = Path.Combine(m_OutputFolder, "audio_f32le.raw");
            m_AudioStream = new FileStream(audioPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 65536);

            m_RecordRT = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32);
            m_RecordRT.name = "Recorder_RT_1080x1920";
            m_ReadTex = new Texture2D(1080, 1920, TextureFormat.RGB24, false);

            if (m_Camera != null)
            {
                m_Camera.targetTexture = m_RecordRT;
            }

            m_FrameIndex = 0;
            m_PendingWrites = 0;
            IsRecording = true;

            Debug.Log($"<color=#00FFAA><b>[VideoRecorder]</b></color> Kayıt başladı: 1080x1920 @ 30fps -> {m_OutputFolder}");
            StartCoroutine(CaptureLoop());
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
                Debug.LogWarning("[VideoRecorder] Audio write exception: " + ex.Message);
            }
        }

        private IEnumerator CaptureLoop()
        {
            while (IsRecording)
            {
                yield return new WaitForEndOfFrame();
                if (!IsRecording) break;

                RenderTexture prevActive = RenderTexture.active;
                RenderTexture.active = m_RecordRT;

                m_ReadTex.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
                m_ReadTex.Apply();

                RenderTexture.active = prevActive;

                byte[] jpgBytes = m_ReadTex.EncodeToJPG(92);
                string framePath = Path.Combine(m_OutputFolder, "frames", $"f_{m_FrameIndex:D5}.jpg");
                m_FrameIndex++;

                Interlocked.Increment(ref m_PendingWrites);
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        File.WriteAllBytes(framePath, jpgBytes);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("[VideoRecorder] Frame write failed: " + ex.Message);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref m_PendingWrites);
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
            Debug.Log($"<color=#00FFAA><b>[VideoRecorder]</b></color> Kayıt durduruldu. {m_FrameIndex} kare yakalandı, disk yazımları bekleniyor...");

            // Kalan disk yazımlarını bekle
            float waitTimeout = 10f;
            while (m_PendingWrites > 0 && waitTimeout > 0f)
            {
                waitTimeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (m_Camera != null)
            {
                m_Camera.targetTexture = null;
            }

            if (m_AudioStream != null)
            {
                m_AudioStream.Flush();
                m_AudioStream.Close();
                m_AudioStream = null;
            }

            if (m_RecordRT != null)
            {
                m_RecordRT.Release();
                Destroy(m_RecordRT);
            }

            if (m_ReadTex != null)
            {
                Destroy(m_ReadTex);
            }

            // Metadata kaydet
            var meta = new RecordingMeta
            {
                fps = 30,
                width = 1080,
                height = 1920,
                frameCount = m_FrameIndex,
                sampleRate = m_SampleRate,
                channels = m_Channels,
                audioFile = "audio_f32le.raw",
                lastFrame = $"frames/f_{Mathf.Max(0, m_FrameIndex - 1):D5}.jpg"
            };

            string metaJson = JsonUtility.ToJson(meta, true);
            File.WriteAllText(Path.Combine(m_OutputFolder, "metadata.json"), metaJson);
            File.WriteAllText(Path.Combine(m_OutputFolder, "DONE.flag"), "done");

            Debug.Log($"<color=#00FFAA><b>[VideoRecorder]</b></color> Tamamlandı! Metadata yazıldı: {m_OutputFolder}/metadata.json");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void OnDestroy()
        {
            if (IsRecording)
            {
                StopRecording();
            }
        }
    }
}
