using System.Collections.Generic;
using UnityEngine;

namespace CM101
{
    public enum JellyState { Connected, Detached, Absorbed }

    /// <summary>일반 충돌로 연결을 유지한 채 떨어진 분리 젤리 묶음. 한 번 접촉으로 전체 회수.</summary>
    public class JellyChunk
    {
        public readonly List<JellyUnit> Members = new List<JellyUnit>();

        public int TotalLength
        {
            get
            {
                int s = 0;
                foreach (var m in Members) if (m) s += m.StoredLength;
                return s;
            }
        }
    }

    /// <summary>
    /// 선두 뒤에 붙는 동료 젤리 한 마리. 고유 ID·저장 길이·상태를 가진다.
    /// 연결 상태의 이동은 JellyChain이 경로 추종으로 구동하고, 분리 상태에서는 스스로 꿈틀거린다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class JellyUnit : MonoBehaviour
    {
        static int nextId = 1;

        public int Id { get; private set; }
        public FoodType Type { get; private set; }
        public int Variant { get; private set; }
        public int StoredLength { get; private set; }
        public float Size { get; private set; }
        public float Radius => Size * 0.5f;
        public JellyState State { get; private set; } = JellyState.Connected;
        public JellyChunk Chunk { get; private set; }
        public Rigidbody Body { get; private set; }
        public SphereCollider Col { get; private set; }
        public JellyEmote Emote { get; private set; }

        [System.NonSerialized] public Vector3 Home;
        [System.NonSerialized] public float PullUntil;
        [System.NonSerialized] public float VacuumZoneTime;
        /// <summary>컨베이어 등 외부에서 매 물리 프레임 더해 주는 속도. DriveToward에서 소비된다.</summary>
        [System.NonSerialized] public Vector3 ExternalVelocity;

        [Header("Detached behaviour")]
        public float wanderRadius = 1.5f;      // 분리 지점에서 1~2타일
        public float wanderSpeed = 1.1f;
        public float stopNearLeaderDistance = 2.2f;

        Vector3 nudgeVel;
        Vector3 wanderTarget;
        float wanderTimer;
        float spawnTime;
        Transform visual;
        Transform beacon;
        Vector3 lastMoveDir = Vector3.forward;

        /// <summary>저장 길이에 따라 몸 크기를 조금 키운다. 3cm → 0.435, 8cm → 0.56.</summary>
        public static float SizeFor(int length) => 0.36f + 0.025f * length;

        public static JellyUnit Create(FoodType t, int variant, Vector3 position)
        {
            var go = new GameObject($"Jelly_{t}_{FoodData.Length(t)}cm");
            go.layer = Layers.Jelly;
            go.transform.position = position;
            var u = go.AddComponent<JellyUnit>();
            u.Init(t, variant);
            return u;
        }

        void Init(FoodType t, int variant)
        {
            Id = nextId++;
            Type = t;
            Variant = variant;
            StoredLength = FoodData.Length(t);
            Size = SizeFor(StoredLength);
            name = $"Jelly#{Id}_{t}_{StoredLength}cm";

            Col = gameObject.AddComponent<SphereCollider>();
            Col.radius = Radius;
            Col.sharedMaterial = ProtoFactory.JellyPhysics;

            Body = GetComponent<Rigidbody>();
            Body.mass = 0.6f;
            Body.freezeRotation = true;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            Body.linearDamping = 0f;

            visual = ProtoFactory.BuildBear(transform, Size, FoodData.JellyColor(t, variant), false);
            ProtoFactory.AddTopping(visual, t, Size);
            Emote = gameObject.AddComponent<JellyEmote>();

            beacon = ProtoFactory.Prim(PrimitiveType.Sphere, transform, Vector3.up * (Size * 1.0f), Vector3.one * 0.13f,
                ProtoFactory.Mat(new Color(1f, 0.85f, 0.4f), 0.1f, 2.0f), "Beacon").transform;
            beacon.gameObject.SetActive(false);

            spawnTime = Time.time;
            Home = transform.position;
        }

        // ------------------------------------------------------------------ 상태 전환 (JellyChain만 호출)

        public void SetConnected()
        {
            State = JellyState.Connected;
            Chunk = null;
            PullUntil = 0f;
            VacuumZoneTime = 0f;
            if (beacon) beacon.gameObject.SetActive(false);
        }

        public void SetDetached(JellyChunk chunk, Vector3 home)
        {
            State = JellyState.Detached;
            Chunk = chunk;
            Home = home;
            wanderTarget = Body.position;
            wanderTimer = Random.Range(0.4f, 1.0f);
            VacuumZoneTime = 0f;
            if (beacon) beacon.gameObject.SetActive(true);
        }

        public void MarkAbsorbed()
        {
            State = JellyState.Absorbed;
            Destroy(gameObject);
        }

        /// <summary>튜토리얼 종료 등으로 조용히 사라진다: 콩 튀고 작아지며 소멸.</summary>
        public void Vanish(float delay)
        {
            State = JellyState.Absorbed;
            if (Col) Col.enabled = false;
            if (Body) { Body.linearVelocity = Vector3.zero; Body.isKinematic = true; }
            if (beacon) beacon.gameObject.SetActive(false);
            StartCoroutine(VanishRoutine(delay));
        }

        System.Collections.IEnumerator VanishRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (Emote) Emote.Play(EmoteType.Wave);
            yield return new WaitForSeconds(0.5f);
            Vector3 start = transform.position;
            float t = 0f;
            while (t < 0.45f)
            {
                t += Time.deltaTime;
                float k = t / 0.45f;
                transform.position = start + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.4f;
                if (visual) visual.localScale = Vector3.one * Mathf.Lerp(1f, 0f, k * k);
                yield return null;
            }
            Destroy(gameObject);
        }

