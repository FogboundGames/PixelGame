# -*- coding: utf-8 -*-
import re

with open(r'Assets\Scripts\ShipDispatcher.cs', 'r', encoding='utf-8') as f:
    text = f.read()

# Replace RunReferenceRopeArm and HopCargoToShip
start_target = 'private IEnumerator RunReferenceRopeArm('
end_target = 'private Vector3 ExitWorldPoint('

start_idx = text.find(start_target)
end_idx = text.find(end_target)

if start_idx == -1 or end_idx == -1:
    print("Could not find section in ShipDispatcher.cs")
    exit(1)

new_section = '''private IEnumerator RunReferenceRopeArm(
            List<PixelCube> armCubes,
            ShoreLanePath path,
            ShipController ship,
            bool entersLeft,
            Vector3 shoreTargetAtLaunch)
        {
            if (armCubes == null || armCubes.Count == 0 || path == null) yield break;

            int count = armCubes.Count;
            float pitch = Mathf.Max(0.12f, m_GridFrame.Pitch);
            float baseSpeed = Mathf.Max(3.2f, MovementSettings.MoveSpeed);
            float spacing = pitch * RopeSpacingFactor;
            float maxStep = baseSpeed * RopeCatchUpMaxMultiplier;
            float liftHeight = pitch * 0.6f;
            float arriveRadius = pitch * 0.35f;

            var cargoObjects = new GameObject[count];
            var runners = new ICargoRunner[count];
            var homePositions = new Vector3[count];
            var controllers = new CubeMovementController[count];
            var startDelays = new float[count];

            for (int k = 0; k < count; k++)
            {
                PixelCube cube = armCubes[k];
                s_ReservedCubes.Add(cube);
                homePositions[k] = cube != null ? cube.transform.position : Vector3.zero;

                if (cube == null) continue;

                ICargoRunner selfRunner = cube.GetComponent<ICargoRunner>();
                if (m_CargoStandInPrefab != null)
                {
                    cargoObjects[k] = CreateStandInCargo(cube, k);
                    cube.SetPoppedVisualState(true, regenerateContourShadow: false);
                }
                else if (selfRunner != null)
                {
                    cube.BeginLeaving(regenerateContourShadow: false);
                    selfRunner.BeginWalk(k);
                    cargoObjects[k] = cube.gameObject;
                }
                else
                {
                    cargoObjects[k] = CreateRopeCargo(cube);
                    cube.SetPoppedVisualState(true, regenerateContourShadow: false);
                }

                runners[k] = cargoObjects[k].GetComponent<ICargoRunner>();

                // Karakter Hareket Katmanı (CubeMovementController)
                controllers[k] = cargoObjects[k].GetComponent<CubeMovementController>();
                if (controllers[k] == null) controllers[k] = cargoObjects[k].AddComponent<CubeMovementController>();
                controllers[k].Initialize(MovementSettings, k, cargoObjects[k].transform.localScale);

                // Multi-cube zamanlama varyasyonu: Arkadaki küpler küçük, kontrollü aralıklarla (stagger) hareket eder
                startDelays[k] = k * MovementSettings.StartDelayVariation + UnityEngine.Random.Range(-0.005f, 0.005f);
            }

            var basePos = (Vector3[])homePositions.Clone();
            var prevPos = (Vector3[])homePositions.Clone();
            var lift = new float[count];
            var arrived = new bool[count];
            int onPath = count;
            float headDist = 0f;
            float headSpeed = 0f;
            float elapsed = 0f;

            Camera cam = ShipController.MainCamera;
            Vector3 popUp = cam != null ? -cam.transform.forward : Vector3.back;
            Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;

            while (onPath > 0)
            {
                float dt = Time.deltaTime;
                elapsed += dt;

                // Başlangıçta yumuşak hızlanma (Acceleration)
                float targetSpeed = baseSpeed * Mathf.SmoothStep(0.20f, 1f, Mathf.Clamp01(elapsed / RopeStartRampDuration));
                headSpeed = Mathf.MoveTowards(headSpeed, targetSpeed, baseSpeed * MovementSettings.Acceleration * dt);

                Vector3 shoreNow = ship != null ? GetShorePoint(ship) : shoreTargetAtLaunch;
                Vector3 shoreShift = shoreNow - shoreTargetAtLaunch;
                Vector3 entranceNow = shoreTargetAtLaunch + shoreShift;

                int leader = -1;
                for (int k = 0; k < count; k++)
                {
                    if (arrived[k]) continue;

                    GameObject cargo = cargoObjects[k];
                    if (cargo == null)
                    {
                        arrived[k] = true;
                        onPath--;
                        continue;
                    }

                    // Henüz başlama gecikmesi dolmadıysa anticipation esnemesi yap
                    if (elapsed < startDelays[k])
                    {
                        float waitProgress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, startDelays[k]));
                        float squash = Mathf.Sin(waitProgress * Mathf.PI) * (MovementSettings.SquashAmount * 0.4f);
                        cargo.transform.localScale = new Vector3(
                            cargo.transform.localScale.x * (1f + squash * 0.3f),
                            cargo.transform.localScale.y * (1f - squash * 0.5f),
                            cargo.transform.localScale.z * (1f + squash * 0.3f)
                        );
                        continue;
                    }

                    Vector3 prev = prevPos[k];
                    bool reachedShip;

                    if (leader < 0 && k == 0)
                    {
                        // Asıl baş küp: Eğrisel şerit yolunu takip eder (Curved path)
                        headDist += headSpeed * dt;
                        float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(headDist / path.Length));
                        basePos[k] = path.PointAtDistance(headDist) + shoreShift * w;
                        reachedShip = headDist >= path.Length;
                    }
                    else if (leader < 0)
                    {
                        // Öndeki küp bindi: Yeni lider girişe yönelir
                        basePos[k] = Vector3.MoveTowards(basePos[k], entranceNow, maxStep * dt);
                        reachedShip = (basePos[k] - entranceNow).sqrMagnitude <= arriveRadius * arriveRadius;
                    }
                    else
                    {
                        // İp kısıtı: Lidere doğru organik yaylanmayla takip
                        Vector3 toLeader = basePos[leader] - basePos[k];
                        float d = toLeader.magnitude;
                        if (d > spacing)
                        {
                            float move = Mathf.Min(d - spacing, maxStep * dt);
                            basePos[k] += toLeader / d * move;
                        }
                        reachedShip = false;
                    }

                    // Karttan kalkış (pop-out) ve sahil zeminine yumuşak iniş
                    if ((basePos[k] - homePositions[k]).sqrMagnitude > 1e-6f)
                        lift[k] = Mathf.MoveTowards(lift[k], 1f, dt / RopePopOutDuration);
                    float landFade = Mathf.Clamp01((basePos[k] - entranceNow).magnitude / (pitch * 3f));

                    // Karakter Yürüyüş Sekmesi (Secondary Walk Bobbing)
                    float walkDist = (basePos[k] - homePositions[k]).magnitude;
                    float bobSin = Mathf.Sin(walkDist * MovementSettings.BobSpeed + (k % 2) * Mathf.PI);
                    float bobOffset = bobSin * MovementSettings.BobAmount * Mathf.Clamp01(headSpeed / baseSpeed);

                    cargo.transform.position = basePos[k] + popUp * (liftHeight * lift[k] * landFade) + camUp * bobOffset;

                    // Hareket yönü ve Viraj Yatma (Bank Tilt)
                    Vector3 moveDir = basePos[k] - prev;
                    if (moveDir.sqrMagnitude > 1e-7f)
                    {
                        if (runners[k] != null)
                        {
                            runners[k].TurnToward(moveDir, dt);
                        }
                        else
                        {
                            float yaw = CargoRunnerHeading.TargetYaw(moveDir, 35f);
                            cargo.transform.rotation = CargoRunnerHeading.Apply(Quaternion.identity, yaw);
                        }
                    }

                    prevPos[k] = basePos[k];

                    if (reachedShip)
                    {
                        arrived[k] = true;
                        onPath--;
                        cargo.transform.position = entranceNow;
                        StartCoroutine(HopCargoToShip(cargo, ship, entersLeft, armCubes[k], controllers[k]));
                        continue;
                    }

                    leader = k;
                }

                yield return null;
            }
        }

        private static MaterialPropertyBlock s_CargoColorBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// Küpün yerine yürüyen "dublör" prefab: küpün yeri, açısı, boyutu ve rengiyle doğar.
        /// Animator'ı varsa yan yana küpler ters fazda koşar.
        /// </summary>
        private GameObject CreateStandInCargo(PixelCube cube, int indexInRope)
        {
            GameObject cargo = Instantiate(m_CargoStandInPrefab, cube.transform.position, cube.transform.rotation);
            cargo.name = "RopeCargoCube";
            cargo.transform.localScale = cube.transform.lossyScale;

            if (s_CargoColorBlock == null) s_CargoColorBlock = new MaterialPropertyBlock();
            s_CargoColorBlock.Clear();
            s_CargoColorBlock.SetColor(BaseColorId, cube.CurrentColor);
            s_CargoColorBlock.SetColor(ColorId, cube.CurrentColor);
            s_CargoColorBlock.SetColor(EmissionColorId, Color.black);
            foreach (Renderer r in cargo.GetComponentsInChildren<Renderer>(true))
            {
                r.SetPropertyBlock(s_CargoColorBlock);
            }

            Animator animator = cargo.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.Play(0, 0, (indexInRope % 2) * 0.5f);
            }

            return cargo;
        }

        /// <summary>
        /// Bacaksız bir küp prefab'ı kullanılıyorsa küpün yerine yürüyen yedek görsel
        /// (küp yerinde gizlenir, bu kopya kayarak gider).
        /// </summary>
        private static GameObject CreateRopeCargo(PixelCube cube)
        {
            GameObject cargo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cargo.name = "RopeCargoCube";
            cargo.transform.position = cube.transform.position;
            cargo.transform.rotation = cube.transform.rotation;
            cargo.transform.localScale = cube.transform.lossyScale;

            Collider c = cargo.GetComponent<Collider>();
            if (c != null) Destroy(c);

            MeshRenderer mr = cargo.GetComponent<MeshRenderer>();
            MeshRenderer srcMr = cube.GetComponent<MeshRenderer>();
            if (mr != null && srcMr != null)
            {
                mr.sharedMaterial = srcMr.sharedMaterial;
                MaterialPropertyBlock b = new MaterialPropertyBlock();
                srcMr.GetPropertyBlock(b);
                mr.SetPropertyBlock(b);
            }

            return cargo;
        }

        /// <summary>
        /// Sahil kenarına gelen küp, sahil sonunda gemiye/arabaya doğru organik parabolik yayla zıplar
        /// ve güverteye yaylanarak (settle bounce) oturur.
        /// </summary>
        private IEnumerator HopCargoToShip(GameObject cargo, ShipController ship, bool fromLeft, PixelCube sourceCube, CubeMovementController controller = null)
        {
            if (cargo == null) yield break;

            Vector3 start = cargo.transform.position;
            ICargoRunner runner = cargo.GetComponent<ICargoRunner>();
            WaddleRunner waddle = cargo.GetComponent<WaddleRunner>();
            WalkingCargoVisual visual = cargo.GetComponent<WalkingCargoVisual>();

            if (waddle != null) waddle.IsAirborne = true;

            // Araç güverte iniş hedefi:
            Vector3 deckOffset = new Vector3(fromLeft ? -0.06f : 0.06f, 0.16f, 0.02f);
            float duration = Mathf.Max(0.20f, m_HopDuration);
            float elapsed = 0f;

            Camera mainCam = ShipController.MainCamera;
            Vector3 arcUp = mainCam != null ? mainCam.transform.up : Vector3.up;

            Quaternion startRot = cargo.transform.rotation;
            Vector3 baseScale = cargo.transform.localScale;

            // 1. Zıplama Öncesi Çok Kısa Anticipation (0.04s)
            float anticipationDur = 0.04f;
            float antElapsed = 0f;
            while (antElapsed < anticipationDur && cargo != null)
            {
                antElapsed += Time.deltaTime;
                float tAnt = Mathf.Clamp01(antElapsed / anticipationDur);
                float squ = Mathf.Sin(tAnt * Mathf.PI) * (MovementSettings.SquashAmount * 0.7f);
                cargo.transform.localScale = new Vector3(baseScale.x * (1f + squ * 0.5f), baseScale.y * (1f - squ), baseScale.z * (1f + squ * 0.5f));
                yield return null;
            }

            // 2. Parabolik Uçuş
            while (elapsed < duration && cargo != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Hedef pozisyon (araç hareket ediyorsa dinamik takip):
                Vector3 target = ship != null ? ship.transform.position + deckOffset : start;

                // Yatay/Dikey İlerleme (Pürüzsüz Parabolik Rota):
                float hT = Mathf.SmoothStep(0f, 1f, t);
                Vector3 p = Vector3.Lerp(start, target, hT);

                // Gerçek fiziksel parabolik yay:
                float arc = 4f * t * (1f - t);
                p += arcUp * (arc * m_HopArcHeight);

                cargo.transform.position = p;

                // Havada Dönüş & Yönelme:
                Vector3 toTarget = target - start;
                if (runner != null)
                {
                    runner.TurnToward(toTarget, Time.deltaTime);
                }

                // Havada öne doğru tatlı bir zıplama eğimi (tilt / pitch):
                float pitchAngle = Mathf.Sin(t * Mathf.PI) * 16f;
                cargo.transform.rotation = startRot * Quaternion.Euler(pitchAngle, 0f, 0f);

                if (visual != null)
                {
                    visual.SetAirborne(Time.deltaTime);
                }

                // Squash & Stretch:
                if (t < 0.28f)
                {
                    // Kalkış: Y'de uzama (stretch)
                    float stretch = Mathf.Sin(t / 0.28f * Mathf.PI * 0.5f) * MovementSettings.SquashAmount;
                    cargo.transform.localScale = new Vector3(baseScale.x * (1f - stretch * 0.5f), baseScale.y * (1f + stretch), baseScale.z * (1f - stretch * 0.5f));
                }
                else if (t > 0.75f)
                {
                    // İniş yaklaşımı: Hafif basılma (squash)
                    float landingT = (t - 0.75f) / 0.25f;
                    float squash = Mathf.Sin(landingT * Mathf.PI) * (MovementSettings.SquashAmount * 1.1f);
                    cargo.transform.localScale = new Vector3(baseScale.x * (1f + squash * 0.5f), baseScale.y * (1f - squash), baseScale.z * (1f + squash * 0.5f));
                }
                else
                {
                    cargo.transform.localScale = baseScale;
                }

                yield return null;
            }

            // 3. Güverteye İnişte Küçük Settle Yaylanması (0.08s)
            float settleDur = MovementSettings.SettleDuration;
            float settleElapsed = 0f;
            Vector3 finalDeckPos = ship != null ? ship.transform.position + deckOffset : start;
            while (settleElapsed < settleDur && cargo != null)
            {
                settleElapsed += Time.deltaTime;
                float sT = Mathf.Clamp01(settleElapsed / settleDur);
                float settleBounce = Mathf.Sin(sT * Mathf.PI) * MovementSettings.SettleBounce * (1f - sT);
                Vector3 currentDeck = ship != null ? ship.transform.position + deckOffset : finalDeckPos;
                cargo.transform.position = currentDeck + arcUp * settleBounce;
                cargo.transform.localScale = Vector3.Lerp(cargo.transform.localScale, baseScale, sT);
                yield return null;
            }

            if (cargo != null)
            {
                if (sourceCube != null && cargo == sourceCube.gameObject)
                {
                    if (waddle != null) waddle.IsAirborne = false;
                    sourceCube.FinishLeaving();
                }
                else
                {
                    Destroy(cargo);
                }
            }
            if (sourceCube != null) s_ReservedCubes.Remove(sourceCube);

            if (ship != null)
            {
                // Gemiye kargo ekle ve görsel/ses/su geri bildirimini tetikle
                ship.AddCargo(1);
                ship.TriggerWaterDipImpact(0.12f, 0.35f);
                ShipController.SpawnWaterRipple(ship.transform.position + new Vector3(0f, -0.05f, 0.05f), 0.28f, 0.95f, 0.45f);
                HypercasualWaterController.TriggerWaterRipple(ship.transform.position, 0.70f, 0.25f);
            }

            m_ActiveCargoFlightCount = Mathf.Max(0, m_ActiveCargoFlightCount - 1);

            CheckWinCondition();
            TriggerWaitingShipsCheck();
        }

        '''

text = text[:start_idx] + new_section + text[end_idx:]

with open(r'Assets\Scripts\ShipDispatcher.cs', 'w', encoding='utf-8') as f:
    f.write(text)

print("ShipDispatcher.cs successfully updated with character movement layer!")
