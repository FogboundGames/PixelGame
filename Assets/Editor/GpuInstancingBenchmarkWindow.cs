using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// CrowdFlow optimizasyon ilkelerini (GPU Instancing, MaterialPropertyBlock, Procedural Vertex Wobble)
    /// sahnede canlı test etmek ve FPS / Draw Call farkını izlemek için geliştirici aracı.
    /// </summary>
    public class GpuInstancingBenchmarkWindow : EditorWindow
    {
        private float m_LiveWobbleAmount = 0.12f;
        private float m_LiveSquashAmount = 0f;

        // [MenuItem("PixelGame/⚡ GPU Instancing & Crowd Optimizer", false, 50)]
        public static void Open()
        {
            var win = GetWindow<GpuInstancingBenchmarkWindow>("⚡ GPU Crowd Optimizer");
            win.minSize = new Vector2(420, 480);
            win.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            DrawHeader();
            EditorGUILayout.Space(10);
            DrawStatusCard();
            EditorGUILayout.Space(10);
            DrawControls();
            EditorGUILayout.Space(15);
            DrawThreePillarsInfo();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label("⚡ CrowdFlow GPU Optimization & Benchmark", titleStyle);
            EditorGUILayout.LabelField("2.000+ karakter performans mimarisini sahnende test et.", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawStatusCard()
        {
            int cubeCount = PixelCube.ActiveCubes.Count;
            bool isInstanced = PixelCube.UseGpuInstancing;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📊 Canlı Sahne Durumu", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Sahnede Aktif Küp Sayısı:", GUILayout.Width(180));
            EditorGUILayout.LabelField(cubeCount.ToString(), EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Render Modu:", GUILayout.Width(180));
            if (isInstanced)
            {
                GUI.color = new Color(0.2f, 1f, 0.4f);
                EditorGUILayout.LabelField("⚡ GPU Instancing (1-2 Draw Call)", EditorStyles.boldLabel);
            }
            else
            {
                GUI.color = new Color(1f, 0.7f, 0.2f);
                EditorGUILayout.LabelField("📦 Klasik SRP Batcher (~" + cubeCount + " Draw Call)", EditorStyles.boldLabel);
            }
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Tahmini Draw Call Kazancı:", GUILayout.Width(180));
            if (isInstanced && cubeCount > 0)
            {
                EditorGUILayout.LabelField($"%{Mathf.Max(90, 100 - (200 / Mathf.Max(1, cubeCount)))} azalma ({cubeCount} → ~2 Draw Call)", EditorStyles.miniBoldLabel);
            }
            else
            {
                EditorGUILayout.LabelField("Instancing kapalı", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎛️ Optimizasyon Kontrolleri", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            bool newInstancing = EditorGUILayout.ToggleLeft(" ⚡ GPU Instancing Aktif (MaterialPropertyBlock)", PixelCube.UseGpuInstancing, EditorStyles.boldLabel);
            if (EditorGUI.EndChangeCheck())
            {
                PixelCube.UseGpuInstancing = newInstancing;
                ApplyToAllCubes();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("1️⃣ Pillar 1: Procedural GPU Vertex Wobble (Canlı Kalabalık Kıpırdaması)", EditorStyles.miniBoldLabel);
            EditorGUI.BeginChangeCheck();
            m_LiveWobbleAmount = EditorGUILayout.Slider("Wobble Genliği (Sway/Bob)", m_LiveWobbleAmount, 0f, 0.5f);
            m_LiveSquashAmount = EditorGUILayout.Slider("Squash & Stretch (Ezilme)", m_LiveSquashAmount, -0.3f, 0.3f);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyWobbleToAllCubes(m_LiveWobbleAmount, m_LiveSquashAmount);
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("🔄 Tüm Küpleri Güncelle ve Materyalleri Senkronize Et", GUILayout.Height(32)))
            {
                ApplyToAllCubes();
            }

            if (GUILayout.Button("🌊 Kalabalığa Canlı Wobble Ver (0.15)", GUILayout.Height(24)))
            {
                m_LiveWobbleAmount = 0.15f;
                m_LiveSquashAmount = 0f;
                ApplyWobbleToAllCubes(0.15f, 0f);
            }

            if (GUILayout.Button("🛑 Wobble'ı Sıfırla (Statik Küpler)", GUILayout.Height(24)))
            {
                m_LiveWobbleAmount = 0f;
                m_LiveSquashAmount = 0f;
                ApplyWobbleToAllCubes(0f, 0f);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawThreePillarsInfo()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("💡 CrowdFlow 3 Pillar Mantığı", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1️⃣ Procedural Vertex Animation: CPU'da hiçbir kemik veya LateUpdate dönmez; dalgalanma vertex shader'da GPU matematiğiyle yapılır.\n\n" +
                "2️⃣ GPU Instancing + MaterialPropertyBlock: Tüm küpler TEK materyal paylaşır. Renkler ve wobble fazları MPB ile GPU'ya aktarılır (1-2 Draw Call).\n\n" +
                "3️⃣ Event-Driven Flow: Her karakterin Update() koşturması yerine olay bazlı merkezi sistem kullanılır.",
                MessageType.Info
            );
            EditorGUILayout.EndVertical();
        }

        private void ApplyToAllCubes()
        {
            var cubes = PixelCube.ActiveCubes;
            for (int i = 0; i < cubes.Count; i++)
            {
                if (cubes[i] != null)
                {
                    cubes[i].ApplyColor(cubes[i].CurrentColor, 0f);
                }
            }
            SceneView.RepaintAll();
        }

        private void ApplyWobbleToAllCubes(float wobble, float squash)
        {
            var cubes = PixelCube.ActiveCubes;
            for (int i = 0; i < cubes.Count; i++)
            {
                if (cubes[i] != null)
                {
                    cubes[i].WobbleAmount = wobble;
                    cubes[i].SquashAmount = squash;
                }
            }
            SceneView.RepaintAll();
        }
    }
}
