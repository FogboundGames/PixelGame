# -*- coding: utf-8 -*-
with open(r'Assets\Scripts\ShipDispatcher.cs', 'r', encoding='utf-8') as f:
    code = f.read()

target1 = '[SerializeField] private GameObject m_CargoStandInPrefab;'
replacement1 = '''[SerializeField] private GameObject m_CargoStandInPrefab;

        [Header("✨ Küp Karakter Hareketi (Hypercasual Polished Movement)")]
        [Tooltip("Küp karakterlerin gemi ve arabalara giderkenki akıcı, organik hareket ayarları.")]
        [SerializeField] private CubeMovementSettings m_CubeMovementSettings;
        public CubeMovementSettings MovementSettings
        {
            get => m_CubeMovementSettings != null ? m_CubeMovementSettings : CubeMovementSettings.Default;
            set => m_CubeMovementSettings = value;
        }'''

if target1 in code:
    code = code.replace(target1, replacement1, 1)
    print("Field added successfully")
else:
    print("target1 not found")

with open(r'Assets\Scripts\ShipDispatcher.cs', 'w', encoding='utf-8') as f:
    f.write(code)
