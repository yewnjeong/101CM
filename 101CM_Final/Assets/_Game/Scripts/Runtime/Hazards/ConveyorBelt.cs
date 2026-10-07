using UnityEngine;

namespace CM101
{
    /// <summary>
    /// 계산대 바닥 벨트. 위에 있는 선두·젤리를 한 방향으로 밀고, 벨트 위 음식을 실어 나른다(끝에 닿으면 처음으로 돌아감).
    /// 손실은 없고, 움직이는 음식을 붙잡는 조작 난도를 만든다.
    /// </summary>
    public class ConveyorBelt : MonoBehaviour
    {
        public Vector2 size = new Vector2(1.6f, 5.5f); // x = 폭, y = 길이(로컬 z)
        public float speed = 1.3f;                    // 로컬 +z 방향
        public float foodSpeedFactor = 0.8f;
        public Transform[] stripes;

        readonly Collider[] buf = new Collider[32];

        void FixedUpdate()
        {
            if (!GameManager.IsPlaying) return;
            float dt = Time.fixedDeltaTime;
            Vector3 v = transform.forward * speed;

            Vector3 c = transform.position + Vector3.up * 0.4f;
            Vector3 half = new Vector3(size.x * 0.5f, 0.4f, size.y * 0.5f);
            int n = Physics.OverlapBoxNonAlloc(c, half, buf, transform.rotation, Layers.JellyMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var ld = buf[i].GetComponent<LeaderController>();
                if (ld) { ld.ExternalVelocity += v; continue; }
                var u = buf[i].GetComponent<JellyUnit>();
                if (u && u.State != JellyState.Absorbed) u.ExternalVelocity = v;
            }

            float halfW = size.x * 0.5f, halfL = size.y * 0.5f;
            foreach (var f in FoodItem.All)
            {
                if (!f || f.Consumed) continue;
                Vector3 lp = transform.InverseTransformPoint(f.transform.position);
                if (Mathf.Abs(lp.x) > halfW || Mathf.Abs(lp.z) > halfL + 0.01f || lp.y > 0.8f || lp.y < -0.2f) continue;
                lp.z += speed * foodSpeedFactor * dt;
                if (lp.z > halfL) lp.z -= size.y;
                f.transform.position = transform.TransformPoint(lp);
            }
        }

        void Update()
        {
            if (stripes == null || stripes.Length == 0) return;
            float L = size.y;
            float spacing = L / stripes.Length;
            float off = Mathf.Repeat(Time.time * speed, spacing);
            for (int i = 0; i < stripes.Length; i++)
            {
                if (!stripes[i]) continue;
                var p = stripes[i].localPosition;
                p.z = -L * 0.5f + Mathf.Repeat(i * spacing + off, L);
                stripes[i].localPosition = p;
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.up * 0.2f, new Vector3(size.x, 0.4f, size.y));
            Gizmos.DrawLine(Vector3.up * 0.3f, Vector3.up * 0.3f + Vector3.forward * 1f);
        }
    }
}
