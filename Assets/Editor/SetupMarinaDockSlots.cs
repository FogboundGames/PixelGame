using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupMarinaDockSlots
    {
        private const string MaterialDir = "Assets/Materials/Marina";
        private const string MeshDir = "Assets/Meshes/Marina";
        private const string TextureDir = "Assets/Textures/Marina";
        private const string ScenePath = "Assets/Scenes/Gemi.unity";
        private const string VersionKey = "MarinaSlots_Konsept2_Applied_20260930_1";

        static SetupMarinaDockSlots()
        {
            EditorApplication.delayCall += AutoSetupIfNeeded;
        }

        private static void AutoSetupIfNeeded()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(VersionKey, false)) return;
            SessionState.SetBool(VersionKey, true);
            SetupMarinaSlots();
        }

        [MenuItem("PixelGame/⚓ Setup Marina Dock Slots (Konsept 2)")]
        public static void SetupMarinaSlots()
        {
            EnsureDirectories();
            Material buoyMat = GetOrCreateBuoyMaterial();
            Material chainMat = GetOrCreateChainMaterial();
            Material[] signMats = GetOrCreateSignMaterials();

            Mesh buoyMesh = GetOrCreateBuoyMesh();
            Mesh chainSideMesh = GetOrCreateChainMesh(1.04f, 9, 0.015f);
            Mesh chainBackMesh = GetOrCreateChainMesh(1.04f, 9, 0.015f);
            Mesh signMesh = GetOrCreateSignMesh();

            // Sahnede slotları bul ve güncelle
            if (!EditorSceneManager.GetActiveScene().path.Equals(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            var slots = Object.FindObjectsByType<ShipSlot>(FindObjectsSortMode.None);
            System.Array.Sort(slots, (a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

            Debug.Log($"[MarinaDock] {slots.Length} adet ShipSlot bulundu. Konsept 2 uygulanıyor...");

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;

                int slotNum = slot.SlotIndex + 1; // 1-indexed (1, 2, 3, 4, 5)

                // 1. Eski IndicatorMesh (beyaz kesikli kutu) veya [Marina_Berth] olmayan diğer göstergeler deaktive edilir
                for (int c = 0; c < slot.transform.childCount; c++)
                {
                    Transform child = slot.transform.GetChild(c);
                    if (child.name != "[Marina_Berth]")
                    {
                        child.gameObject.SetActive(false);
                    }
                }

                // 2. Varsa eski [Marina_Berth] temizlenir
                Transform existingBerth = slot.transform.Find("[Marina_Berth]");
                if (existingBerth != null)
                {
                    Undo.DestroyObjectImmediate(existingBerth.gameObject);
                }

                // 3. Yeni [Marina_Berth] container oluştur
                GameObject berthGo = new GameObject("[Marina_Berth]");
                Undo.RegisterCreatedObjectUndo(berthGo, "Create Marina Berth");
                berthGo.transform.SetParent(slot.transform, false);
                berthGo.transform.localPosition = Vector3.zero;
                berthGo.transform.localRotation = Quaternion.identity;
                berthGo.transform.localScale = Vector3.one;

                float halfW = 0.52f;
                float halfL = 0.52f;
                float waterY = 0.04f;

                // 4. Dört köşeye yüzen kırmızı-beyaz şamandıralar
                Vector3 fl = new Vector3(-halfW, waterY, -halfL);
                Vector3 fr = new Vector3(halfW, waterY, -halfL);
                Vector3 bl = new Vector3(-halfW, waterY, halfL);
                Vector3 br = new Vector3(halfW, waterY, halfL);

                CreateBuoy("Buoy_Front_Left", fl, berthGo.transform, buoyMesh, buoyMat, 0.0f);
                CreateBuoy("Buoy_Front_Right", fr, berthGo.transform, buoyMesh, buoyMat, 0.8f);
                CreateBuoy("Buoy_Back_Left", bl, berthGo.transform, buoyMesh, buoyMat, 1.6f);
                CreateBuoy("Buoy_Back_Right", br, berthGo.transform, buoyMesh, buoyMat, 2.4f);

                // 5. Su yüzeyinde yüzen deniz zincirleri (Sol yan, Sağ yan, Arka sahil tarafı)
                // Sol zincir (fl -> bl)
                CreateChain("Chain_Left", fl, bl, berthGo.transform, chainSideMesh, chainMat);
                // Sağ zincir (fr -> br)
                CreateChain("Chain_Right", fr, br, berthGo.transform, chainSideMesh, chainMat);
                // Arka zincir (bl -> br)
                CreateChain("Chain_Back", bl, br, berthGo.transform, chainBackMesh, chainMat);

                // 6. Girişteki şık ahşap numara tabelası (1, 2, 3, 4, 5)
                Material slotSignMat = signMats[Mathf.Clamp(slotNum - 1, 0, signMats.Length - 1)];
                Vector3 signPos = new Vector3(0f, waterY + 0.04f, -halfL - 0.08f);
                CreateSignPlate($"Sign_Slot_{slotNum}", signPos, berthGo.transform, signMesh, slotSignMat);

                Debug.Log($"[MarinaDock] WaterSlot_{slotNum} için Konsept 2 yüzen şamandıra ve zincir ızgarası başarıyla kuruldu.");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[MarinaDock] Sahne kaydedildi! Konsept 2 kurulumu eksiksiz tamamlandı.");
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder(MaterialDir))
            {
                AssetDatabase.CreateFolder("Assets/Materials", "Marina");
            }
            if (!AssetDatabase.IsValidFolder(MeshDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Meshes")) AssetDatabase.CreateFolder("Assets", "Meshes");
                AssetDatabase.CreateFolder("Assets/Meshes", "Marina");
            }
        }

        private static Material GetOrCreateBuoyMaterial()
        {
            string path = $"{MaterialDir}/Marina_Buoy_Mat.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                mat.name = "Marina_Buoy_Mat";

                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/Buoy_Striped_Tex.png");
                if (tex != null)
                {
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetTexture("_MainTex", tex);
                }
                mat.SetFloat("_Smoothness", 0.35f);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static Material GetOrCreateChainMaterial()
        {
            string path = $"{MaterialDir}/Marina_Chain_Mat.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                mat.name = "Marina_Chain_Mat";
                Color ironColor = new Color(0.20f, 0.22f, 0.25f, 1f);
                mat.SetColor("_BaseColor", ironColor);
                mat.SetColor("_Color", ironColor);
                mat.SetFloat("_Metallic", 0.85f);
                mat.SetFloat("_Smoothness", 0.40f);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static Material[] GetOrCreateSignMaterials()
        {
            Material[] mats = new Material[5];
            for (int i = 1; i <= 5; i++)
            {
                string path = $"{MaterialDir}/Marina_Sign_Slot_{i}_Mat.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    mat = new Material(shader);
                    mat.name = $"Marina_Sign_Slot_{i}_Mat";

                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/Sign_Slot_{i}.png");
                    if (tex != null)
                    {
                        mat.SetTexture("_BaseMap", tex);
                        mat.SetTexture("_MainTex", tex);
                    }
                    mat.SetFloat("_Smoothness", 0.20f);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mats[i - 1] = mat;
            }
            return mats;
        }

        private static Mesh GetOrCreateBuoyMesh()
        {
            Mesh objMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Marina/Buoy.obj");
            if (objMesh != null) return objMesh;

            string path = $"{MeshDir}/Marina_Buoy_Mesh.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;

            mesh = new Mesh();
            mesh.name = "Marina_Buoy_Mesh";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            int radialSegments = 16;
            int heightSegments = 10;
            float radius = 0.12f;
            float height = 0.18f;

            // Rounded capsule / barrel
            for (int y = 0; y <= heightSegments; y++)
            {
                float v = (float)y / heightSegments;
                float angleV = (v - 0.5f) * Mathf.PI; // -pi/2 to +pi/2
                float r = radius * Mathf.Cos(angleV * 0.75f);
                float posY = Mathf.Sin(angleV) * (height * 0.5f);

                for (int x = 0; x <= radialSegments; x++)
                {
                    float u = (float)x / radialSegments;
                    float angleH = u * Mathf.PI * 2f;

                    float posX = Mathf.Cos(angleH) * r;
                    float posZ = Mathf.Sin(angleH) * r;

                    Vector3 pos = new Vector3(posX, posY, posZ);
                    Vector3 norm = new Vector3(posX, posY * 0.5f, posZ).normalized;

                    verts.Add(pos);
                    norms.Add(norm);
                    uvs.Add(new Vector2(u, v));
                }
            }

            for (int y = 0; y < heightSegments; y++)
            {
                for (int x = 0; x < radialSegments; x++)
                {
                    int current = y * (radialSegments + 1) + x;
                    int next = current + radialSegments + 1;

                    tris.Add(current);
                    tris.Add(next);
                    tris.Add(current + 1);

                    tris.Add(current + 1);
                    tris.Add(next);
                    tris.Add(next + 1);
                }
            }

            // Top mooring eyelet ring
            int ringSegments = 12;
            int ringTubeSegs = 6;
            float ringR = 0.038f;
            float tubeR = 0.010f;
            Vector3 ringCenter = new Vector3(0f, height * 0.5f + ringR, 0f);
            int baseIdx = verts.Count;

            for (int r = 0; r < ringSegments; r++)
            {
                float phi = (float)r / ringSegments * Mathf.PI * 2f;
                Vector3 c = ringCenter + new Vector3(Mathf.Cos(phi) * ringR, Mathf.Sin(phi) * ringR, 0f);

                for (int t = 0; t < ringTubeSegs; t++)
                {
                    float theta = (float)t / ringTubeSegs * Mathf.PI * 2f;
                    Vector3 offset = new Vector3(
                        Mathf.Cos(phi) * Mathf.Cos(theta) * tubeR,
                        Mathf.Sin(phi) * Mathf.Cos(theta) * tubeR,
                        Mathf.Sin(theta) * tubeR
                    );

                    verts.Add(c + offset);
                    norms.Add(offset.normalized);
                    uvs.Add(new Vector2((float)r / ringSegments, 0.15f));
                }
            }

            for (int r = 0; r < ringSegments; r++)
            {
                int nextR = (r + 1) % ringSegments;
                for (int t = 0; t < ringTubeSegs; t++)
                {
                    int nextT = (t + 1) % ringTubeSegs;

                    int i0 = baseIdx + r * ringTubeSegs + t;
                    int i1 = baseIdx + nextR * ringTubeSegs + t;
                    int i2 = baseIdx + nextR * ringTubeSegs + nextT;
                    int i3 = baseIdx + r * ringTubeSegs + nextT;

                    tris.Add(i0);
                    tris.Add(i1);
                    tris.Add(i2);

                    tris.Add(i0);
                    tris.Add(i2);
                    tris.Add(i3);
                }
            }

            mesh.vertices = verts.ToArray();
            mesh.normals = norms.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds();

            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Mesh GetOrCreateChainMesh(float length, int linkCount, float sag)
        {
            Mesh objMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Marina/Chain.obj");
            if (objMesh != null) return objMesh;

            string path = $"{MeshDir}/Marina_Chain_L{Mathf.RoundToInt(length * 100)}_Mesh.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;

            mesh = new Mesh();
            mesh.name = $"Marina_Chain_{Mathf.RoundToInt(length * 100)}";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            float linkStep = length / (linkCount - 1);
            float linkLen = linkStep * 0.95f;
            float linkRadius = 0.024f;
            float wireRadius = 0.0075f;

            for (int i = 0; i < linkCount; i++)
            {
                float t = (float)i / (linkCount - 1);
                float z = t * length;
                float sagY = -Mathf.Sin(t * Mathf.PI) * sag; // Tatlı sarkma eğrisi
                Vector3 linkCenter = new Vector3(0f, sagY, z);

                bool isVertical = (i % 2 == 1);
                GenerateChainLink(verts, norms, uvs, tris, linkCenter, linkLen * 0.5f, linkRadius, wireRadius, isVertical);
            }

            mesh.vertices = verts.ToArray();
            mesh.normals = norms.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds();

            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void GenerateChainLink(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
            Vector3 center, float halfLen, float linkRadius, float wireRadius, bool isVertical)
        {
            int baseIdx = verts.Count;
            int ringSegs = 14;
            int tubeSegs = 6;

            for (int r = 0; r < ringSegs; r++)
            {
                float angle = (float)r / ringSegs * Mathf.PI * 2f;
                float localZ = Mathf.Sin(angle) * halfLen;
                float localX = Mathf.Cos(angle) * linkRadius;
                Vector3 ringPt = new Vector3(localX, 0f, localZ);

                for (int t = 0; t < tubeSegs; t++)
                {
                    float tubeAngle = (float)t / tubeSegs * Mathf.PI * 2f;
                    Vector3 wireOffset = new Vector3(
                        Mathf.Cos(angle) * Mathf.Cos(tubeAngle) * wireRadius,
                        Mathf.Sin(tubeAngle) * wireRadius,
                        Mathf.Sin(angle) * Mathf.Cos(tubeAngle) * wireRadius
                    );

                    Vector3 pt = ringPt + wireOffset;
                    if (isVertical)
                    {
                        // Rotate 90 deg around Z
                        pt = new Vector3(-pt.y, pt.x, pt.z);
                    }

                    verts.Add(center + pt);
                    norms.Add((isVertical ? new Vector3(-wireOffset.y, wireOffset.x, wireOffset.z) : wireOffset).normalized);
                    uvs.Add(new Vector2((float)r / ringSegs, (float)t / tubeSegs));
                }
            }

            for (int r = 0; r < ringSegs; r++)
            {
                int nextR = (r + 1) % ringSegs;
                for (int t = 0; t < tubeSegs; t++)
                {
                    int nextT = (t + 1) % tubeSegs;

                    int i0 = baseIdx + r * tubeSegs + t;
                    int i1 = baseIdx + nextR * tubeSegs + t;
                    int i2 = baseIdx + nextR * tubeSegs + nextT;
                    int i3 = baseIdx + r * tubeSegs + nextT;

                    tris.Add(i0);
                    tris.Add(i1);
                    tris.Add(i2);

                    tris.Add(i0);
                    tris.Add(i2);
                    tris.Add(i3);
                }
            }
        }

        private static Mesh GetOrCreateSignMesh()
        {
            Mesh objMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Marina/Sign.obj");
            if (objMesh != null) return objMesh;

            string path = $"{MeshDir}/Marina_Sign_Mesh.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;

            mesh = new Mesh();
            mesh.name = "Marina_Sign_Mesh";

            float w = 0.28f;
            float h = 0.22f;
            float d = 0.03f;
            float hw = w * 0.5f;
            float hh = h * 0.5f;
            float hd = d * 0.5f;

            // Box with UV mapped on front face
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            // Front face (+Z)
            int b0 = verts.Count;
            verts.Add(new Vector3(-hw, -hh, hd)); norms.Add(Vector3.forward); uvs.Add(new Vector2(0, 0));
            verts.Add(new Vector3(hw, -hh, hd));  norms.Add(Vector3.forward); uvs.Add(new Vector2(1, 0));
            verts.Add(new Vector3(hw, hh, hd));   norms.Add(Vector3.forward); uvs.Add(new Vector2(1, 1));
            verts.Add(new Vector3(-hw, hh, hd));  norms.Add(Vector3.forward); uvs.Add(new Vector2(0, 1));
            tris.Add(b0); tris.Add(b0 + 2); tris.Add(b0 + 1);
            tris.Add(b0); tris.Add(b0 + 3); tris.Add(b0 + 2);

            // Back face (-Z)
            int b1 = verts.Count;
            verts.Add(new Vector3(-hw, -hh, -hd)); norms.Add(Vector3.back); uvs.Add(new Vector2(0.5f, 0.5f));
            verts.Add(new Vector3(-hw, hh, -hd));  norms.Add(Vector3.back); uvs.Add(new Vector2(0.5f, 0.5f));
            verts.Add(new Vector3(hw, hh, -hd));   norms.Add(Vector3.back); uvs.Add(new Vector2(0.5f, 0.5f));
            verts.Add(new Vector3(hw, -hh, -hd));  norms.Add(Vector3.back); uvs.Add(new Vector2(0.5f, 0.5f));
            tris.Add(b1); tris.Add(b1 + 2); tris.Add(b1 + 1);
            tris.Add(b1); tris.Add(b1 + 3); tris.Add(b1 + 2);

            // Top, Bottom, Left, Right faces
            AddQuad(verts, norms, uvs, tris, new Vector3(-hw, hh, -hd), new Vector3(-hw, hh, hd), new Vector3(hw, hh, hd), new Vector3(hw, hh, -hd), Vector3.up);
            AddQuad(verts, norms, uvs, tris, new Vector3(-hw, -hh, -hd), new Vector3(hw, -hh, -hd), new Vector3(hw, -hh, hd), new Vector3(-hw, -hh, hd), Vector3.down);
            AddQuad(verts, norms, uvs, tris, new Vector3(-hw, -hh, -hd), new Vector3(-hw, -hh, hd), new Vector3(-hw, hh, hd), new Vector3(-hw, hh, -hd), Vector3.left);
            AddQuad(verts, norms, uvs, tris, new Vector3(hw, -hh, -hd), new Vector3(hw, hh, -hd), new Vector3(hw, hh, hd), new Vector3(hw, -hh, hd), Vector3.right);

            // Wooden post beneath the sign
            float pw = 0.035f;
            float ph = 0.12f;
            float postTop = -hh;
            float postBottom = postTop - ph;
            AddQuad(verts, norms, uvs, tris, new Vector3(-pw, postBottom, -hd), new Vector3(pw, postBottom, -hd), new Vector3(pw, postBottom, hd), new Vector3(-pw, postBottom, hd), Vector3.down);
            AddQuad(verts, norms, uvs, tris, new Vector3(-pw, postBottom, hd), new Vector3(pw, postBottom, hd), new Vector3(pw, postTop, hd), new Vector3(-pw, postTop, hd), Vector3.forward);
            AddQuad(verts, norms, uvs, tris, new Vector3(-pw, postBottom, -hd), new Vector3(-pw, postTop, -hd), new Vector3(pw, postTop, -hd), new Vector3(pw, postBottom, -hd), Vector3.back);
            AddQuad(verts, norms, uvs, tris, new Vector3(-pw, postBottom, -hd), new Vector3(-pw, postBottom, hd), new Vector3(-pw, postTop, hd), new Vector3(-pw, postTop, -hd), Vector3.left);
            AddQuad(verts, norms, uvs, tris, new Vector3(pw, postBottom, -hd), new Vector3(pw, postTop, -hd), new Vector3(pw, postTop, hd), new Vector3(pw, postBottom, hd), Vector3.right);

            mesh.vertices = verts.ToArray();
            mesh.normals = norms.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds();

            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void AddQuad(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 norm)
        {
            int b = verts.Count;
            verts.Add(v0); norms.Add(norm); uvs.Add(new Vector2(0.5f, 0.5f));
            verts.Add(v1); norms.Add(norm); uvs.Add(new Vector2(0.5f, 0.5f));
            verts.Add(v2); norms.Add(norm); uvs.Add(new Vector2(0.5f, 0.5f));
            verts.Add(v3); norms.Add(norm); uvs.Add(new Vector2(0.5f, 0.5f));

            tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
        }

        private static GameObject CreateBuoy(string name, Vector3 localPos, Transform parent, Mesh mesh, Material mat, float phaseOffset)
        {
            GameObject buoy = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(buoy, "Create Buoy");
            buoy.transform.SetParent(parent, false);
            buoy.transform.localPosition = localPos;
            buoy.transform.localRotation = Quaternion.identity;
            buoy.transform.localScale = Vector3.one;

            MeshFilter mf = buoy.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = buoy.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            var bob = buoy.AddComponent<MarinaBuoyBobbing>();
            // Farklı şamandıraların doğal ve dalgalı salınması için faz ofseti
            var field = typeof(MarinaBuoyBobbing).GetField("m_RandomOffset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(bob, phaseOffset);

            return buoy;
        }

        private static GameObject CreateChain(string name, Vector3 start, Vector3 end, Transform parent, Mesh mesh, Material mat)
        {
            GameObject chain = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(chain, "Create Chain");
            chain.transform.SetParent(parent, false);

            chain.transform.localPosition = start;
            Vector3 dir = end - start;
            if (dir.sqrMagnitude > 0.0001f)
            {
                chain.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
            }

            MeshFilter mf = chain.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = chain.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            return chain;
        }

        private static GameObject CreateSignPlate(string name, Vector3 localPos, Transform parent, Mesh mesh, Material mat)
        {
            GameObject sign = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(sign, "Create Sign Plate");
            sign.transform.SetParent(parent, false);
            sign.transform.localPosition = localPos;
            // Kameranın bakış açısına doğru hafif tatlı eğim (~ -35 derece)
            sign.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            sign.transform.localScale = Vector3.one;

            MeshFilter mf = sign.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = sign.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            return sign;
        }
    }
}
