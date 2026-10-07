using System.Collections.Generic;
using UnityEngine;

namespace CM101
{
    /// <summary>
    /// 길이와 연결 목록의 단일 기준(기획서 10장).
    /// - 표시 길이 = 선두 10cm + 연결 젤리 저장 길이 합계
    /// - 선두 이동 경로(trail, 높이 포함)를 기록하고 연결 젤리를 그 경로 위 간격 지점으로 구동한다.
    ///   간격은 수평 거리로 재고 높이는 궤적을 그대로 따른다 → 선두가 뛴 자리에서 뒤 동료도 같은 포물선으로 차례로 넘어간다.
    /// - 떨어진 동료는 revertAfter초 안에 회수하지 않으면 그 주변에 음식으로 흩어져 돌아간다.
    /// - 손실 요청은 같은 갱신 시점에 모아 선두에 가장 가까운 피격점 하나로 한 번만 처리한다.
    /// 실행 순서를 앞당겨(-100) 같은 프레임의 피해가 최종 섭취 판정보다 먼저 확정되게 한다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class JellyChain : MonoBehaviour
    {
        public static JellyChain I { get; private set; }

        public const int LeaderLength = 10;
        public const int FoodTotal = 90;
        public const int Goal = 100;

        public LeaderController leader;

        [Header("Loss / Recover")]
        public float lossProtection = 1f;      // 같은 위험으로 연속 절단 방지
        public float recoverProtection = 0.75f; // 회수 직후 보호

        [Header("Follow")]
        public float followGain = 10f;
        public float spacingFactor = 1.08f;
        public float fallbackDistance = 2.5f;
        [Tooltip("선두가 바로 뒤 동료 쪽으로 돌아설 때 동료가 선두 둘레를 돌아 뒤로 가는 속도(도/초)")] public float turnaroundSpeed = 720f;
        [Tooltip("돌아서기 중 첫 동료의 추종 세기")] public float turnaroundGain = 22f;

        [Header("Detached → Food")]
        [Tooltip("떨어진 동료가 음식으로 돌아가기까지 걸리는 시간(초)")] public float revertAfter = 5f;

        public readonly List<JellyUnit> Connected = new List<JellyUnit>();
        public readonly List<JellyUnit> Detached = new List<JellyUnit>();

        readonly List<Vector3> trail = new List<Vector3>(); // 오래된 점 → 최신 점
        readonly List<float> trailOdo = new List<float>();  // 각 점까지 선두가 움직인 누적 거리
        readonly List<float> trailGround = new List<float>(); // 각 점을 기록할 때 선두 아래 바닥 높이
        float headOdo;

        /// <summary>
        /// 선두 점프 알림. 움직이며 뛰면 동료는 궤적을 따라 자연히 같은 점프를 한다.
        /// 제자리에서 뛰면 궤적이 생기지 않으므로, 동료는 자리를 지킨 채 모습만 앞에서부터 차례로 폴짝 뛴다(순서가 섞이지 않게).
        /// </summary>
        public void NotifyLeaderJump(float vy)
        {
            if (!leader) return;
            Vector3 v = leader.Body.linearVelocity;
            v.y = 0f;
            if (v.magnitude > 0.8f) return;
            for (int i = 0; i < Connected.Count; i++)
                if (Connected[i] && Connected[i].Emote) Connected[i].Emote.PlayHopOnce(0.06f + i * 0.06f, 0.35f);
        }

        struct PendingHit
        {
            public JellyUnit unit;
            public LossType type;
            public Vector3 source;
        }

        readonly List<PendingHit> pending = new List<PendingHit>();
        bool pendingVacuum;
        Vector3 pendingVacuumSource;
        bool pendingLeaderStomp;
        Vector3 pendingStompSource;
        float protectedUntil;
        float lastSpaceMsgTime = -10f;
        int revertedPending, revertedLenPending;

        public int CurrentLength
        {
            get
            {
                int s = LeaderLength;
                foreach (var u in Connected) if (u) s += u.StoredLength;
                return s;
            }
        }

        public int DetachedLength
        {
            get
            {
                int s = 0;
                foreach (var u in Detached) if (u) s += u.StoredLength;
                return s;
            }
        }

        public bool IsProtected => Time.time < protectedUntil;
        public bool CanGrow(int length) => CurrentLength + length <= Goal;

        void Awake() { I = this; }
        void OnDestroy() { if (I == this) I = null; }

        void Start()
        {
            if (!leader) { Debug.LogError("[101CM] JellyChain: leader가 비어 있습니다."); enabled = false; return; }
            Vector3 p = leader.transform.position;
            Vector3 back = -leader.transform.forward;
            for (int i = 40; i >= 1; i--) { trail.Add(p + back * (i * 0.25f)); trailOdo.Add(-i * 0.25f); trailGround.Add(p.y - leader.Radius); }
            trail.Add(p);
            trailOdo.Add(0f);
            trailGround.Add(p.y - leader.Radius);
            headOdo = 0f;
        }

        // ------------------------------------------------------------------ 추종

        void FixedUpdate()
        {
            if (!leader) return;
            Connected.RemoveAll(u => !u);
            Detached.RemoveAll(u => !u);

            Vector3 head = leader.Body.position;
            // 선두가 왔던 길을 되짚으면(벽·매대에 막혀 앞뒤로 튕길 때, 뒤로 밀릴 때) 마지막 점을 지운다.
            // 지우지 않으면 경로가 제자리에서 접혀 길이만 늘고, 동료 목표가 선두 쪽으로 몰려 서로 밀치며 좌우로 떨린다. (2026-10-07)
            while (trail.Count >= 2)
            {
                Vector3 last = trail[trail.Count - 1], prev = trail[trail.Count - 2];
                if (Flat(head, prev) >= Flat(last, prev) - 0.005f) break;
                trail.RemoveAt(trail.Count - 1);
                trailOdo.RemoveAt(trailOdo.Count - 1);
                trailGround.RemoveAt(trailGround.Count - 1);
                headOdo = trailOdo[trailOdo.Count - 1];
            }
            // 경로는 선두가 옆으로 움직였을 때만 기록한다(점은 높이 포함). 간격도 수평 거리로 잰다.
            // 제자리 점프의 위아래 움직임이 경로 길이를 잡아먹으면 동료 목표가 선두 쪽으로 몰려 서로 섞이기 때문이다.
            if (trail.Count == 0 || Flat(head, trail[trail.Count - 1]) > 0.05f)
            {
                if (trail.Count > 0) headOdo += Flat(trail[trail.Count - 1], head);
                trail.Add(head);
                trailOdo.Add(headOdo);
                float gy = head.y - leader.Radius;
                if (Physics.Raycast(head, Vector3.down, out var gh, 6f, Layers.EnvMask, QueryTriggerInteraction.Ignore)) gy = gh.point.y;
                trailGround.Add(gy);
            }
            SettleTrail();
            UpdateTurnaround(head);
            if (turning) DriveTurnaround(head);
            else DriveConnected(head);
            RevertExpired();
        }

        static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        /// <summary>
        /// 선두가 점프 끝에 멈춰 서면 경로의 공중 점들이 그대로 남아 동료가 떠 있게 된다.
        /// 선두가 바닥에 서서 거의 움직이지 않을 때는 공중 점을 기록 당시 바닥 높이로 천천히 내려 동료도 내려앉게 한다.
        /// </summary>
        void SettleTrail()
        {
            Vector3 v = leader.Body.linearVelocity;
            v.y = 0f;
            if (!leader.IsGrounded || v.magnitude > 0.3f) return;
            float step = 5f * Time.fixedDeltaTime;
            for (int i = 0; i < trail.Count; i++)
            {
                float rest = trailGround[i] + leader.Radius;
                Vector3 p = trail[i];
                if (p.y > rest + 0.01f) { p.y = Mathf.MoveTowards(p.y, rest, step); trail[i] = p; }
            }
        }

        float Spacing(float rA, float rB) => (rA + rB) * spacingFactor;

        /// <summary>선두 중심 높이의 궤적 점을 반지름 r인 젤리의 중심 높이로 바꾼다(발바닥 높이를 맞춤).</summary>
        Vector3 ForRadius(Vector3 trailPoint, float r) => trailPoint + Vector3.up * (r - leader.Radius);

        void DriveConnected(Vector3 head)
        {
            float need = 0f;
            float prevR = leader.Radius;
            int idx = trail.Count;      // 다음에 볼 점 = trail[idx-1]
            Vector3 a = head;
            float accA = 0f;            // head에서 a까지의 경로 길이
            float maxSpeed = leader.CatchUpSpeed;

            for (int i = 0; i < Connected.Count; i++)
            {
                var u = Connected[i];
                need += Spacing(prevR, u.Radius);
                prevR = u.Radius;

                Vector3 target;
                while (true)
                {
                    if (idx - 1 < 0) { target = a; break; }
                    Vector3 b = trail[idx - 1];
                    float len = Flat(a, b);
                    if (accA + len >= need)
                    {
                        float t = len > 1e-5f ? (need - accA) / len : 0f;
                        target = Vector3.Lerp(a, b, t);
                        break;
                    }
                    accA += len;
                    a = b;
                    idx--;
                }

                target = ForRadius(target, u.Radius);

                // 매대 단차나 벽에 걸려 경로에서 오래 떨어져 있으면 경로 지점으로 옮겨 준다
                // (무리가 끊기거나 뒤 동료가 앞질러 순서가 섞이지 않게)
                Vector3 trailTarget = target;
                float gap = (trailTarget - u.Body.position).magnitude;
                bool stuck = gap > fallbackDistance &&
                             (trailTarget.y > u.Body.position.y + 0.4f ||
                              Physics.Linecast(u.Body.position, trailTarget, Layers.EnvMask, QueryTriggerInteraction.Ignore));
                if (stuck)
                {
                    u.FarTime += Time.fixedDeltaTime;
                    if (u.FarTime > 1.0f && !Physics.CheckSphere(trailTarget + Vector3.up * 0.15f, u.Radius * 0.9f, Layers.EnvMask, QueryTriggerInteraction.Ignore))
                    {
                        u.Body.position = trailTarget + Vector3.up * 0.15f;
                        u.Body.linearVelocity = Vector3.zero;
                        u.FarTime = 0f;
                    }
                }
                else u.FarTime = 0f;

                // 모서리에 걸려 경로 지점에서 너무 멀어지면 바로 앞 젤리를 향한다(벽 관통 금지, 끌림 허용)
                Vector3 toT = target - u.Body.position;
                toT.y = 0f;
                if (toT.magnitude > fallbackDistance)
                {
                    Vector3 prevPos = i == 0 ? head : Connected[i - 1].Body.position;
                    float prevRadius = i == 0 ? leader.Radius : Connected[i - 1].Radius;
                    Vector3 d = u.Body.position - prevPos;
                    d.y = 0f;
                    Vector3 dir = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.back;
                    target = prevPos + dir * Spacing(prevRadius, u.Radius);
                    target.y = prevPos.y + (u.Radius - prevRadius);
                }
                // 경로가 급하게 꺾인 곳에서는 경로 길이로는 간격이 맞아도 실제 거리가 가까워져
                // 선두의 몸 밀어내기(SeparateFromBody)와 서로 당기고 밀며 제자리에서 두두둑 떨렸다 → 목표를 실제 거리로도 띄운다 (2026-10-07)
                {
                    Vector3 ap = i == 0 ? head : Connected[i - 1].Body.position;
                    float ar = i == 0 ? leader.Radius : Connected[i - 1].Radius;
                    target = KeepClear(target, ap, (ar + u.Radius) * 1.03f, u.Body.position);
                    if (i > 0) target = KeepClear(target, head, (leader.Radius + u.Radius) * 1.05f, u.Body.position);
                }
                // 바라보는 방향 기준: 바로 앞 동료(첫 동료는 선두). 속도 방향은 작은 떨림에도 휙휙 바뀌어서 쓰지 않는다
                u.AheadPos = i == 0 ? head : Connected[i - 1].Body.position;
                u.HasAhead = true;
                u.DriveAlong(target, maxSpeed, followGain);
            }

            // 필요 이상 오래된 경로 정리
            float keep = need + 3f;
            float acc = 0f;
            Vector3 prev = head;
            for (int j = trail.Count - 1; j >= 0; j--)
            {
                acc += Flat(prev, trail[j]);
                prev = trail[j];
                if (acc > keep && j > 0)
                {
                    trail.RemoveRange(0, j);
                    trailOdo.RemoveRange(0, j);
                    trailGround.RemoveRange(0, j);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ 돌아서기 (2026-10-07)
        // 선두가 바로 뒤 동료 쪽으로 돌아서 걸으면 예전에는 경로가 앞쪽에 남아 동료가 선두 앞을 막고,
        // 선두는 자기 몸 충돌로 0.35초마다 튕겨 동료가 앞뒤로 두두둑 끊겼다.
        // 이제는 첫 동료가 선두 둘레를 돌아 선두 뒤로 비켜서고, 나머지는 줄처럼 앞 젤리를 따라 끌려온다.
        // 첫 동료가 선두 뒤에 오면 지금 줄 모양 그대로 경로를 새로 깔고 평소 경로 따라가기로 돌아간다.

        bool turning;
        Vector3 turnDir;      // 선두가 가려는 방향
        Vector3 orbitDir;     // 선두에서 첫 동료 목표 쪽 방향(선두 둘레를 돈다)
        float turnSide = 1f;  // 정면으로 마주쳤을 때 도는 쪽 (+1 / -1)
        float turnTime;

        public bool IsTurning => turning;
        public bool IsYielding(JellyUnit u) => turning && u && u.State == JellyState.Connected;

        /// <summary>선두가 연결 동료 u 쪽으로 걸으려 할 때 부른다. 비켜서기로 처리하면 true(선두는 튕기지 않는다).</summary>
        public bool TryYield(JellyUnit u, Vector3 moveDir)
        {
            if (!u || Connected.Count == 0) return false;
            if (turning) return true;
            if (u != Connected[0]) return false; // 꼬리 쪽 몸에 부딪히는 것은 예전처럼 튕긴다
            BeginTurnaround(moveDir);
            return true;
        }

        void UpdateTurnaround(Vector3 head)
        {
            if (Connected.Count == 0) { turning = false; return; }
            Vector3 m = leader.MoveInput;
            m.y = 0f;
            bool hasInput = m.sqrMagnitude > 0.01f;
            var f0 = Connected[0];
            Vector3 d = f0.Body.position - head;
            d.y = 0f;

            if (!turning)
            {
                if (!hasInput || leader.IsStunned) return;
                float s = Spacing(leader.Radius, f0.Radius);
                if (d.magnitude < s * 1.6f && d.sqrMagnitude > 1e-4f && Vector3.Dot(m.normalized, d.normalized) > 0.5f)
                    BeginTurnaround(m);
                return;
            }

            turnTime += Time.fixedDeltaTime;
            if (hasInput) turnDir = m.normalized;
            bool behind = d.sqrMagnitude > 1e-4f && Vector3.Dot(d.normalized, turnDir) < -0.8f
                          && Vector3.Dot(orbitDir, turnDir) < -0.95f;
            if (behind || turnTime > 1.5f) EndTurnaround();
        }

        void BeginTurnaround(Vector3 moveDir)
        {
            moveDir.y = 0f;
            if (moveDir.sqrMagnitude < 1e-4f || Connected.Count == 0) return;
            turning = true;
            turnTime = 0f;
            turnDir = moveDir.normalized;
            var f0 = Connected[0];
            Vector3 head = leader.Body.position;
            Vector3 d = f0.Body.position - head;
            d.y = 0f;
            orbitDir = d.sqrMagnitude > 1e-4f ? d.normalized : turnDir;

            float ang = Vector3.SignedAngle(orbitDir, -turnDir, Vector3.up);
            if (Mathf.Abs(ang) < 170f) turnSide = Mathf.Sign(ang);
            else
            {
                // 정면으로 마주쳤으면 벽이 없는 쪽으로 돈다
                float s = Spacing(leader.Radius, f0.Radius);
                Vector3 pr = head + Quaternion.Euler(0f, 90f, 0f) * orbitDir * s;
                Vector3 pl = head + Quaternion.Euler(0f, -90f, 0f) * orbitDir * s;
                bool rFree = !Physics.CheckSphere(pr, f0.Radius * 0.9f, Layers.EnvMask, QueryTriggerInteraction.Ignore);
                bool lFree = !Physics.CheckSphere(pl, f0.Radius * 0.9f, Layers.EnvMask, QueryTriggerInteraction.Ignore);
                turnSide = rFree || !lFree ? 1f : -1f;
            }
        }

        void EndTurnaround()
        {
            turning = false;
            RebuildTrailFromChain();
        }

        void DriveTurnaround(Vector3 head)
        {
            float dt = Time.fixedDeltaTime;
            float maxSpeed = leader.CatchUpSpeed;
            Vector3 want = -turnDir;
            float ang = Vector3.SignedAngle(orbitDir, want, Vector3.up);
            if (Mathf.Abs(ang) > 170f) ang = turnSide * Mathf.Abs(ang);
            float step = Mathf.Sign(ang) * Mathf.Min(Mathf.Abs(ang), turnaroundSpeed * dt);
            orbitDir = Quaternion.Euler(0f, step, 0f) * orbitDir;

            Vector3 prevPos = head;
            float prevR = leader.Radius;
            for (int i = 0; i < Connected.Count; i++)
            {
                var u = Connected[i];
                float s = Spacing(prevR, u.Radius);
                Vector3 dir;
                if (i == 0) dir = orbitDir;
                else
                {
                    Vector3 d = u.Body.position - prevPos;
                    d.y = 0f;
                    dir = d.sqrMagnitude > 1e-4f ? d.normalized : -turnDir;
                }
                Vector3 target = prevPos + dir * s;
                target.y = prevPos.y + (u.Radius - prevR);
                if (i > 0) target = KeepClear(target, head, (leader.Radius + u.Radius) * 1.1f, u.Body.position);
                u.AheadPos = prevPos;
                u.HasAhead = true;
                u.FarTime = 0f;
                u.DriveAlong(target, maxSpeed, i == 0 ? turnaroundGain : followGain);
                prevPos = u.Body.position;
                prevR = u.Radius;
            }
        }

        /// <summary>target이 center에서 수평으로 minDist보다 가까우면 center 바깥쪽으로 밀어낸다(높이는 그대로).</summary>
        static Vector3 KeepClear(Vector3 target, Vector3 center, float minDist, Vector3 from)
        {
            Vector3 off = target - center;
            off.y = 0f;
            float m = off.magnitude;
            if (m >= minDist) return target;
            Vector3 dir;
            if (m > 1e-3f) dir = off / m;
            else
            {
                dir = from - center;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.back;
            }
            Vector3 r = center + dir * minDist;
            r.y = target.y;
            return r;
        }

        /// <summary>지금 줄 모양(선두 → 동료들 → 꼬리 뒤 3u)을 그대로 경로로 깐다. 선두 중심 높이 기준.</summary>
        void RebuildTrailFromChain()
        {
            Vector3 head = leader.Body.position;
            var pts = new List<Vector3> { head };
            foreach (var u in Connected)
            {
                if (!u) continue;
                pts.Add(u.Body.position + Vector3.up * (leader.Radius - u.Radius));
            }
            Vector3 tailDir = pts.Count >= 2 ? pts[pts.Count - 1] - pts[pts.Count - 2] : -turnDir;
            tailDir.y = 0f;
            tailDir = tailDir.sqrMagnitude > 1e-4f ? tailDir.normalized : -turnDir;
            pts.Add(pts[pts.Count - 1] + tailDir * 3f);

            trail.Clear(); trailOdo.Clear(); trailGround.Clear();
            // 오래된 점(꼬리 뒤)부터 선두까지 0.25 간격으로 채운다
            var seq = new List<Vector3>();
            for (int k = pts.Count - 1; k >= 1; k--)
            {
                Vector3 a = pts[k], b = pts[k - 1];
                int n = Mathf.Max(1, Mathf.CeilToInt(Flat(a, b) / 0.25f));
                for (int j = 0; j < n; j++) seq.Add(Vector3.Lerp(a, b, j / (float)n));
            }
            seq.Add(head);
            float total = 0f;
            for (int j = 1; j < seq.Count; j++) total += Flat(seq[j - 1], seq[j]);
            float odo = -total;
            for (int j = 0; j < seq.Count; j++)
            {
                if (j > 0) odo += Flat(seq[j - 1], seq[j]);
                trail.Add(seq[j]);
                trailOdo.Add(odo);
                trailGround.Add(seq[j].y - leader.Radius);
            }
            headOdo = 0f;
            trailOdo[trailOdo.Count - 1] = 0f;
        }

        /// <summary>
        /// 순간이동 뒤 경로를 새로 깐다: 선두 뒤쪽(back 방향)으로 곧은 경로를 만들고 연결된 동료를 그 위 간격 지점에 세운다.
        /// 벽에 막히는 자리는 선두 쪽으로 당겨 세운다. (개발자 치트 F3/F4용)
        /// </summary>
        public void ResetTrail(Vector3 back)
        {
            back.y = 0f;
            back = back.sqrMagnitude > 1e-4f ? back.normalized : Vector3.back;
            turning = false;
            Vector3 p = leader.Body.position;
            trail.Clear(); trailOdo.Clear(); trailGround.Clear();
            float gy = p.y - leader.Radius;
            int n = Mathf.CeilToInt((TotalSpacing() + 4f) / 0.25f);
            for (int i = n; i >= 1; i--)
            {
                trail.Add(p + back * (i * 0.25f));
                trailOdo.Add(-i * 0.25f);
                trailGround.Add(gy);
            }
            trail.Add(p); trailOdo.Add(0f); trailGround.Add(gy);
            headOdo = 0f;
            float need = 0f, prevR = leader.Radius;
            Vector3 last = p;
            foreach (var u in Connected)
            {
                if (!u) continue;
                need += Spacing(prevR, u.Radius);
                prevR = u.Radius;
                Vector3 t = ForRadius(SampleTrail(need), u.Radius);
                if (Physics.CheckSphere(t, u.Radius * 0.9f, Layers.EnvMask, QueryTriggerInteraction.Ignore)) t = last + Vector3.up * 0.05f;
                u.Body.position = t;
                u.Body.linearVelocity = Vector3.zero;
                u.FarTime = 0f;
                last = t;
            }
            Physics.SyncTransforms();
        }

        /// <summary>head에서 경로를 따라 수평 거리 dist만큼 뒤의 지점(선두 중심 높이).</summary>
        public Vector3 SampleTrail(float dist)
        {
            Vector3 a = leader.Body.position;
            float acc = 0f;
            for (int j = trail.Count - 1; j >= 0; j--)
            {
                Vector3 b = trail[j];
                float len = Flat(a, b);
                if (acc + len >= dist)
                {
                    float t = len > 1e-5f ? (dist - acc) / len : 0f;
                    return Vector3.Lerp(a, b, t);
                }
                acc += len;
                a = b;
            }
            return a;
        }

        float TotalSpacing()
        {
            float s = 0f;
            float prevR = leader.Radius;
            foreach (var u in Connected)
            {
                s += Spacing(prevR, u.Radius);
                prevR = u.Radius;
            }
            return s;
        }

        // ------------------------------------------------------------------ 성장

        public JellyUnit AddFromFood(FoodType type, int variant, FoodKind kind = FoodKind.None, int length = 0)
        {
            float r = JellyUnit.SizeFor(length > 0 ? length : FoodData.Length(type)) * 0.5f;
            float lastR = Connected.Count > 0 ? Connected[Connected.Count - 1].Radius : leader.Radius;
            Vector3 cand = ForRadius(SampleTrail(TotalSpacing() + Spacing(lastR, r)), r);
            Vector3 spawn;
            if (!Physics.CheckSphere(cand, r * 0.9f, Layers.EnvMask | Layers.JellyMask | Layers.HazardMask, QueryTriggerInteraction.Ignore))
                spawn = cand;
            else
            {
                Transform tail = Connected.Count > 0 ? Connected[Connected.Count - 1].transform : leader.transform;
                spawn = tail.position + Vector3.up * (lastR + r + 0.05f);
            }

            var u = JellyUnit.Create(type, variant, spawn, kind, length);
            Connected.Add(u);
            return u;
        }

        // ------------------------------------------------------------------ 손실

        public void ReportHit(JellyUnit u, LossType type, Vector3 source)
        {
            if (!u || u.State != JellyState.Connected) return;
            pending.Add(new PendingHit { unit = u, type = type, source = source });
        }

        /// <summary>선두가 밟힘 → 선두 뒤 연결 전체가 흩어진다.</summary>
        public void ReportLeaderStomp(Vector3 source)
        {
            pendingLeaderStomp = true;
            pendingStompSource = source;
        }

        public void ReportVacuum(Vector3 source)
        {
            pendingVacuum = true;
            pendingVacuumSource = source;
        }

        void Update()
        {
            if (pending.Count == 0 && !pendingVacuum && !pendingLeaderStomp) return;

            if (!GameManager.IsPlaying || IsProtected || Connected.Count == 0 || DevCheats.GodMode)
            {
                pending.Clear();
                pendingVacuum = false;
                pendingLeaderStomp = false;
                return;
            }

            if (pendingVacuum)
            {
                DetachAllVacuum(pendingVacuumSource);
            }
            else if (pendingLeaderStomp)
            {
                ScatterFrom(0, pendingStompSource, true);
            }
            else
            {
                int best = int.MaxValue;
                PendingHit bestHit = default;
                foreach (var h in pending)
                {
                    int i = Connected.IndexOf(h.unit);
                    if (i >= 0 && i < best)
                    {
                        best = i;
                        bestHit = h;
                    }
                }
                if (best != int.MaxValue)
                {
                    if (bestHit.type == LossType.Chunk) ChunkFrom(best, bestHit.source);
                    else ScatterFrom(best, bestHit.source, false, bestHit.type == LossType.FallScatter ? "떨어진 상품에 맞아" : "직원에게 밟혀");
                }
            }

            pending.Clear();
            pendingVacuum = false;
            pendingLeaderStomp = false;
            protectedUntil = Time.time + lossProtection;
        }

        void ChunkFrom(int index, Vector3 source)
        {
            var chunk = new JellyChunk();
            for (int i = index; i < Connected.Count; i++) chunk.Members.Add(Connected[i]);
            Connected.RemoveRange(index, Connected.Count - index);

            foreach (var u in chunk.Members)
            {
                Vector3 away = u.Body.position - source;
                away.y = 0f;
                away = away.sqrMagnitude > 1e-4f ? away.normalized : Random.insideUnitSphere;
                u.SetDetached(chunk, u.Body.position + new Vector3(away.x, 0f, away.z) * 0.5f);
                u.Kick(away * 2f + Vector3.up * 1.5f);
                Detached.Add(u);
            }
            GameManager.RecordLoss(chunk.Members.Count);
            GameManager.Notify($"상품에 맞아 동료 {chunk.Members.Count}마리({chunk.TotalLength}cm)가 덩어리로 떨어졌어요", MsgKind.Warn, 3f);
        }

        /// <summary>맞은 동료부터 꼬리까지만 한 마리씩 흩어진다. 맞은 동료 앞쪽(선두 쪽)은 그대로 연결을 유지한다.</summary>
        void ScatterFrom(int index, Vector3 source, bool leaderStomped = false, string cause = "직원에게 밟혀")
        {
            int n = 0, len = 0;
            for (int i = index; i < Connected.Count; i++)
            {
                var u = Connected[i];
                Vector3 away = u.Body.position - source;
                away.y = 0f;
                if (away.sqrMagnitude < 1e-4f) away = Random.insideUnitSphere;
                away.y = 0f;
                Vector3 dir = (away.normalized + Random.insideUnitSphere * 0.6f);
                dir.y = 0f;
                dir.Normalize();
                u.SetDetached(null, u.Body.position + dir * 0.8f);
                bool flat = u.Emote && u.Emote.IsFlat; // 발밑에서 납작해진 젤리는 거의 튀지 않는다
                if (leaderStomped)
                {
                    u.RecoverableAt = Time.time + (leader ? leader.stompStun : 1.3f) + 0.8f;
                    if (u.Emote && !flat) u.Emote.PlayFlatten(0.9f + i * 0.04f); // 선두가 밟히면 모두 납작
                    flat = false; // 선두가 밟힌 경우엔 모두 사방으로 튕겨 나간다
                }
                u.Kick(flat ? dir * 0.6f : dir * Random.Range(2.5f, 3.5f) + Vector3.up * 2.5f);
                Detached.Add(u);
                n++;
                len += u.StoredLength;
            }
            Connected.RemoveRange(index, Connected.Count - index);
            GameManager.RecordLoss(n);
            GameManager.Notify(leaderStomped
                ? $"납작! 밟혀서 동료 {n}마리({len}cm)가 모두 흩어졌어요"
                : $"{cause} 뒤쪽 동료 {n}마리({len}cm)가 흩어졌어요", MsgKind.Warn, 3f);
        }

        void DetachAllVacuum(Vector3 source)
        {
            int n = Connected.Count, len = CurrentLength - LeaderLength;
            Vector3 center = Vector3.zero;
            foreach (var u in Connected) center += u.Body.position;
            if (n > 0) center /= n;
            foreach (var u in Connected)
            {
                // 뭉치지 않게 한 마리씩 사방으로 튕겨 나간다 (청소기에서 멀어지는 쪽 + 무리 중심에서 바깥쪽 + 무작위)
                Vector3 away = u.Body.position - source; away.y = 0f;
                Vector3 outward = u.Body.position - center; outward.y = 0f;
                Vector2 rnd = Random.insideUnitCircle;
                Vector3 dir = (away.normalized * 0.5f + outward.normalized * 0.7f + new Vector3(rnd.x, 0f, rnd.y) * 0.8f);
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-3f) dir = new Vector3(rnd.x, 0f, rnd.y);
                dir.Normalize();
                u.SetDetached(null, u.Body.position + dir * Random.Range(1.5f, 3f));
                u.Kick(dir * Random.Range(3f, 4.5f) + Vector3.up * 2.2f);
                Detached.Add(u);
            }
            Connected.Clear();
            GameManager.RecordLoss(n);
            GameManager.Notify($"청소기에 휩쓸려 동료 {n}마리({len}cm)가 사방으로 흩어졌어요! 흡입되기 전에 구하세요", MsgKind.Warn, 3.5f);
        }

        // ------------------------------------------------------------------ 기분 표현 / 튜토리얼

        /// <summary>선두가 기분을 표현하면 연결된 동료들이 앞에서부터 차례로 따라 한다.</summary>
        public void PlayEmote(EmoteType e)
        {
            if (leader && leader.Emote) leader.Emote.Play(e);
            for (int i = 0; i < Connected.Count; i++)
                if (Connected[i] && Connected[i].Emote) Connected[i].Emote.Play(e, 0.12f + i * 0.07f);
            foreach (var u in Detached)
                if (u && u.Emote) u.Emote.Play(e, Random.Range(0.2f, 0.6f));
        }

        /// <summary>튜토리얼 종료: 연습으로 붙인 동료를 모두 없앤다(손 흔들고 사라짐).</summary>
        public int ClearAllForTutorial()
        {
            int n = 0;
            for (int i = 0; i < Connected.Count; i++)
                if (Connected[i]) { Connected[i].Vanish(0.05f * i); n++; }
            foreach (var u in Detached)
                if (u) { u.Vanish(0f); n++; }
            Connected.Clear();
            Detached.Clear();
            pending.Clear();
            pendingVacuum = false;
            return n;
        }

        // ------------------------------------------------------------------ 회수 / 소멸

        public bool TryRecover(JellyUnit u)
        {
            if (!u || u.State != JellyState.Detached || !GameManager.IsPlaying) return false;

            // 떨어진 동료는 덩어리여도 Space 한 번에 한 마리씩 붙는다
            var group = new List<JellyUnit> { u };
            group.RemoveAll(x => !x || x.State != JellyState.Detached);
            if (group.Count == 0) return false;
            foreach (var g in group) if (Time.time < g.RecoverableAt) return false;

            int len = 0;
            foreach (var g in group) len += g.StoredLength;
            if (CurrentLength + len > Goal)
            {
                if (Time.time - lastSpaceMsgTime > 1.5f)
                {
                    lastSpaceMsgTime = Time.time;
                    GameManager.Notify($"{len}cm 공간이 필요해요", MsgKind.Warn);
                }
                return false;
            }

            foreach (var g in group)
            {
                Detached.Remove(g);
                if (g.Chunk != null) g.Chunk.Members.Remove(g);
                g.SetConnected();
                Connected.Add(g);
            }
            protectedUntil = Mathf.Max(protectedUntil, Time.time + recoverProtection);
            GameManager.RecordRecovered(group.Count);
            GameManager.Notify($"동료 회수 +{len}cm", MsgKind.Good, 1.5f);
            return true;
        }

        // ------------------------------------------------------------------ 떨어진 동료 → 음식

        /// <summary>떨어진 지 revertAfter초가 지난 동료를 그 주변 바닥에 음식으로 흩뿌린다(청소기에 끌려가는 중이거나 납작한 동안은 기다린다).</summary>
        void RevertExpired()
        {
            if (!GameManager.IsPlaying || Detached.Count == 0) return;
            for (int i = Detached.Count - 1; i >= 0; i--)
            {
                var u = Detached[i];
                if (!u || u.State != JellyState.Detached || u.DetachedAt < 0f) continue;
                if (Time.time - u.DetachedAt < revertAfter) continue;
                if (Time.time < u.PullUntil || u.VacuumZoneTime > 0f) continue;
                if (u.Emote && u.Emote.IsFlat) continue;
                RevertToFood(u);
            }
            if (revertedPending > 0)
            {
                GameManager.Notify(revertedPending == 1
                    ? $"떨어진 동료가 음식으로 돌아갔어요 (+{revertedLenPending}cm)"
                    : $"떨어진 동료 {revertedPending}마리가 음식으로 흩어졌어요 ({revertedLenPending}cm)", MsgKind.Info, 2.5f);
                revertedPending = 0;
                revertedLenPending = 0;
            }
        }

        public void RevertToFood(JellyUnit u)
        {
            if (!u || u.State != JellyState.Detached) return;
            Vector3 p = u.Body.position;
            float gy = p.y - u.Radius;
            if (Physics.Raycast(p + Vector3.up * 0.1f, Vector3.down, out var hit, 4f, Layers.EnvMask, QueryTriggerInteraction.Ignore)) gy = hit.point.y;
            Vector3 ground = new Vector3(p.x, gy, p.z);
            Vector3 spot = ScatterSpot(ground);

            Detached.Remove(u);
            if (u.Chunk != null) u.Chunk.Members.Remove(u);
            var f = FoodItem.Create(u.Type, u.Variant, spot, u.Kind, u.StoredLength);
            f.isTutorial = GameManager.I && GameManager.I.TutorialActive;
            revertedPending++;
            revertedLenPending += u.StoredLength;
            u.MarkAbsorbed();
        }

        /// <summary>떨어진 자리 주변 0.4~1.2u의 빈 바닥(같은 높이, 벽 너머 아님, 다른 음식과 0.7u 이상).</summary>
        static Vector3 ScatterSpot(Vector3 ground)
        {
            for (int tries = 0; tries < 12; tries++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(0.4f, 1.2f);
                Vector3 c = ground + new Vector3(r.x, 0f, r.y);
                if (!Physics.Raycast(c + Vector3.up * 0.4f, Vector3.down, out var h, 0.8f, Layers.EnvMask, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(h.point.y - ground.y) > 0.15f) continue;
                Vector3 g = h.point;
                if (Physics.CheckSphere(g + Vector3.up * 0.3f, 0.22f, Layers.EnvMask, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Linecast(ground + Vector3.up * 0.3f, g + Vector3.up * 0.3f, Layers.EnvMask, QueryTriggerInteraction.Ignore)) continue;
                bool near = false;
                foreach (var f in FoodItem.All)
                    if (f && !f.Consumed && (f.transform.position - (g + Vector3.up * 0.25f)).sqrMagnitude < 0.7f * 0.7f) { near = true; break; }
                if (near) continue;
                return g;
            }
            return ground;
        }

        public void Absorb(JellyUnit u)
        {
            if (!u || u.State != JellyState.Detached) return;
            Detached.Remove(u);
            if (u.Chunk != null) u.Chunk.Members.Remove(u);
            GameManager.RecordAbsorbed(1);
            if (GameManager.I) GameManager.I.SpawnReplacement(u.Type, u.Variant, u.transform.position, u.StoredLength, u.Kind);
            u.MarkAbsorbed();
        }
    }
}
