using System.Collections.Generic;
using UnityEngine;

namespace CM101
{
    /// <summary>
    /// 청소기(기획서 07장). 경로를 왕복하며 전방 흡입 영역에 젤리가 들어오면 선두 뒤 연결 전체를 끊고 흡입구로 끌어당긴다.
    /// 영역 진입 후 최소 2초가 지나야 완전 흡입(소멸). 벽 너머 젤리는 당기지 않는다.
    /// 선두도 끌려가며, 탈출 여유를 준 뒤 흡입구에 닿으면 실패.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VacuumCleaner : MonoBehaviour
    {
        public Vector3 pointA;
        public Vector3 pointB;
        public float speed = 1.2f;
        public float pauseAtEnds = 1.5f;
        public float turnTime = 0.6f;

        public Vector3 zoneLocalCenter = new Vector3(0f, 0.4f, 2.0f);
        public Vector3 zoneSize = new Vector3(2.2f, 0.8f, 2.6f);
        public Vector3 mouthLocal = new Vector3(0f, 0.25f, 0.7f);
        public float pullSpeed = 2.5f;
        public float leaderPull = 1.4f;
        public float absorbTime = 2f;
        public float mouthRadius = 0.5f;
        public Renderer zoneMarker;
        public Color zoneColor = new Color(1f, 0.35f, 0.15f, 1f);

        Rigidbody rb;
        bool toB = true;
        float pauseTimer;
        float leaderZoneTime;
        MaterialPropertyBlock mpb;
        readonly Collider[] buf = new Collider[48];
        HashSet<JellyUnit> inZoneNow = new HashSet<JellyUnit>();
        HashSet<JellyUnit> inZonePrev = new HashSet<JellyUnit>();

        public float LeaderZoneTime => leaderZoneTime;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            mpb = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (!zoneMarker) return;
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 6f);
            zoneMarker.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", Color.Lerp(zoneColor * 0.6f, zoneColor, pulse));
            mpb.SetColor("_EmissionColor", zoneColor * (0.4f + pulse));
            zoneMarker.SetPropertyBlock(mpb);
        }

        void FixedUpdate()
        {
            if (!GameManager.IsPlaying) return;
            Move(Time.fixedDeltaTime);
            Suction(Time.fixedDeltaTime);
        }

        void Move(float dt)
        {
            Vector3 target = toB ? pointB : pointA;
            target.y = rb.position.y;
            Vector3 to = target - rb.position;

            if (pauseTimer > 0f)
            {
                pauseTimer -= dt;
                // 멈춘 동안 다음 방향으로 회전
                Vector3 next = (toB ? pointB : pointA) - rb.position;
                next.y = 0f;
                if (next.sqrMagnitude > 1e-3f)
                    rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, Quaternion.LookRotation(next.normalized), 180f / turnTime * dt));
                return;
            }

            float step = speed * dt;
            if (to.magnitude <= step)
            {
                rb.MovePosition(target);
                toB = !toB;
                pauseTimer = pauseAtEnds;
            }
            else
            {
                rb.MovePosition(rb.position + to.normalized * step);
                rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, Quaternion.LookRotation(to.normalized), 180f / turnTime * dt));
            }
        }

        void Suction(float dt)
        {
            Vector3 mouth = transform.TransformPoint(mouthLocal);
            Vector3 zc = transform.TransformPoint(zoneLocalCenter);
            int n = Physics.OverlapBoxNonAlloc(zc, zoneSize * 0.5f, buf, transform.rotation, Layers.JellyMask, QueryTriggerInteraction.Ignore);

            inZoneNow.Clear();
            bool leaderIn = false;
            bool cut = false;
            LeaderController leader = null;

            for (int i = 0; i < n; i++)
            {
                var col = buf[i];
                Vector3 p = col.transform.position;
                if (Physics.Linecast(mouth, p, Layers.EnvMask, QueryTriggerInteraction.Ignore)) continue; // 벽 너머

                var ld = col.GetComponent<LeaderController>();
                if (ld)
                {
                    leader = ld;
                    leaderIn = true;
                    cut = true;
                    Vector3 d = mouth - ld.Body.position;
                    d.y = 0f;
                    if (d.sqrMagnitude > 1e-4f) ld.ExternalVelocity += d.normalized * leaderPull;
                    continue;
                }

                var u = col.GetComponent<JellyUnit>();
                if (!u) continue;
                if (u.State == JellyState.Connected)
                {
                    cut = true;
                    continue;
                }
                if (u.State != JellyState.Detached) continue;

                inZoneNow.Add(u);
                u.PullUntil = Time.time + 0.15f;
                u.VacuumZoneTime += dt;
                Vector3 dm = mouth - u.Body.position;
                dm.y = 0f;
                Vector3 pv = dm.sqrMagnitude > 1e-4f ? dm.normalized * pullSpeed : Vector3.zero;
                var cv = u.Body.linearVelocity;
                u.Body.linearVelocity = new Vector3(pv.x, cv.y, pv.z);
                if (dm.magnitude < mouthRadius + u.Radius && u.VacuumZoneTime >= absorbTime && JellyChain.I)
                    JellyChain.I.Absorb(u);
            }

            foreach (var u in inZonePrev)
                if (u && !inZoneNow.Contains(u)) u.VacuumZoneTime = 0f;
            var tmp = inZonePrev;
            inZonePrev = inZoneNow;
            inZoneNow = tmp;

            if (cut && JellyChain.I) JellyChain.I.ReportVacuum(transform.position);

            if (leaderIn)
            {
                leaderZoneTime += dt;
                Vector3 d = mouth - leader.Body.position;
                d.y = 0f;
                if (leaderZoneTime >= absorbTime && d.magnitude < mouthRadius + leader.Radius + 0.1f)
                    GameManager.I.Fail("청소기에 빨려 들어갔어요");
            }
            else leaderZoneTime = 0f;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(zoneLocalCenter, zoneSize);
            Gizmos.DrawWireSphere(mouthLocal, mouthRadius);
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawLine(pointA, pointB);
        }
    }
}
