using UnityEngine;

namespace CM101
{
    /// <summary>
    /// 직원 발(프로토타입용 단순 위험). 정해진 경로를 걸으며 일정 거리마다 발을 내딛는다.
    /// 착지 1.5초 전 바닥 그림자로 예고. 선두가 밟히면 실패, 연결 젤리가 밟히면 밟힌 젤리부터 꼬리까지 흩어진다.
    /// 시야/소음 감지(순찰→의심→확인→복귀)는 2단계에서 추가한다.
    /// </summary>
    public class StaffFoot : MonoBehaviour
    {
        public Vector3[] waypoints;
        public float moveSpeed = 1.8f;
        public float stepDistance = 2.2f;
        public float warnTime = 1.5f;
        public float liftHeight = 1.6f;
        public float dropTime = 0.12f;
        public float plantedTime = 0.7f;
        public float liftTime = 0.35f;
        public Vector3 footHalfExtents = new Vector3(0.5f, 0.35f, 0.8f);

        public Transform foot;
        public Renderer shadow;
        public Collider footCollider;
        public Color shadowIdle = new Color(0.1f, 0.1f, 0.12f, 1f);
        public Color shadowWarn = new Color(0.9f, 0.15f, 0.1f, 1f);

        enum S { Moving, Warning, Dropping, Planted, Lifting }

        S state;
        float t;
        int wp;
        float traveled;
        MaterialPropertyBlock mpb;
        readonly Collider[] buf = new Collider[32];

        void Start()
        {
            mpb = new MaterialPropertyBlock();
            if (footCollider) footCollider.enabled = false;
            SetFootHeight(liftHeight);
            SetShadow(0.55f, shadowIdle);
        }

        void SetFootHeight(float y)
        {
            if (foot) foot.localPosition = new Vector3(0f, y, 0f);
        }

        void SetShadow(float scale01, Color c)
        {
            if (!shadow) return;
            shadow.transform.localScale = new Vector3(footHalfExtents.x * 2.2f * scale01, 0.005f, footHalfExtents.z * 2.2f * scale01);
            shadow.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            shadow.SetPropertyBlock(mpb);
        }

        void FixedUpdate()
        {
            if (!GameManager.IsPlaying) return;
            float dt = Time.fixedDeltaTime;
            t += dt;

            switch (state)
            {
                case S.Moving:
                {
                    if (waypoints == null || waypoints.Length == 0) break;
                    Vector3 target = waypoints[wp];
                    target.y = transform.position.y;
                    Vector3 to = target - transform.position;
                    float step = moveSpeed * dt;
                    if (to.magnitude <= step)
                    {
                        transform.position = target;
                        wp = (wp + 1) % waypoints.Length;
                    }
                    else
                    {
                        transform.position += to.normalized * step;
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), 0.15f);
                    }
                    traveled += step;
                    SetShadow(0.55f, shadowIdle);
                    if (traveled >= stepDistance)
                    {
                        traveled = 0f;
                        state = S.Warning;
                        t = 0f;
                    }
                    break;
                }
                case S.Warning:
                {
                    float k = Mathf.Clamp01(t / warnTime);
                    bool blink = ((int)(t * (k > 0.6f ? 12f : 6f))) % 2 == 0;
                    SetShadow(Mathf.Lerp(0.55f, 1f, k), blink ? shadowWarn : Color.Lerp(shadowIdle, shadowWarn, 0.4f));
                    SetFootHeight(liftHeight + Mathf.Sin(k * Mathf.PI) * 0.15f);
                    if (t >= warnTime)
                    {
                        state = S.Dropping;
                        t = 0f;
                    }
                    break;
                }
                case S.Dropping:
                {
                    float k = Mathf.Clamp01(t / dropTime);
                    SetFootHeight(Mathf.Lerp(liftHeight, 0f, k * k));
                    if (t >= dropTime)
                    {
                        SetFootHeight(0f);
                        Impact();
                        state = S.Planted;
                        t = 0f;
                    }
                    break;
                }
                case S.Planted:
                    SetShadow(1f, shadowIdle);
                    if (t >= plantedTime)
                    {
                        if (footCollider) footCollider.enabled = false;
                        state = S.Lifting;
                        t = 0f;
                    }
                    break;
                case S.Lifting:
                {
                    float k = Mathf.Clamp01(t / liftTime);
                    SetFootHeight(Mathf.Lerp(0f, liftHeight, k));
                    if (t >= liftTime)
                    {
                        state = S.Moving;
                        t = 0f;
                    }
                    break;
                }
            }
        }

        void Impact()
        {
            Vector3 c = transform.position + Vector3.up * footHalfExtents.y;
            int n = Physics.OverlapBoxNonAlloc(c, footHalfExtents, buf, transform.rotation, Layers.JellyMask, QueryTriggerInteraction.Ignore);
            bool leaderStomped = false;
            for (int i = 0; i < n; i++)
            {
                var col = buf[i];
                var ld = col.GetComponent<LeaderController>();
                if (ld)
                {
                    // 실패하지 않는다: 선두는 납작해져 잠시 멈추고, 연결된 동료는 모두 흩어진다
                    if (ld.CanBeStomped)
                    {
                        ld.OnStomped();
                        leaderStomped = true;
                    }
                    continue;
                }
                var u = col.GetComponent<JellyUnit>();
                if (!u || u.State == JellyState.Absorbed) continue;
                if (u.Emote) u.Emote.PlayFlatten(1.2f);
                if (JellyChain.I) JellyChain.I.ReportHit(u, LossType.Scatter, transform.position);
            }
            if (leaderStomped)
            {
                if (JellyChain.I) JellyChain.I.ReportLeaderStomp(transform.position);
                GameManager.Notify("납작! 직원에게 밟혔어요", MsgKind.Warn, 2f);
            }
            if (footCollider) footCollider.enabled = true;
        }

        void OnDrawGizmos()
        {
            if (waypoints == null) return;
            Gizmos.color = Color.red;
            for (int i = 0; i < waypoints.Length; i++)
                Gizmos.DrawLine(waypoints[i], waypoints[(i + 1) % waypoints.Length]);
        }
    }
}
