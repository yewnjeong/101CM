using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CM101
{
    /// <summary>
    /// 선두 곰 젤리.
    /// - WASD 카메라 기준 걷기. 길이가 늘수록 느려진다.
    /// - Shift 달리기: 게이지(최대 runMaxDuration초)를 쓰고, 멈추면 쓴 만큼 쿨타임 동안 다시 달릴 수 없다.
    /// - Space 섭취(1회 입력당 1개).
    /// - 자기 몸 충돌: Jelly↔Lead 물리 충돌은 꺼 두고(올라타기 방지) 수평 방향으로만 막고 튕긴다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class LeaderController : MonoBehaviour
    {
        [Header("Body")]
        public float bodySize = 0.6f; // 1타일 = 1유닛 ≈ 몸길이 1.7배

        [Header("Walk")]
        public float walkSpeed = 2.6f;              // 10cm일 때 걷기
        public float slowestWalkFactor = 0.7f;      // 100cm일 때 걷기 배율
        public float acceleration = 18f;

        [Header("Run")]
        public float runMultiplier = 1.9f;
        public float runMaxDuration = 2.5f;         // 게이지 가득 → 0까지
        public float runCooldownFull = 4f;          // 게이지를 다 썼을 때 쿨타임
        public float runCooldownMin = 0.8f;

        [Header("Eat")]
        public float eatRangeBodyLengths = 1f;

        [Header("Self collision")]
        public float bounceBodyLengths = 0.2f;
        public float trapReleaseTime = 2f;

        public OrbitCamera cam;

        public Rigidbody Body { get; private set; }
        public SphereCollider Col { get; private set; }
        public JellyEmote Emote { get; private set; }
        public float Radius => bodySize * 0.5f;

        /// <summary>길이에 따른 감속 배율 (10cm → 1, 100cm → slowestWalkFactor).</summary>
        public float LengthSpeedFactor
        {
            get
            {
                int len = JellyChain.I ? JellyChain.I.CurrentLength : JellyChain.LeaderLength;
                float t = Mathf.InverseLerp(JellyChain.LeaderLength, JellyChain.Goal, len);
                return Mathf.Lerp(1f, slowestWalkFactor, t);
            }
        }

        public float CurrentWalkSpeed => walkSpeed * LengthSpeedFactor;
        public float CurrentRunSpeed => CurrentWalkSpeed * runMultiplier;
        /// <summary>추종 젤리가 따라잡을 수 있는 최고 속도 계산용.</summary>
        public float RunSpeed => walkSpeed * runMultiplier;

        public bool IsRunning { get; private set; }
        public float RunGauge { get; private set; } = 1f;       // 0~1
        public float RunCooldown { get; private set; }          // 남은 쿨타임(초)
        public float RunCooldownTotal { get; private set; }
        public bool RunReady => RunCooldown <= 0f && RunGauge > 0f;

        public FoodItem CandidateFood { get; private set; }
        public FinalJelly CandidateFinal { get; private set; }

        [System.NonSerialized] public Vector3? AutoMoveTarget;
        [System.NonSerialized] public Vector3 ExternalVelocity;

        Vector3 inputDir;
        Vector3 moveVel;
        Vector3 bounceVel;
        float bounceCooldown;
        float trapTimer;
        float trapSampleTimer;
        bool wasRunning;
        Transform visual;
        Vector3 lastDir = Vector3.forward;
        Vector3 startPos;
        FinalJelly highlightedFinal;
        const float BounceDecel = 14f;

        readonly Dictionary<Collider, float> ignoreUntil = new Dictionary<Collider, float>();
        readonly List<Collider> ignoreExpired = new List<Collider>();
        readonly Collider[] buf = new Collider[48];

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Col = GetComponent<SphereCollider>();
            Col.radius = Radius;
            Col.sharedMaterial = ProtoFactory.JellyPhysics;
            Body.freezeRotation = true;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            visual = transform.Find("Visual");
            Emote = GetComponent<JellyEmote>();
            if (!Emote) Emote = gameObject.AddComponent<JellyEmote>();
            startPos = transform.position;
            lastDir = transform.forward;
            // 올라타기 방지: 선두와 젤리는 물리적으로 부딪히지 않게 하고 코드로 수평 충돌만 처리한다.
            Physics.IgnoreLayerCollision(Layers.Jelly, Layers.Lead, true);
        }

        // ------------------------------------------------------------------ 입력

        void Update()
        {
            inputDir = Vector3.zero;

            if (!GameManager.IsPlaying)
            {
                IsRunning = false;
                ClearCandidates();
                UpdateVisual();
                return;
            }

            bool shift = false;
            var kb = Keyboard.current;
            if (kb != null)
            {
                Vector2 m = Vector2.zero;
                if (kb.wKey.isPressed) m.y += 1f;
                if (kb.sKey.isPressed) m.y -= 1f;
                if (kb.dKey.isPressed) m.x += 1f;
                if (kb.aKey.isPressed) m.x -= 1f;
                float yaw = cam ? cam.Yaw : 0f;
                inputDir = Quaternion.Euler(0f, yaw, 0f) * new Vector3(m.x, 0f, m.y);
                if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();
                shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            }

            UpdateRun(shift && inputDir.sqrMagnitude > 0.01f, Time.deltaTime);
            FindCandidates();
            if (kb != null && kb.spaceKey.wasPressedThisFrame) TryEat();
            if (kb != null && JellyChain.I)
            {
                // 기분 표현: 1 손 흔들기, 2 신나서 빙글빙글, 3 폴짝폴짝, 4 꾸벅 인사
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) JellyChain.I.PlayEmote(EmoteType.Wave);
                else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) JellyChain.I.PlayEmote(EmoteType.Spin);
                else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) JellyChain.I.PlayEmote(EmoteType.Hop);
                else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) JellyChain.I.PlayEmote(EmoteType.Bow);
            }
            UpdateVisual();
        }

        void UpdateRun(bool wantRun, float dt)
        {
            if (RunCooldown > 0f)
            {
                RunCooldown -= dt;
                RunGauge = RunCooldownTotal > 0f ? Mathf.Lerp(1f, RunGaugeAtCooldownStart, RunCooldown / RunCooldownTotal) : 1f;
                if (RunCooldown <= 0f)
                {
                    RunCooldown = 0f;
                    RunGauge = 1f;
                }
                IsRunning = false;
                wasRunning = false;
                return;
            }

            IsRunning = wantRun && RunGauge > 0f;
            if (IsRunning)
            {
                RunGauge = Mathf.Max(0f, RunGauge - dt / runMaxDuration);
                wasRunning = true;
                if (RunGauge <= 0f) StartRunCooldown();
            }
            else if (wasRunning)
            {
                StartRunCooldown();
            }
        }

        float RunGaugeAtCooldownStart;

        void StartRunCooldown()
        {
            IsRunning = false;
            wasRunning = false;
            RunGaugeAtCooldownStart = RunGauge;
            RunCooldownTotal = Mathf.Max(runCooldownMin, (1f - RunGauge) * runCooldownFull);
            RunCooldown = RunCooldownTotal;
        }

        void ClearCandidates()
        {
            CandidateFood = null;
            CandidateFinal = null;
            FoodItem.SetHighlighted(null);
            if (highlightedFinal) highlightedFinal.SetHighlighted(false);
            highlightedFinal = null;
        }

        /// <summary>도달 범위 안 + 벽/선반에 가려지지 않은 가장 가까운 후보 하나.</summary>
        void FindCandidates()
        {
            CandidateFood = null;
            CandidateFinal = null;
            float range = Radius + bodySize * eatRangeBodyLengths + 0.25f;
            Vector3 c = Body.position;
            int n = Physics.OverlapSphereNonAlloc(c, range, buf, Layers.FoodMask, QueryTriggerInteraction.Collide);
            float bestF = float.MaxValue, bestJ = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var col = buf[i];
                var f = col.GetComponent<FoodItem>();
                var fj = f ? null : col.GetComponent<FinalJelly>();
                if (!f && !fj) continue;
                Vector3 p = col.transform.position;
                float d = Vector3.Distance(c, p);
                if (d > range) continue;
                if (Physics.Linecast(c, p, Layers.EnvMask, QueryTriggerInteraction.Ignore)) continue;
                if (f && !f.Consumed && d < bestF) { bestF = d; CandidateFood = f; }
                else if (fj && d < bestJ) { bestJ = d; CandidateFinal = fj; }
            }
            if (CandidateFood && CandidateFinal)
            {
                if (bestF <= bestJ) CandidateFinal = null;
                else CandidateFood = null;
            }

            FoodItem.SetHighlighted(CandidateFood);
            if (highlightedFinal != CandidateFinal)
            {
                if (highlightedFinal) highlightedFinal.SetHighlighted(false);
                highlightedFinal = CandidateFinal;
                if (highlightedFinal) highlightedFinal.SetHighlighted(true);
            }
        }

        void TryEat()
        {
            if (CandidateFinal)
            {
                GameManager.I.TryEatFinal(CandidateFinal);
                return;
            }
            var f = CandidateFood;
            if (!f) return;
            var chain = JellyChain.I;
            int len = f.Length;
            if (!chain.CanGrow(len))
            {
                GameManager.Notify($"{len}cm 공간이 필요해요 (남은 공간 {JellyChain.Goal - chain.CurrentLength}cm)", MsgKind.Warn);
                return;
            }
            FoodType t = f.Type;
            int variant = f.Variant;
            if (!f.TryConsume()) return;
            chain.AddFromFood(t, variant);
            GameManager.I.OnFoodEaten(t, len);
            CandidateFood = null;
        }

        // ------------------------------------------------------------------ 이동

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            ExpireIgnores();

            Vector3 desired;
            if (AutoMoveTarget.HasValue)
            {
                Vector3 to = AutoMoveTarget.Value - Body.position;
                to.y = 0f;
                desired = to.magnitude > 0.2f ? to.normalized * walkSpeed : Vector3.zero;
                if (desired.sqrMagnitude > 0.01f) lastDir = desired.normalized;
            }
            else if (GameManager.IsPlaying)
            {
                desired = inputDir * (IsRunning ? CurrentRunSpeed : CurrentWalkSpeed);
            }
            else desired = Vector3.zero;

            // 자기 몸 충돌: 진행 방향의 연결 젤리를 통과하지 못하고 살짝 튕긴다(수평만)
            bool blockedBySelf = false;
            JellyUnit blocker = null;
            if (desired.sqrMagnitude > 0.01f && !AutoMoveTarget.HasValue)
            {
                Vector3 dir = desired.normalized;
                float dist = 0.08f + desired.magnitude * dt * 2f;
                var hits = Physics.SphereCastAll(Body.position, Radius * 0.92f, dir, dist, 1 << Layers.Jelly, QueryTriggerInteraction.Ignore);
                foreach (var h in hits)
                {
                    if (ignoreUntil.ContainsKey(h.collider)) continue;
                    var u = h.collider.GetComponent<JellyUnit>();
                    if (!u || u.State != JellyState.Connected) continue;

                    Vector3 nrm = Body.position - u.Body.position;
                    nrm.y = 0f;
                    if (nrm.sqrMagnitude < 1e-4f) nrm = -dir;
                    nrm.Normalize();

                    float into = Vector3.Dot(desired, -nrm);
                    if (into > 0f) desired += nrm * into;
                    blockedBySelf = true;
                    blocker = u;

                    if (bounceCooldown <= 0f)
                    {
                        float d = bounceBodyLengths * bodySize;
                        bounceVel = nrm * Mathf.Sqrt(2f * BounceDecel * d);
                        bounceCooldown = 0.35f;
                        u.Nudge(-nrm * 0.8f);
                    }
                }
            }

            bounceCooldown -= dt;
            bounceVel = Vector3.MoveTowards(bounceVel, Vector3.zero, BounceDecel * dt);
            moveVel = Vector3.MoveTowards(moveVel, desired, acceleration * dt);

            Vector3 v = Body.linearVelocity;
            Vector3 final = moveVel + bounceVel + ExternalVelocity;
            Body.linearVelocity = new Vector3(final.x, v.y, final.z);
            ExternalVelocity = Vector3.zero;

            SeparateFromBody();

            // 자기 몸 갇힘 해제
            if (blockedBySelf && inputDir.sqrMagnitude > 0.1f)
            {
                trapSampleTimer -= dt;
                if (trapSampleTimer <= 0f)
                {
                    trapSampleTimer = 0.25f;
                    trapTimer = IsEnclosed() ? trapTimer + 0.25f : 0f;
                }
                if (trapTimer >= trapReleaseTime && blocker)
                {
                    ReleaseBlocker(blocker, inputDir);
                    trapTimer = 0f;
                }
            }
            else if (!blockedBySelf)
            {
                trapTimer = Mathf.Max(0f, trapTimer - dt);
            }

            // 분리 젤리 회수: 선두가 닿으면
            if (GameManager.IsPlaying && JellyChain.I)
            {
                int n = Physics.OverlapSphereNonAlloc(Body.position, Radius + 0.12f, buf, 1 << Layers.Jelly, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var u = buf[i].GetComponent<JellyUnit>();
                    if (u && u.State == JellyState.Detached) JellyChain.I.TryRecover(u);
                }
            }

            // 물리 오류 복구
            if (Body.position.y < -4f)
            {
                Body.position = startPos + Vector3.up * 0.5f;
                Body.linearVelocity = Vector3.zero;
                moveVel = Vector3.zero;
                GameManager.Notify("안전한 위치로 돌아왔어요", MsgKind.Info);
            }
        }

        /// <summary>
        /// 물리 충돌 대신 수평 방향으로만 연결 젤리를 밀어낸다. 높이 차가 커도(경사로 등) 서로 겹치지 않게 하되,
        /// 위아래로 밀어 올리는 힘은 절대 만들지 않는다 → 올라타기 없음.
        /// </summary>
        void SeparateFromBody()
        {
            int n = Physics.OverlapSphereNonAlloc(Body.position, Radius + 0.35f, buf, 1 << Layers.Jelly, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var u = buf[i].GetComponent<JellyUnit>();
                if (!u || u.State != JellyState.Connected || ignoreUntil.ContainsKey(u.Col)) continue;
                Vector3 d = u.Body.position - Body.position;
                if (Mathf.Abs(d.y) > Radius + u.Radius) continue;
                d.y = 0f;
                float minD = (Radius + u.Radius) * 0.98f;
                float dist = d.magnitude;
                if (dist >= minD) continue;
                Vector3 dir = dist > 1e-4f ? d / dist : -lastDir;
                float pen = minD - dist;
                // 젤리를 밀어내고, 선두는 조금만 밀린다
                u.Body.position += dir * pen * 0.7f;
                Body.position -= dir * pen * 0.3f;
                u.Nudge(dir * 0.6f);
            }
        }

        bool IsEnclosed()
        {
            int mask = (1 << Layers.Jelly) | Layers.EnvMask | Layers.HazardMask;
            bool anySelf = false;
            for (int k = 0; k < 8; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward;
                var hits = Physics.SphereCastAll(Body.position, Radius * 0.85f, dir, 0.45f, mask, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                foreach (var h in hits)
                {
                    if (h.collider == Col) continue;
                    var u = h.collider.GetComponent<JellyUnit>();
                    if (u && u.State != JellyState.Connected) continue;
                    if (u) anySelf = true;
                    blocked = true;
                    break;
                }
                if (!blocked) return false;
            }
            return anySelf;
        }

        void ReleaseBlocker(JellyUnit u, Vector3 wantDir)
        {
            ignoreUntil[u.Col] = Time.time + 1.2f;
            Vector3 side = Vector3.Cross(Vector3.up, wantDir.normalized);
            u.Nudge(side * 2.5f);
            GameManager.Notify("동료가 옆으로 비켜 길을 열었어요", MsgKind.Info, 1.5f);
        }

        void ExpireIgnores()
        {
            if (ignoreUntil.Count == 0) return;
            ignoreExpired.Clear();
            foreach (var kv in ignoreUntil)
                if (!kv.Key || Time.time >= kv.Value) ignoreExpired.Add(kv.Key);
            foreach (var c in ignoreExpired) ignoreUntil.Remove(c);
        }

        // ------------------------------------------------------------------ 시각

        void UpdateVisual()
        {
            if (!visual) return;
            if (inputDir.sqrMagnitude > 0.01f) lastDir = inputDir.normalized;
            visual.rotation = Quaternion.Slerp(visual.rotation, Quaternion.LookRotation(lastDir, Vector3.up), 1f - Mathf.Exp(-12f * Time.deltaTime));
            Vector3 v = Body ? Body.linearVelocity : Vector3.zero;
            v.y = 0f;
            float spd = v.magnitude;
            bool moving = spd > 0.2f;
            float bob = Mathf.Sin(Time.time * (moving ? (IsRunning ? 18f : 11f) : 3f)) * (moving ? 0.05f : 0.025f);
            visual.localScale = new Vector3(1f + bob * 0.5f, 1f - bob, 1f + bob * 0.5f);
        }
    }
}
