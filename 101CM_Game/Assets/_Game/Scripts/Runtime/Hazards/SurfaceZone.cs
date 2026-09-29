using System.Collections.Generic;
using UnityEngine;

namespace CM101
{
    public enum SurfaceKind { None, Slippery, Sticky }

    /// <summary>
    /// 바닥 효과 영역. 손실은 없고 움직임을 방해한다.
    /// - Slippery (쏟은 음료): 가속이 크게 줄어 미끄러진다. 멈추거나 방향을 바꾸기 어렵다.
    /// - Sticky (녹은 아이스크림): 느려지고 점프가 낮아진다.
    /// 선두가 이 상자 안 바닥에 있을 때만 적용된다(매대 위에서는 적용 안 됨).
    /// </summary>
    public class SurfaceZone : MonoBehaviour
    {
        public static readonly List<SurfaceZone> All = new List<SurfaceZone>();

        public SurfaceKind kind = SurfaceKind.Slippery;
        public Vector3 size = new Vector3(3f, 0.6f, 3f);

        [Header("Slippery")]
        public float slipAccel = 0.1f;       // 가속 배율 (출발·멈춤·방향 전환이 모두 느려져 미끄러진다)
        public float slipSpeed = 1.25f;
        [Header("Sticky")]
        public float stickySpeed = 0.45f;
        public float stickyJump = 0.55f;

        public Renderer marker;
        MaterialPropertyBlock mpb;
        Color baseColor;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            mpb = new MaterialPropertyBlock();
            if (marker) baseColor = marker.sharedMaterial.GetColor("_BaseColor");
        }

        public bool Contains(Vector3 p)
        {
            Vector3 c = transform.position;
            return Mathf.Abs(p.x - c.x) <= size.x * 0.5f && Mathf.Abs(p.z - c.z) <= size.z * 0.5f && p.y >= c.y - 0.2f && p.y <= c.y + size.y;
        }

        public static void Sample(Vector3 p, bool grounded, out float speedMul, out float accelMul, out float jumpMul, out SurfaceKind kind)
        {
            speedMul = 1f;
            accelMul = 1f;
            jumpMul = 1f;
            kind = SurfaceKind.None;
            foreach (var z in All)
            {
                if (!z || !z.Contains(p)) continue;
                if (z.kind == SurfaceKind.Slippery)
                {
                    accelMul = Mathf.Min(accelMul, z.slipAccel);
                    speedMul = Mathf.Max(speedMul, z.slipSpeed);
                    if (kind == SurfaceKind.None) kind = SurfaceKind.Slippery;
                }
                else if (grounded)
                {
                    speedMul = Mathf.Min(speedMul, z.stickySpeed);
                    jumpMul = Mathf.Min(jumpMul, z.stickyJump);
                    kind = SurfaceKind.Sticky;
                }
            }
        }

        void Update()
        {
            if (!marker) return;
            float k = kind == SurfaceKind.Slippery ? 0.85f + 0.15f * Mathf.Sin(Time.time * 2.5f) : 0.9f + 0.1f * Mathf.Sin(Time.time * 1.3f);
            marker.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", baseColor * k);
            marker.SetPropertyBlock(mpb);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = kind == SurfaceKind.Slippery ? new Color(0.3f, 0.7f, 1f, 0.8f) : new Color(1f, 0.6f, 0.8f, 0.8f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * size.y * 0.5f, size);
        }
    }
}
