using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PixelGame
{
    /// <summary>
    /// Blender'da Cell Fracture ile üretilen 12 parçalı küpün (cube.fbx)
    /// parça mesh'lerini ve yerel ofsetlerini yöneten merkezi veri sınıfı.
    /// </summary>
    public class FracturedCubeData : ScriptableObject
    {
        public const string ResourcePath = "FracturedCubeData";
        public const string FbxAssetPath = "Assets/Prefabs/cube.fbx";

        [Serializable]
        public struct ShardData
        {
            public string name;
            public Mesh mesh;
            public Vector3 localOffset;
            public Vector3 outwardDir;
        }

        [SerializeField] private ShardData[] m_Shards = new ShardData[0];
        [SerializeField] private Mesh m_IntactMesh;

        public ShardData[] Shards => m_Shards;
        public int ShardCount => m_Shards != null ? m_Shards.Length : 0;
        public Mesh IntactMesh => m_IntactMesh;

        private static FracturedCubeData s_Instance;
        public static FracturedCubeData Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = Resources.Load<FracturedCubeData>(ResourcePath);
                    #if UNITY_EDITOR
                    if (s_Instance == null)
                    {
                        s_Instance = CreateOrUpdateAsset();
                    }
                    #endif
                }
                return s_Instance;
            }
        }

        public ShardData GetShard(int index)
        {
            if (m_Shards == null || m_Shards.Length == 0)
            {
                return GetFallbackShard(index);
            }
            int clamped = Mathf.Clamp(index, 0, m_Shards.Length - 1);
            return m_Shards[clamped];
        }

        // FBX'teki kesin koordinatlar baz alınarak hazırlanan emniyetli fallback
        private static readonly Vector3[] s_FallbackOffsets = new Vector3[]
        {
            new Vector3( 0.8343f,  0.7706f, -0.7900f), // Ore_Frag_00
            new Vector3(-0.5967f, -0.7045f,  0.1119f), // Ore_Frag_01
            new Vector3(-0.6918f,  0.7722f,  0.8948f), // Ore_Frag_02
            new Vector3( 0.7611f, -0.6587f,  0.7390f), // Ore_Frag_03
            new Vector3(-0.8583f,  0.6933f, -0.7550f), // Ore_Frag_04
            new Vector3(-0.0505f,  0.7432f, -0.7349f), // Ore_Frag_05
            new Vector3(-0.7042f, -0.7541f,  0.8720f), // Ore_Frag_06
            new Vector3( 0.7907f,  0.7584f,  0.6725f), // Ore_Frag_07
            new Vector3(-0.7737f, -0.7981f, -0.8243f), // Ore_Frag_08
            new Vector3( 0.7490f, -0.6927f, -0.7527f), // Ore_Frag_09
            new Vector3( 0.0977f, -0.0156f, -0.3394f), // Ore_Frag_10
            new Vector3(-0.7064f,  0.4110f,  0.5286f)  // Ore_Frag_11
        };

        private static ShardData GetFallbackShard(int index)
        {
            int idx = Mathf.Abs(index) % s_FallbackOffsets.Length;
            Vector3 offset = s_FallbackOffsets[idx];
            return new ShardData
            {
                name = $"Ore_Frag_{idx:D2}",
                mesh = null,
                localOffset = offset,
                outwardDir = offset.sqrMagnitude > 0.001f ? offset.normalized : Vector3.up
            };
        }

        #if UNITY_EDITOR
        public static FracturedCubeData CreateOrUpdateAsset()
        {
            string assetDir = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(assetDir))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            string fullAssetPath = $"{assetDir}/{ResourcePath}.asset";
            FracturedCubeData data = AssetDatabase.LoadAssetAtPath<FracturedCubeData>(fullAssetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<FracturedCubeData>();
                AssetDatabase.CreateAsset(data, fullAssetPath);
            }

            data.PopulateFromFBX();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        public void PopulateFromFBX()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(FbxAssetPath);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning($"[FracturedCubeData] {FbxAssetPath} bulunamadı!");
                return;
            }

            Mesh intact = null;
            Dictionary<string, Mesh> fragMeshes = new Dictionary<string, Mesh>();

            foreach (var asset in assets)
            {
                if (asset is Mesh m)
                {
                    if (m.name.IndexOf("intact", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        m.name.IndexOf("cube.001", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        m.name.Equals("Cube", StringComparison.OrdinalIgnoreCase))
                    {
                        intact = m;
                    }
                    else if (m.name.IndexOf("Frag", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        fragMeshes[m.name] = m;
                    }
                }
            }

            m_IntactMesh = intact;

            // Ore_Root prefabından transform ofsetlerini ve mesh eşleşmelerini al
            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxAssetPath);
            List<ShardData> shardList = new List<ShardData>();

            if (fbxPrefab != null)
            {
                Transform root = fbxPrefab.transform.Find("Ore_Root");
                if (root != null)
                {
                    for (int i = 0; i < root.childCount; i++)
                    {
                        Transform child = root.GetChild(i);
                        MeshFilter mf = child.GetComponent<MeshFilter>();
                        Mesh m = mf != null ? mf.sharedMesh : null;
                        if (m == null && fragMeshes.TryGetValue(child.name, out Mesh foundMesh))
                        {
                            m = foundMesh;
                        }

                        Vector3 localPos = child.localPosition;
                        Vector3 outDir = localPos.sqrMagnitude > 0.001f ? localPos.normalized : Vector3.up;

                        shardList.Add(new ShardData
                        {
                            name = child.name,
                            mesh = m,
                            localOffset = localPos,
                            outwardDir = outDir
                        });
                    }
                }
            }

            if (shardList.Count == 0)
            {
                // Fallback: Bulunan mesh'leri ve bilinen ofsetleri kullan
                for (int i = 0; i < s_FallbackOffsets.Length; i++)
                {
                    string fragName = $"Ore_Frag_{i:D2}";
                    fragMeshes.TryGetValue(fragName, out Mesh m);
                    Vector3 offset = s_FallbackOffsets[i];
                    shardList.Add(new ShardData
                    {
                        name = fragName,
                        mesh = m,
                        localOffset = offset,
                        outwardDir = offset.normalized
                    });
                }
            }

            m_Shards = shardList.ToArray();
            Debug.Log($"<color=#00FFAA><b>[FracturedCubeData]</b></color> {m_Shards.Length} adet kırık parça FBX'ten başarıyla yüklendi.");
        }
        #endif
    }
}
