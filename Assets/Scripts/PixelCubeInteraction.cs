using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PixelGame
{
    /// <summary>
    /// Mouse ile tıklanan veya basılı tutulup üzerinden geçilen piksel küplerini
    /// 3D voksel partiküllerine ayırarak patlatır ve yok eder.
    /// 'R' tuşu veya ekrandaki buton ile tüm resmi eski haline getirme (Reset) desteği sunar.
    /// Hem Play Mode hem de Scene View (Edit Mode) üzerinde tam etkileşim sağlar.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class PixelCubeInteraction : MonoBehaviour
    {
        private static PixelCubeInteraction s_Instance;
        public static PixelCubeInteraction Instance => s_Instance;

        [Header("Kamera & Hedefler")]
        [SerializeField] private Camera m_WorldCamera;

        [Header("Etkileşim Modları")]
        [Tooltip("Mouse'u basılı tutarak sürüklediğinde üzerinden geçilen tüm küpleri sırayla patlat")]
        [SerializeField] private bool m_AllowDragPopping = true;

        [Tooltip("Sadece UI butonlarına tıklandığında engeller, çerçeve veya arka plan tıklamayı engellemez")]
        [SerializeField] private bool m_BlockOverUIButtonsOnly = true;

        [Header("Arayüz (HUD)")]
        [SerializeField] private bool m_ShowResetButtonOnScreen = true;

        // Patlatılan küplerin listesi (Geri yükleme için)
        private readonly List<PixelCube> m_PoppedCubes = new List<PixelCube>();
        private PixelCube m_LastPoppedCube;

        private void Awake()
        {
            s_Instance = this;
            EnsureCameraAndRaycaster();
        }

        private void OnEnable()
        {
            s_Instance = this;
            EnsureCameraAndRaycaster();

            #if UNITY_EDITOR
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            #endif
        }

        private void OnDisable()
        {
            #if UNITY_EDITOR
            SceneView.duringSceneGui -= OnSceneGUI;
            #endif
        }

        private void EnsureCameraAndRaycaster()
        {
            if (m_WorldCamera == null)
            {
                m_WorldCamera = Camera.main;
                if (m_WorldCamera == null)
                {
                    m_WorldCamera = Object.FindFirstObjectByType<Camera>();
                }
            }

            if (m_WorldCamera != null)
            {
                // Unity UGUI EventSystem'in 3D collider'lara otomatik tıklayabilmesi için PhysicsRaycaster ekle
                if (m_WorldCamera.GetComponent<PhysicsRaycaster>() == null)
                {
                    m_WorldCamera.gameObject.AddComponent<PhysicsRaycaster>();
                }
            }
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                HandleInput();
                HandleKeyboardShortcuts();
            }
        }

        private void HandleInput()
        {
            Vector2 screenPos;
            bool isPressed;
            bool isDown;

            if (!GetPointerState(out screenPos, out isPressed, out isDown))
                return;

            // Sadece gerçek UI butonlarının üzerindeyse engelle (Arka plan veya çerçeve engellemez!)
            if (m_BlockOverUIButtonsOnly && IsOverInteractiveUI(screenPos))
                return;

            // Tek tık veya sürükleme modu
            if (isDown || (m_AllowDragPopping && isPressed))
            {
                TryPopCubeAt(screenPos);
            }
            else if (!isPressed)
            {
                m_LastPoppedCube = null;
            }
        }

        private bool IsOverInteractiveUI(Vector2 screenPosition)
        {
            if (EventSystem.current == null) return false;

            PointerEventData pe = new PointerEventData(EventSystem.current);
            pe.position = screenPosition;
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pe, results);

            foreach (var r in results)
            {
                if (r.gameObject != null)
                {
                    // Eğer tıklanan nesne bir Button veya Selectable ise engelle
                    if (r.gameObject.GetComponent<UnityEngine.UI.Button>() != null ||
                        r.gameObject.GetComponent<UnityEngine.UI.Selectable>() != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool GetPointerState(out Vector2 screenPos, out bool isPressed, out bool isDown)
        {
            screenPos = Vector2.zero;
            isPressed = false;
            isDown = false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                screenPos = Mouse.current.position.ReadValue();
                isPressed = Mouse.current.leftButton.isPressed;
                isDown = Mouse.current.leftButton.wasPressedThisFrame;
                if (isPressed || isDown) return true;
            }

            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            {
                var touch = Touchscreen.current.touches[0];
                screenPos = touch.position.ReadValue();
                isPressed = touch.press.isPressed;
                isDown = touch.press.wasPressedThisFrame;
                if (isPressed || isDown) return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                screenPos = Input.mousePosition;
                isPressed = Input.GetMouseButton(0);
                isDown = Input.GetMouseButtonDown(0);
                if (isPressed || isDown) return true;
            }
            catch {}
#endif

            return isPressed || isDown;
        }

        private void TryPopCubeAt(Vector2 screenPosition)
        {
            EnsureCameraAndRaycaster();
            if (m_WorldCamera == null) return;

            Ray ray = m_WorldCamera.ScreenPointToRay(screenPosition);
            RaycastHit hit;

            // Küpleri tespit etmek için physics raycast
            if (Physics.Raycast(ray, out hit, 100f))
            {
                PixelCube cube = hit.collider.GetComponent<PixelCube>();
                if (cube == null)
                {
                    cube = hit.collider.GetComponentInParent<PixelCube>();
                }

                if (cube != null && cube != m_LastPoppedCube && !cube.IsPopped && cube.gameObject.activeSelf)
                {
                    cube.BurstAndDestroy();
                }
            }
        }

        /// <summary>
        /// Bir küp patlatıldığında bu metot çağrılır; küpün görselini gizler ve geri alma listesine ekler.
        /// Küpün sahte gölgesi panoda hep sabit kalır!
        /// </summary>
        public void RegisterPoppedCube(PixelCube cube)
        {
            if (cube == null || cube.IsPopped) return;

            m_LastPoppedCube = cube;
            cube.SetPoppedVisualState(true);
            m_PoppedCubes.Add(cube);
        }

        #if UNITY_EDITOR
        private void OnSceneGUI(SceneView sceneView)
        {
            // Edit Mode'da Scene View içerisinden küplere tıklayarak patlatma desteği!
            Event e = Event.current;
            if (e != null && (e.type == EventType.MouseDown && e.button == 0))
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    PixelCube cube = hit.collider.GetComponent<PixelCube>() ?? hit.collider.GetComponentInParent<PixelCube>();
                    if (cube != null && !cube.IsPopped && cube.gameObject.activeSelf)
                    {
                        cube.BurstAndDestroy();
                        e.Use();
                    }
                }
            }
        }
        #endif

        private void HandleKeyboardShortcuts()
        {
            bool resetPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                resetPressed = Keyboard.current.rKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (!resetPressed)
                {
                    resetPressed = Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Space);
                }
            }
            catch {}
