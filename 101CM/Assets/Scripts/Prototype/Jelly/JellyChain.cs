using System.Collections.Generic;
using UnityEngine;

namespace CM101
{
    /// <summary>
    /// 길이와 연결 목록의 단일 기준(기획서 10장).
    /// - 표시 길이 = 선두 10cm + 연결 젤리 저장 길이 합계
    /// - 선두 이동 경로(trail)를 기록하고 연결 젤리를 그 경로 위 간격 지점으로 구동한다(경로 추종 + 물리 충돌 혼합).
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

        public readonly List<JellyUnit> Connected = new List<JellyUnit>();
        public readonly List<JellyUnit> Detached = new List<JellyUnit>();

        readonly List<Vector3> trail = new List<Vector3>(); // 오래된 점 → 최신 점

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
            for (int i = 40; i >= 1; i--) trail.Add(p + back * (i * 0.25f));
            trail.Add(p);
        }

        // ------------------------------------------------------------------ 추종

        void FixedUpdate()
        {
            if (!leader) return;
            Connected.RemoveAll(u => !u);
            Detached.RemoveAll(u => !u);

            Vector3 head = leader.Body.position;
            if (trail.Count == 0 || (trail[trail.Count - 1] - head).sqrMagnitude > 0.05f * 0.05f) trail.Add(head);
            DriveConnected(head);
        }

        float Spacing(float rA, float rB) => (rA + rB) * spacingFactor;

        void DriveConnected(Vector3 head)
        {
            float need = 0f;
            float prevR = leader.Radius;
            int idx = trail.Count;      // 다음에 볼 점 = trail[idx-1]
            Vector3 a = head;
            float accA = 0f;            // head에서 a까지의 경로 길이
            float maxSpeed = leader.RunSpeed * 1.7f;

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
                    float len = Vector3.Distance(a, b);
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
                }
                u.DriveToward(target, maxSpeed, followGain);
            }

            // 필요 이상 오래된 경로 정리
            float keep = need + 3f;
            float acc = 0f;
            Vector3 prev = head;
            for (int j = trail.Count - 1; j >= 0; j--)
            {
                acc += Vector3.Distance(prev, trail[j]);
                prev = trail[j];
                if (acc > keep && j > 0)
                {
                    trail.RemoveRange(0, j);
                    break;
                }
            }
        }

        /// <summary>head에서 경로를 따라 dist만큼 뒤의 지점.</summary>
        public Vector3 SampleTrail(float dist)
        {
            Vector3 a = leader.Body.position;
            float acc = 0f;
            for (int j = trail.Count - 1; j >= 0; j--)
            {
                Vector3 b = trail[j];
                float len = Vector3.Distance(a, b);
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

        public JellyUnit AddFromFood(FoodType type, int variant)
        {
            float r = JellyUnit.SizeFor(FoodData.Length(type)) * 0.5f;
            float lastR = Connected.Count > 0 ? Connected[Connected.Count - 1].Radius : leader.Radius;
            Vector3 cand = SampleTrail(TotalSpacing() + Spacing(lastR, r));
            Vector3 spawn;
            if (!Physics.CheckSphere(cand, r * 0.9f, Layers.EnvMask | Layers.JellyMask | Layers.HazardMask, QueryTriggerInteraction.Ignore))
                spawn = cand;
            else
            {
                Transform tail = Connected.Count > 0 ? Connected[Connected.Count - 1].transform : leader.transform;
                spawn = tail.position + Vector3.up * (lastR + r + 0.05f);
            }

            var u = JellyUnit.Create(type, variant, spawn);
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

            if (!GameManager.IsPlaying || IsProtected || Connected.Count == 0)
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
                    else ScatterFrom(best, bestHit.source);
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

        void ScatterFrom(int index, Vector3 source, bool leaderStomped = false)
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
                : $"직원에게 밟혀 동료 {n}마리({len}cm)가 흩어졌어요", MsgKind.Warn, 3f);
        }

        void DetachAllVacuum(Vector3 source)
        {
            int n = Connected.Count, len = CurrentLength - LeaderLength;
            foreach (var u in Connected)
            {
                u.SetDetached(null, u.Body.position);
                Detached.Add(u);
            }
            Connected.Clear();
            GameManager.RecordLoss(n);
            GameManager.Notify($"청소기에 휩쓸려 동료 {n}마리({len}cm)가 모두 떨어졌어요! 흡입되기 전에 구하세요", MsgKind.Warn, 3.5f);
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

            var group = u.Chunk != null ? new List<JellyUnit>(u.Chunk.Members) : new List<JellyUnit> { u };
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
                g.SetConnected();
                Connected.Add(g);
            }
            protectedUntil = Mathf.Max(protectedUntil, Time.time + recoverProtection);
            GameManager.RecordRecovered(group.Count);
            GameManager.Notify(group.Count > 1 ? $"덩어리 회수! 동료 {group.Count}마리 +{len}cm" : $"동료 회수 +{len}cm", MsgKind.Good, 1.8f);
            return true;
        }

        public void Absorb(JellyUnit u)
        {
            if (!u || u.State != JellyState.Detached) return;
            Detached.Remove(u);
            if (u.Chunk != null) u.Chunk.Members.Remove(u);
            GameManager.RecordAbsorbed(1);
            if (GameManager.I) GameManager.I.SpawnReplacement(u.Type, u.Variant, u.transform.position, u.StoredLength);
            u.MarkAbsorbed();
        }
    }
}
