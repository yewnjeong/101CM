using System.Collections.Generic;
using UnityEngine;

namespace CM101
{
    public enum RollerLook { Fruit, Can, Mixed }

    /// <summary>
    /// 굴러다니는 과일·음료 캔. 시작하면 영역 안에 여러 개를 만들고, 일정한 속도로 굴러다니며 벽에 튕긴다.
    /// 방해 요소: 선두와 동료를 세게 밀어낸다(손실 없음). 부딪힌 선두는 2u 정도 밀려나며 잠깐 조작이 잘 먹히지 않는다.
    /// 선두는 점프로 피하거나 매대 위로 올라가면 된다.
    /// </summary>
    public class RollerSpawner : MonoBehaviour
    {
        public RollerLook look = RollerLook.Fruit;
        public int count = 5;
        public Vector3 areaCenter;
        public Vector3 areaSize = new Vector3(10f, 2f, 10f);
        public float speed = 1.6f;
        public float radius = 0.3f;
        public float pushLeader = 5.5f;
        public float pushJelly = 3.5f;

        readonly List<Roller> rollers = new List<Roller>();
        static PhysicsMaterial bouncy;

        void Start()
        {
            if (!bouncy)
                bouncy = new PhysicsMaterial("RollerBounce")
                {
                    bounciness = 0.85f, dynamicFriction = 0.05f, staticFriction = 0.05f,
                    bounceCombine = PhysicsMaterialCombine.Maximum, frictionCombine = PhysicsMaterialCombine.Minimum
                };
            for (int i = 0; i < count; i++) Spawn(i);
        }

        void Spawn(int i)
        {
            Vector3 p = transform.position;
            for (int tries = 0; tries < 20; tries++)
            {
                Vector3 c = areaCenter + new Vector3(Random.Range(-0.45f, 0.45f) * areaSize.x, 0f, Random.Range(-0.45f, 0.45f) * areaSize.z);
                c.y = radius + 0.02f;
                if (!Physics.CheckSphere(c, radius + 0.1f, Layers.EnvMask | Layers.HazardMask, QueryTriggerInteraction.Ignore)) { p = c; break; }
            }
            bool can = look == RollerLook.Can || (look == RollerLook.Mixed && i % 2 == 1);
            var go = new GameObject(can ? "RollingCan" : "RollingFruit");
            go.layer = Layers.Hazard;
            go.transform.SetParent(transform, true);
            go.transform.position = p;
            var col = go.AddComponent<SphereCollider>();
            col.radius = radius;
            col.sharedMaterial = bouncy;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            float d = radius * 2f;
            if (can)
            {
                var body = ProtoFactory.Prim(PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(d * 0.95f, d * 0.6f, d * 0.95f),
                    ProtoFactory.Mat(Color.HSVToRGB((i * 0.17f) % 1f, 0.7f, 0.95f), 0.8f), "Can");
                body.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                ProtoFactory.Prim(PrimitiveType.Cylinder, body.transform, new Vector3(0f, 0.52f, 0f), new Vector3(0.9f, 0.05f, 0.9f),
                    ProtoFactory.Mat(new Color(0.8f, 0.82f, 0.86f), 0.9f), "Lid");
            }
            else
            {
                Color c = new[] { new Color(1f, 0.3f, 0.3f), new Color(1f, 0.6f, 0.15f), new Color(0.6f, 0.9f, 0.3f), new Color(0.95f, 0.85f, 0.25f) }[i % 4];
                ProtoFactory.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * d, ProtoFactory.Mat(c, 0.7f), "Fruit");
                ProtoFactory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, radius, 0f), new Vector3(d * 0.35f, d * 0.08f, d * 0.18f),
                    ProtoFactory.Mat(new Color(0.3f, 0.7f, 0.25f), 0.4f), "Leaf");
            }
            var r = go.AddComponent<Roller>();
            r.owner = this;
            r.dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            rollers.Add(r);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(areaCenter + Vector3.up * areaSize.y * 0.5f, areaSize);
        }
    }

    /// <summary>굴러다니는 물건 하나. 수평 속도를 일정하게 유지하고 영역을 벗어나면 안쪽으로 돌아온다.</summary>
    public class Roller : MonoBehaviour
    {
        public RollerSpawner owner;
        public Vector3 dir = Vector3.forward;
        Rigidbody rb;
        float stuckTimer;
        Vector3 lastPos;
        float lastPushTime = -10f;

        void Awake() { rb = GetComponent<Rigidbody>(); }

        void FixedUpdate()
        {
            if (!owner) return;
            if (!GameManager.IsPlaying) { rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f); return; }
            Vector3 v = rb.linearVelocity;
            Vector3 h = new Vector3(v.x, 0f, v.z);
            if (h.sqrMagnitude > 0.04f) dir = h.normalized;

            // 영역 밖이면 중심으로 방향을 튼다
            Vector3 c = owner.areaCenter;
            Vector3 p = rb.position;
            if (Mathf.Abs(p.x - c.x) > owner.areaSize.x * 0.5f || Mathf.Abs(p.z - c.z) > owner.areaSize.z * 0.5f)
            {
                Vector3 back = c - p;
                back.y = 0f;
                dir = Vector3.Slerp(dir, back.normalized, 0.2f).normalized;
            }

            // 걸려서 멈추면 방향을 바꾼다
            if ((p - lastPos).sqrMagnitude < 0.0004f) stuckTimer += Time.fixedDeltaTime; else stuckTimer = 0f;
            lastPos = p;
            if (stuckTimer > 0.6f)
            {
                dir = Quaternion.Euler(0f, Random.Range(100f, 260f), 0f) * dir;
                stuckTimer = 0f;
            }

            Vector3 want = dir * owner.speed;
            rb.linearVelocity = new Vector3(want.x, v.y, want.z);

            // 떨어지면 영역 중심으로 복귀
            if (p.y < -3f) { rb.position = c + Vector3.up * 0.5f; rb.linearVelocity = Vector3.zero; }
        }

        float lastHitTime = -10f;

        void OnCollisionEnter(Collision col) => Hit(col);

        void OnCollisionStay(Collision col)
        {
            if (Time.time - lastHitTime > 0.3f) Hit(col); // 붙어서 계속 밀고 들어오면 다시 민다
        }

        void Hit(Collision col)
        {
            if (!owner) return;
            Vector3 away = col.transform.position - transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = dir;
            away.Normalize();
            // 굴러가던 방향도 섞어서 '치고 지나가는' 느낌을 준다
            Vector3 push = (away * 0.7f + dir * 0.5f).normalized;
            var ld = col.collider.GetComponent<LeaderController>();
            if (ld)
            {
                lastHitTime = Time.time;
                ld.Knock(push * owner.pushLeader);
                if (Time.time - lastPushTime > 3f)
                {
                    lastPushTime = Time.time;
                    GameManager.Notify(name.Contains("Can") ? "데굴데굴 음료 캔에 밀렸어요" : "데굴데굴 과일에 밀렸어요", MsgKind.Info, 1.5f);
                }
                return;
            }
            var u = col.collider.GetComponent<JellyUnit>();
            if (u) { lastHitTime = Time.time; u.Nudge(push * owner.pushJelly); }
        }
    }
}
