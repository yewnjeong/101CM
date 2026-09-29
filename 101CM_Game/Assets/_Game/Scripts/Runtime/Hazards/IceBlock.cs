using UnityEngine;

namespace CM101
{
    /// <summary>
    /// 얼음 덩어리(아이스크림 구역). 선두가 반경 안에 들어오면 2초 동안 얼어 멈춘다.
    /// 한 번 얼리면 이 얼음은 cooldown 동안 쉰다(표시가 흐려짐). 선두는 풀린 뒤 3초 동안 다시 얼지 않는다.
    /// </summary>
    public class IceBlock : MonoBehaviour
    {
        public float radius = 1.3f;
        public float freezeTime = 2f;
        public float cooldown = 5f;
        public Renderer ring;
        public Renderer block;

        float readyAt;
        MaterialPropertyBlock mpb;
        Color ringColor, blockColor;

        public bool Ready => Time.time >= readyAt;

        void Start()
        {
            mpb = new MaterialPropertyBlock();
            if (ring) ringColor = ring.sharedMaterial.GetColor("_BaseColor");
            if (block) blockColor = block.sharedMaterial.GetColor("_BaseColor");
        }

        void FixedUpdate()
        {
            if (!GameManager.IsPlaying || !Ready) return;
            var gm = GameManager.I;
            if (!gm || !gm.leader) return;
            Vector3 p = gm.leader.Body.position;
            Vector3 d = p - transform.position;
            if (Mathf.Abs(d.y) > 1.2f) return;
            d.y = 0f;
            if (d.magnitude > radius) return;
            if (gm.leader.TryFreeze(freezeTime)) readyAt = Time.time + cooldown;
        }

        void Update()
        {
            float k = Ready ? 0.75f + 0.25f * Mathf.Sin(Time.time * 4f) : 0.25f;
            if (ring)
            {
                ring.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", ringColor * k);
                mpb.SetColor("_EmissionColor", ringColor * k);
                ring.SetPropertyBlock(mpb);
            }
            if (block)
            {
                block.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", Ready ? blockColor : blockColor * 0.6f);
                block.SetPropertyBlock(mpb);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.5f, 0.9f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
