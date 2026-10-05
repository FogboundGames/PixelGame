# -*- coding: utf-8 -*-
with open(r'Assets\Scripts\ShipDispatcher.cs', 'r', encoding='utf-8') as f:
    text = f.read()

start_target = 'private ShoreLanePath BuildHeadExitPath('
end_target = 'private static MaterialPropertyBlock s_CargoColorBlock;'

start_idx = text.find(start_target)
end_idx = text.find(end_target)

if start_idx == -1 or end_idx == -1:
    print("Could not find section in ShipDispatcher.cs")
    exit(1)

new_section = '''private ShoreLanePath BuildHeadExitPath(PixelCube head, HashSet<(int, int)> outsideAir, int minY,
                                                Vector3 exitRef, Vector3 boatEntrance)
        {
            Vector3 start = head.transform.position;
            float z = start.z;
            var wpts = new List<Vector3> { start };

            var headCell = (head.GridX, head.GridY);
            var route = CargoRopeBuilder.FindExitRoute(headCell, outsideAir, minY - 1);
            if (route.Count > 0)
            {
                foreach (var cell in CargoRopeBuilder.SimplifyRoute(headCell, route, outsideAir))
                {
                    Vector3 w = m_GridFrame.ToWorld(cell.Item1, cell.Item2);
                    w.z = z;
                    wpts.Add(w);
                }
            }

            Vector3 boardExit = wpts[wpts.Count - 1];
            Vector3 delta = boatEntrance - boardExit;

            // 1. DÜZ PATH → KONTROLLÜ KAVİSLİ PATH:
            // Düz çizgi yerine, küpün çıkış tarafına (sol veya sağ) göre dışa doğru tatlı bir yay (bow) çizilir.
            float isLeft = (boardExit.x < exitRef.x) ? -1f : 1f;
            if (Mathf.Abs(boardExit.x - exitRef.x) < 0.15f)
            {
                isLeft = (boatEntrance.x < boardExit.x) ? -1f : 1f;
            }

            float lateralBow = isLeft * Mathf.Clamp(Mathf.Abs(delta.x) * 0.40f + 0.35f, 0.30f, 0.90f);

            // 4 kontrol noktalı pürüzsüz Bezier kavis eğrisi
            Vector3 c0 = boardExit;
            Vector3 c1 = boardExit + new Vector3(lateralBow * 0.65f, delta.y * 0.32f, 0f);
            Vector3 c2 = boatEntrance + new Vector3(lateralBow * 0.20f, -delta.y * 0.28f, 0f);
            Vector3 c3 = boatEntrance;

            int curveSteps = 10;
            for (int i = 1; i <= curveSteps; i++)
            {
                float t = i / (float)curveSteps;
                float u = 1f - t;
                Vector3 pt = u * u * u * c0 + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * c3;
                pt.z = z;
                wpts.Add(pt);
            }

            return ShoreLanePath.BuildThrough(wpts, 24);
        }

        /// <summary>
        /// Referans videodaki hareket kalitesi:
        /// 1. Düz path → Kontrollü kavisli path
        /// 2. Instant rotation → Gradual rotation + Hafif tilt (Bank Tilt)
        /// 3. Sabit hız → Acceleration / Deceleration
        /// 4. Hareket eden statik kutu → Küçük secondary motion (Bob + Tilt + Zemin temas squash)
        /// 5. Hedefte snap → Approach + Settle (Yumuşak yaklaşma ve sönümlü oturma)
        /// </summary>
        private IEnumerator RunReferenceRopeArm(
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

            var cargoObjects = new GameObject[count];
            var runners = new ICargoRunner[count];
            var waddlers = new WaddleRunner[count];
            var homePositions = new Vector3[count];
            var baseScales = new Vector3[count];
            var distOnPath = new float[count];
            var currentYaw = new float[count];
            var currentTilt = new float[count];
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
                waddlers[k] = cargoObjects[k].GetComponent<WaddleRunner>();
                baseScales[k] = cargoObjects[k].transform.localScale;

                // Multi-cube timing varyasyonu: Arkadaki küpler küçük, kontrollü gecikmeyle yola çıkar
                startDelays[k] = k * MovementSettings.StartDelayVariation + UnityEngine.Random.Range(-0.005f, 0.005f);
                distOnPath[k] = 0f;
            }

            var basePos = (Vector3[])homePositions.Clone();
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

                // 3. SABİT HIZ → ACCELERATION / DECELERATION:
                // a) Başlangıçta hızlanma (Acceleration):
                float accelFactor = Mathf.Clamp01(elapsed / RopeStartRampDuration);
                accelFactor = Mathf.SmoothStep(0.20f, 1f, accelFactor);

                // b) Gemi girişine yaklaşırken yavaşlama (Deceleration / Arrival Ease-Out):
                float remainingDist = Mathf.Max(0f, path.Length - headDist);
                float arrivalRange = Mathf.Max(0.50f, MovementSettings.ArrivalDistance);
                float decelFactor = 1f;
                if (remainingDist < arrivalRange)
                {
                    float r = remainingDist / arrivalRange;
                    decelFactor = Mathf.Lerp(MovementSettings.ArrivalSlowdown, 1f, Mathf.SmoothStep(0f, 1f, r));
                }

                float targetSpeed = baseSpeed * accelFactor * decelFactor;
                headSpeed = Mathf.MoveTowards(headSpeed, targetSpeed, baseSpeed * MovementSettings.Acceleration * dt);
                headDist += headSpeed * dt;

                Vector3 shoreNow = ship != null ? GetShorePoint(ship) : shoreTargetAtLaunch;
                Vector3 shoreShift = shoreNow - shoreTargetAtLaunch;
                Vector3 entranceNow = shoreTargetAtLaunch + shoreShift;

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

                    // Henüz başlama gecikmesi dolmadıysa minik anticipation basılması
                    if (elapsed < startDelays[k])
                    {
                        float waitProgress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, startDelays[k]));
                        float squ = Mathf.Sin(waitProgress * Mathf.PI) * (MovementSettings.SquashAmount * 0.35f);
                        cargo.transform.localScale = new Vector3(
                            baseScales[k].x * (1f + squ * 0.3f),
                            baseScales[k].y * (1f - squ * 0.5f),
                            baseScales[k].z * (1f + squ * 0.3f)
                        );
                        continue;
                    }

                    // 1. KONTROLLÜ KAVİSLİ PATH BOYUNCA İLERLEME:
                    // Her küp, baş küpün arkasından spacing mesafesini koruyarak aynı kavisli yolu izler
                    float targetDist = Mathf.Max(0f, headDist - k * spacing);
                    distOnPath[k] = Mathf.MoveTowards(distOnPath[k], targetDist, maxStep * dt);

                    float d = distOnPath[k];
                    bool reachedShip = d >= path.Length;

                    if (d > 0f)
                    {
                        // Kavisli rota üzerinde pozisyon
                        float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / path.Length));
                        Vector3 pathPt = path.PointAtDistance(d) + shoreShift * w;
                        // Karttan kalkış harmanı
                        float pathBlend = Mathf.Clamp01(d / (pitch * 1.2f));
                        basePos[k] = Vector3.Lerp(homePositions[k], pathPt, pathBlend);
                    }
                    else
                    {
                        basePos[k] = homePositions[k];
                    }

                    // Karttan kalkış (pop-out) ve sahil zeminine iniş
                    if ((basePos[k] - homePositions[k]).sqrMagnitude > 1e-6f)
                        lift[k] = Mathf.MoveTowards(lift[k], 1f, dt / RopePopOutDuration);
                    float landFade = Mathf.Clamp01((basePos[k] - entranceNow).magnitude / (pitch * 3f));

                    // 4. SECONDARY MOTION (Hafif Walk Bobbing + Zemin Temas Basılması):
                    float stepPhase = d * 16f + (k % 2) * Mathf.PI;
                    float speedRatio = Mathf.Clamp01(headSpeed / baseSpeed);
                    float bob = Mathf.Sin(stepPhase) * MovementSettings.BobAmount * speedRatio;

                    // Adım yere bastığında çok hafif squash
                    float groundSquash = Mathf.Max(0f, -bob) * 1.2f;
                    cargo.transform.localScale = new Vector3(
                        baseScales[k].x * (1f + groundSquash * 0.3f),
                        baseScales[k].y * (1f - groundSquash * 0.5f),
                        baseScales[k].z * (1f + groundSquash * 0.3f)
                    );

                    cargo.transform.position = basePos[k] + popUp * (liftHeight * lift[k] * landFade) + camUp * bob;

                    // 2. GRADUAL ROTATION + HAFİF TİLT (Bank Tilt):
                    Vector3 tangent = path.TangentAtDistance(d);
                    Vector3 futureTangent = path.TangentAtDistance(d + 0.22f);
                    float turnCurvature = (futureTangent.x - tangent.x);
                    float targetTilt = Mathf.Clamp(-turnCurvature * 30f, -MovementSettings.TiltAmount, MovementSettings.TiltAmount);
                    currentTilt[k] = Mathf.Lerp(currentTilt[k], targetTilt, dt * 10f);

                    float targetYaw = CargoRunnerHeading.TargetYaw(tangent, 38f);
                    currentYaw[k] = Mathf.MoveTowardsAngle(currentYaw[k], targetYaw, MovementSettings.TurnSpeed * dt);

                    if (waddlers[k] != null)
                    {
                        waddlers[k].BankTilt = currentTilt[k];
                        waddlers[k].TurnToward(tangent, dt);
                    }
                    else if (runners[k] != null)
                    {
                        runners[k].TurnToward(tangent, dt);
                        cargo.transform.rotation = CargoRunnerHeading.Apply(cargo.transform.rotation, 0f, currentTilt[k]);
                    }
                    else
                    {
                        cargo.transform.rotation = CargoRunnerHeading.Apply(Quaternion.identity, currentYaw[k], currentTilt[k]);
                    }

                    // 5. HEDEFTE SNAP → APPROACH + SETTLE:
                    if (reachedShip)
                    {
                        arrived[k] = true;
                        onPath--;
                        cargo.transform.position = entranceNow;
                        StartCoroutine(HopCargoToShip(cargo, ship, entersLeft, armCubes[k]));
                        continue;
                    }
                }

                yield return null;
            }
        }

        '''

text = text[:start_idx] + new_section + text[end_idx:]

with open(r'Assets\Scripts\ShipDispatcher.cs', 'w', encoding='utf-8') as f:
    f.write(text)

print("Updated ShipDispatcher.cs with 5 core motion pillars!")