        /// <summary>수평 성분은 서서히 감쇠하는 밀림으로, 수직 성분은 즉시 속도로 준다.</summary>
        public void Kick(Vector3 v)
        {
            nudgeVel = new Vector3(v.x, 0f, v.z);
            var cv = Body.linearVelocity;
            Body.linearVelocity = new Vector3(cv.x, Mathf.Max(cv.y, v.y), cv.z);
        }

        public void Nudge(Vector3 v)
        {
            nudgeVel += new Vector3(v.x, 0f, v.z);
        }

        public void DriveToward(Vector3 target, float maxSpeed, float gain)
        {
            Vector3 to = target - Body.position;
            to.y = 0f;
            Vector3 v = Vector3.ClampMagnitude(to * gain, maxSpeed) + nudgeVel + ExternalVelocity;
            ExternalVelocity = Vector3.zero;
            Vector3 cur = Body.linearVelocity;
            Body.linearVelocity = new Vector3(v.x, cur.y, v.z);
        }

        // ------------------------------------------------------------------ 물리 갱신

        void FixedUpdate()
        {
            if (State == JellyState.Absorbed) return;
            float dt = Time.fixedDeltaTime;
            if (nudgeVel.sqrMagnitude > 1e-5f) nudgeVel = Vector3.MoveTowards(nudgeVel, Vector3.zero, 8f * dt);

            // 물리 오류 복구: 월드 밖으로 떨어짐
            if (Body.position.y < -4f)
            {
                Vector3 p = State == JellyState.Detached ? Home :
                    (JellyChain.I && JellyChain.I.leader ? JellyChain.I.leader.Body.position : Home);
                Body.position = p + Vector3.up * 0.6f;
                Body.linearVelocity = Vector3.zero;
            }

            AntiStack(dt);

            if (State != JellyState.Detached) return;
            if (Time.time < PullUntil) return; // 청소기가 끌어당기는 중
            if (!GameManager.IsPlaying)
            {
                DriveToward(Body.position, 0f, 1f);
                return;
            }

            if (Chunk != null) ChunkBehaviour();
            else WanderBehaviour();
        }

        /// <summary>다른 젤리 위에 올라탔으면 옆으로 미끄러져 내려오게 한다(젤리끼리 쌓임 방지).</summary>
        void AntiStack(float dt)
        {
            if (!Physics.SphereCast(Body.position, Radius * 0.6f, Vector3.down, out var hit, Radius * 0.9f, 1 << Layers.Jelly, QueryTriggerInteraction.Ignore))
                return;
            if (hit.collider == Col) return;
            Vector3 side = Body.position - hit.collider.transform.position;
            side.y = 0f;
            if (side.sqrMagnitude < 1e-4f) side = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            side.Normalize();
            nudgeVel += side * 25f * dt;
            var v = Body.linearVelocity;
            Body.linearVelocity = new Vector3(v.x, Mathf.Min(v.y, -2f), v.z);
        }