#endif

            if (resetPressed)
            {
                ResetAllCubes();
            }
        }

        /// <summary>
        /// Patlatılan tüm küpleri tekrar görünür hale getirir (Resmi yeniler).
        /// </summary>
        [ContextMenu("Tüm Küpleri Geri Yükle (Reset)")]
        public void ResetAllCubes()
        {
            if (m_PoppedCubes.Count == 0) return;

            foreach (var cube in m_PoppedCubes)
            {
                if (cube != null)
                {
                    cube.gameObject.SetActive(true);
                    cube.SetPoppedVisualState(false);
                }
            }

            m_PoppedCubes.Clear();
            m_LastPoppedCube = null;

            Debug.Log("<color=cyan>[PixelGame]</color> Tüm küpler başarıyla geri yüklendi!");
        }

        public int PoppedCount => m_PoppedCubes.Count;

        private void OnGUI()
        {
            if (!m_ShowResetButtonOnScreen || m_PoppedCubes.Count == 0) return;

            float btnW = 200f;
            float btnH = 48f;
            float margin = 20f;
            Rect rect = new Rect(Screen.width - btnW - margin, Screen.height - btnH - margin, btnW, btnH);

            GUIStyle style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.15f, 0.65f, 1f, 0.95f);

            if (GUI.Button(rect, $"🔄 Resmi Yenile ({m_PoppedCubes.Count} Küp)", style))
            {
                ResetAllCubes();
            }

            GUI.backgroundColor = oldBg;
        }
    }
}
