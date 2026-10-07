using UnityEngine;

namespace CM101
{
    /// <summary>
    /// 무너지는 행사 매대(기획서 07장). 선두가 구역에 들어오면 흔들림 + 바닥 표시로 0.5초 예고 후 (2026-09-29: 1초 → 0.5초, 긴장감)
    /// 상품이 정해진 방향으로 떨어진다. 유효한 낙하 충격만 '덩어리 분리'를 만든다.
    /// 테스트 반복을 위해 일정 시간 뒤 원위치로 복구된다.
    /// </summary>
    public class FallingStack : MonoBehaviour
    {
        public Rigidbody[] items;
        public Vector3 pushDirection = Vector3.left;
        public float pushSpeed = 3f;
        public Vector3 zoneCenter;
        public Vector3 zoneSize = new Vector3(4f, 2f, 6f);
        public float warnTime = 0.3f; // 2026-10-07: 0.5초 → 0.3초 (사용자)
        public float liveTime = 1.6f;
        public float resetAfter = 8f;
        public float rearmCooldown = 3f;
        public Renderer landingMarker;

        enum S { Idle, Warning, Falling, Cooldown }

        S state;
        float t;
        Vector3[] homePos;
        Quaternion[] homeRot;

        public bool IsLive => state == S.Falling && t < liveTime;

        void Start()
        {
            homePos = new Vector3[items.Length];
            homeRot = new Quaternion[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                homePos[i] = it.transform.position;
                homeRot[i] = it.transform.rotation;
                it.isKinematic = true;
                var fi = it.GetComponent<FallingItem>();
                if (!fi) fi = it.gameObject.AddComponent<FallingItem>();
                fi.stack = this;
            }
            if (landingMarker) landingMarker.enabled = false;
        }

        bool LeaderInZone()
        {
            var gm = GameManager.I;
            if (!gm || !gm.leader) return false;
            return new Bounds(zoneCenter, zoneSize).Contains(gm.leader.Body.position);
        }

        void Update()
        {
            if (!GameManager.IsPlaying && state != S.Falling) return;
            t += Time.deltaTime;

            switch (state)
            {
                case S.Idle:
                    if (LeaderInZone())
                    {
                        state = S.Warning;
                        t = 0f;
                    }
                    break;

                case S.Warning:
                    for (int i = 0; i < items.Length; i++)
                    {
                        // 예고가 짧은 대신 더 세게, 갈수록 심하게 떤다
                        float k = 0.6f + 0.8f * Mathf.Clamp01(t / warnTime);
                        items[i].transform.SetPositionAndRotation(homePos[i] + Random.insideUnitSphere * 0.05f * k,
                            homeRot[i] * Quaternion.Euler(Random.Range(-7f, 7f) * k, 0f, Random.Range(-7f, 7f) * k));
                    }
                    if (landingMarker) landingMarker.enabled = ((int)(t * 14f)) % 2 == 0;
                    if (t >= warnTime)
                    {
                        state = S.Falling;
                        t = 0f;
                        if (landingMarker) landingMarker.enabled = false;
                        Vector3 dir = pushDirection.normalized;
                        for (int i = 0; i < items.Length; i++)
                        {
                            var it = items[i];
                            it.transform.SetPositionAndRotation(homePos[i], homeRot[i]);
                            it.isKinematic = false;
                            it.linearVelocity = dir * pushSpeed * Random.Range(0.8f, 1.25f) + Random.insideUnitSphere * 0.5f;
                            it.angularVelocity = Random.insideUnitSphere * 3f;
                        }
                    }
                    break;

                case S.Falling:
                    if (t >= resetAfter)
                    {
                        for (int i = 0; i < items.Length; i++)
                        {
                            items[i].isKinematic = true;
                            items[i].transform.SetPositionAndRotation(homePos[i], homeRot[i]);
                        }
                        state = S.Cooldown;
                        t = 0f;
                    }
                    break;

                case S.Cooldown:
                    if (t >= rearmCooldown && !LeaderInZone()) state = S.Idle;
                    break;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);
            Gizmos.DrawWireCube(zoneCenter, zoneSize);
        }
    }

    public class FallingItem : MonoBehaviour
    {
        public FallingStack stack;
        public float minImpactSpeed = 1.0f;

        /// <summary>
        /// 떨어지는 상품이 동료 위로 떨어졌을 때만 맞은 것으로 친다(바닥을 미끄러지며 스친 것은 제외).
        /// 맞은 동료부터 꼬리까지만 흩어지고, 그 앞쪽은 연결을 유지한다.
        /// </summary>
        void OnCollisionEnter(Collision c)
        {
            if (!stack || !stack.IsLive) return;
            if (c.relativeVelocity.magnitude < minImpactSpeed) return;
            var u = c.collider.GetComponent<JellyUnit>();
            if (!u || !JellyChain.I) return;
            bool fromAbove = transform.position.y > u.Body.position.y + u.Radius * 0.3f;
            if (!fromAbove) return;
            JellyChain.I.ReportHit(u, LossType.FallScatter, transform.position);
        }
    }
}