        void ChunkBehaviour()
        {
            int idx = Chunk.Members.IndexOf(this);
            if (idx <= 0)
            {
                // 덩어리 머리: 제자리에서 꿈틀
                float t = Time.time * 1.3f + Id;
                Vector3 wig = new Vector3(Mathf.Sin(t) * 0.25f, 0f, Mathf.Cos(t * 0.7f) * 0.15f);
                DriveToward(Home + wig, 0.6f, 2f);
            }
            else
            {
                var prev = Chunk.Members[idx - 1];
                if (!prev) { DriveToward(Body.position, 0f, 1f); return; }
                Vector3 d = Body.position - prev.Body.position;
                d.y = 0f;
                float sp = (prev.Radius + Radius) * 1.05f;
                Vector3 dir = d.sqrMagnitude > 1e-4f ? d.normalized : -lastMoveDir;
                DriveToward(prev.Body.position + dir * sp, 2.5f, 6f);
            }
        }

        void WanderBehaviour()
        {
            var leader = JellyChain.I ? JellyChain.I.leader : null;
            if (leader)
            {
                Vector3 dl = leader.Body.position - Body.position;
                dl.y = 0f;
                if (dl.sqrMagnitude < stopNearLeaderDistance * stopNearLeaderDistance)
                {
                    DriveToward(Body.position, 0f, 1f); // 선두가 다가오면 잠시 멈춘다
                    return;
                }
            }

            wanderTimer -= Time.fixedDeltaTime;
            Vector3 toT = wanderTarget - Body.position;
            toT.y = 0f;
            if (wanderTimer <= 0f || toT.sqrMagnitude < 0.01f) PickWanderTarget();
            DriveToward(wanderTarget, wanderSpeed, 3f);
        }

        void PickWanderTarget()
        {
            wanderTimer = Random.Range(0.8f, 1.8f);
            Vector3 pos = Body.position;
            for (int i = 0; i < 6; i++)
            {
                Vector2 r = Random.insideUnitCircle * wanderRadius;
                Vector3 p = new Vector3(Home.x + r.x, pos.y, Home.z + r.y);
                // 바닥이 있는지(낭떠러지 방지)
                if (!Physics.Raycast(p + Vector3.up * 0.2f, Vector3.down, out var hit, 1.2f, Layers.EnvMask, QueryTriggerInteraction.Ignore))
                    continue;
                // 벽/선반/위험물 내부가 아닌지
                if (Physics.CheckSphere(hit.point + Vector3.up * (Radius + 0.05f), Radius, Layers.EnvMask | Layers.HazardMask, QueryTriggerInteraction.Ignore))
                    continue;
                // 벽 너머가 아닌지
                if (Physics.Linecast(pos, p, Layers.EnvMask, QueryTriggerInteraction.Ignore))
                    continue;
                wanderTarget = p;
                return;
            }
            wanderTarget = pos;
        }

        // ------------------------------------------------------------------ 시각 효과

        void Update()
        {
            if (!visual || !Body || State == JellyState.Absorbed) return;
            float age = Time.time - spawnTime;
            float pop = age < 0.25f ? Mathf.SmoothStep(0.2f, 1f, age / 0.25f) : 1f;

            Vector3 v = Body.linearVelocity;
            v.y = 0f;
            float spd = v.magnitude;
            if (spd > 0.15f) lastMoveDir = v / spd;
            visual.rotation = Quaternion.Slerp(visual.rotation, Quaternion.LookRotation(lastMoveDir, Vector3.up), 1f - Mathf.Exp(-10f * Time.deltaTime));

            bool detached = State == JellyState.Detached;
            float wob = Mathf.Sin(Time.time * (detached ? 9f : 6f) + Id) * (detached ? 0.07f : 0.03f);
            float squash = Mathf.Clamp01(spd / 5f) * 0.08f;
            visual.localScale = new Vector3(1f + wob * 0.5f + squash * 0.5f, 1f - wob - squash, 1f + wob * 0.5f) * pop;

            if (beacon && beacon.gameObject.activeSelf)
            {
                beacon.localPosition = Vector3.up * (Size * 1.0f + 0.1f + Mathf.Sin(Time.time * 4f + Id) * 0.06f);
                float bs = 0.11f + Mathf.PingPong(Time.time * 0.12f, 0.04f);
                beacon.localScale = Vector3.one * bs;
            }
        }
    }
}
