# -*- coding: utf-8 -*-
with open(r'Assets\Scripts\ShipController.cs', 'r', encoding='utf-8') as f:
    text = f.read()

start_target = 'private IEnumerator SailToSlotRoutine(ShipSlot targetSlot, float customDuration = -1f, Action onComplete = null)'
end_target = '[Header("🧭 Slota Gidişte Yönelme")]'

start_idx = text.find(start_target)
end_idx = text.find(end_target)

if start_idx == -1 or end_idx == -1:
    print("Could not find SailToSlotRoutine in ShipController.cs")
    exit(1)

new_routine = '''private IEnumerator SailToSlotRoutine(ShipSlot targetSlot, float customDuration = -1f, Action onComplete = null)
        {
            m_IsMoving = true;
            m_IsDragging = false;
            m_IsPickedUp = false;
            m_CandidateSlot = null;
            m_EnableWaterBobbing = false;
            ResetVisualOffset();
            transform.DOKill(true);

            // Eski slottan ayrıl
            if (m_CurrentSlot != null && m_CurrentSlot != targetSlot)
            {
                m_CurrentSlot.ReleaseShip();
            }

            // Slota bağla
            targetSlot.DockShip(this);
            m_CurrentSlot = targetSlot;

            Vector3 startWorldPos = transform.position;
            Vector3 startWorldScale = transform.lossyScale;
            Quaternion startRot = transform.rotation;

            Vector3 targetLocalPos = new Vector3(0f, m_DockHeightOffset, m_DockForwardOffset);
            Quaternion targetSlotWorldRot = targetSlot.transform.rotation;
            Vector3 targetWorld = targetSlot.transform.TransformPoint(targetLocalPos);
            Vector3 slotUp = targetSlotWorldRot * Vector3.up;

            // 1. PICKUP / START: VisualRoot üzerinde hafif kalkış ve minik scale (1.04x)
            if (m_VisualRoot != null)
            {
                m_VisualRoot.localPosition = new Vector3(0f, 0.04f, 0f);
                m_VisualRoot.localScale = Vector3.one * 1.04f;
            }

            // 2 & 3. TEK, PÜRÜZSÜZ VE DOĞAL BEZIER ROTASI
            // P0: Geminin kalkış noktası
            // P3: Slotun tam varış noktası
            // P1 & P2: Keyfi dünya eksenleri yerine doğrudan start->target yönü ve slot giriş ekseninden türetilir.
            Vector3 p0 = startWorldPos;
            Vector3 p3 = targetWorld;
            Vector3 delta = p3 - p0;
            float dist = delta.magnitude;

            // Kalkış doğrultusu (geminin mevcut ileri yönü ile hedefe doğru yönün pürüzsüz karışımı)
            Vector3 vStart = Vector3.Lerp(transform.forward, delta.normalized, 0.45f).normalized;
            Vector3 p1 = p0 + vStart * (dist * 0.38f);

            // Varış doğrultusu: Slotun kendi ileri ekseni boyunca yaklaşır.
            // Bu sayede teğet (P3 - P2) doğrudan slotun içine bakar; ASLA yana kayma (sideways drift) yapmaz!
            Vector3 vSlot = (targetSlotWorldRot * Vector3.forward).normalized;
            Vector3 p2 = p3 - vSlot * (dist * 0.32f);

            // Snap durumunda m_SnapDuration (0.16s), normal click durumunda 0.46s
            float duration = (customDuration > 0f) ? customDuration : 0.46f;
            float elapsed = 0f;
            float lastSmokeTime = 0f;
            float lateralDelta = targetWorld.x - startWorldPos.x;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 5. HIZ PROFİLİ: %0 yavaş -> %20 hızlan -> %70 seyir -> %90 yavaşla -> %100 oturma
                float easeT = Mathf.SmoothStep(0f, 1f, t);

                // Pozisyon:
                Vector3 currentWorldPos = EvaluateCubicBezier(p0, p1, p2, p3, easeT);
                transform.position = currentWorldPos;

                // 4. ROTASYON: Rotasyon hareketin anlık teğetine bakar (Forward = Path Tangent)
                Vector3 tangent = 3f * (1f - easeT) * (1f - easeT) * (p1 - p0) +
                                  6f * (1f - easeT) * easeT * (p2 - p1) +
                                  3f * easeT * easeT * (p3 - p2);

                if (tangent.sqrMagnitude > 1e-5f)
                {
                    Quaternion pathRot = Quaternion.LookRotation(tangent.normalized, slotUp);
                    // Rota sonuna yaklaştıkça (%70+) slotun kesin açısıyla tam hizalanır
                    float alignWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((easeT - 0.70f) / 0.30f));
                    transform.rotation = Quaternion.Slerp(pathRot, targetSlotWorldRot, alignWeight);
                }

                // Viraja yatma (Bank Roll) m_VisualRoot üzerinde uygulanır
                float bankRoll = Mathf.Sin(easeT * Mathf.PI) * (-Mathf.Sign(lateralDelta) * Mathf.Clamp(Mathf.Abs(lateralDelta) * 4.5f, 1.5f, 6.0f));
                float finalRollWeight = 1f - Mathf.Clamp01((easeT - 0.65f) / 0.35f);
                if (m_VisualRoot != null)
                {
                    m_VisualRoot.localRotation = Quaternion.Euler(0f, 0f, bankRoll * finalRollWeight);
                    // Ölçek varışa doğru normale döner
                    m_VisualRoot.localScale = Vector3.Lerp(Vector3.one * 1.04f, Vector3.one, easeT);
                    m_VisualRoot.localPosition = new Vector3(0f, Mathf.Lerp(0.04f, 0f, easeT), 0f);
                }

                // Duman ve su dalgası efekti
                if (Time.time - lastSmokeTime > 0.045f)
                {
                    lastSmokeTime = Time.time;
                    Vector3 exhaustPos = currentWorldPos - transform.forward * 0.32f + new Vector3(0f, -0.05f, 0.02f);
                    SpawnSmokePuff(exhaustPos, 0.10f, 0.28f, 0.32f);
                    SpawnWaterRipple(exhaustPos, 0.15f, 0.52f, 0.35f);
                }

                yield return null;
            }

            // 6. FINAL SNAP & DOCKING
            transform.SetParent(targetSlot.transform, true);
            transform.localPosition = targetLocalPos;
            transform.localRotation = Quaternion.identity;
            m_BaseLocalRotation = Quaternion.identity;
            transform.localScale = GetLocalScaleForBaseWorldScale();

            m_BaseLocalPosition = targetLocalPos;
            ResetVisualOffset();

            // 6.b) SETTLING MOVEMENT: 0.10 saniyelik çok tatlı, doğal sönümlü yaylanma
            float settleDur = 0.10f;
            float settleElapsed = 0f;
            while (settleElapsed < settleDur)
            {
                settleElapsed += Time.deltaTime;
                float sT = Mathf.Clamp01(settleElapsed / settleDur);
                float settleDip = Mathf.Sin(sT * Mathf.PI) * 0.035f * (1f - sT);
                if (m_VisualRoot != null)
                {
                    m_VisualRoot.localPosition = new Vector3(0f, -settleDip, 0f);
                    m_VisualRoot.localRotation = Quaternion.identity;
                    m_VisualRoot.localScale = Vector3.one;
                }
                yield return null;
            }

            if (m_VisualRoot != null)
            {
                m_VisualRoot.localPosition = Vector3.zero;
                m_VisualRoot.localRotation = Quaternion.identity;
                m_VisualRoot.localScale = Vector3.one;
            }

            // Su etkisi ve dalga
            if (targetSlot != null)
            {
                targetSlot.TriggerWaterDipImpact(0.18f, 0.52f);
            }
            TriggerWaterDipImpact(0.18f, 0.52f);
            SpawnWaterRipple(transform.position, 0.35f, 1.25f, 0.55f);

            m_IsMoving = false;
            m_IsDocked = true;
            m_EnableWaterBobbing = true;

            // ÖNEMLİ BUG DÜZELTMESİ: Gemi slota vardığında hemen CompactSlots ÇAĞRILMAZ.
            // CompactSlots yalnızca bir gemi ayrıldığında (boşluk açıldığında) çalışmalıdır;
            // aksi halde slota yeni giren gemiyi anında yana doğru kaydırıyordu.
            if (ShipDispatcher.Instance != null)
            {
                ShipDispatcher.Instance.OnShipDocked(this);
            }

            onComplete?.Invoke();
        }

        '''

text = text[:start_idx] + new_routine + text[end_idx:]

with open(r'Assets\Scripts\ShipController.cs', 'w', encoding='utf-8') as f:
    f.write(text)

print("ShipController.cs successfully updated with polished boat movement!")
