using UnityEngine;

namespace CM101
{
    /// <summary>
    /// 마지막 1cm 젤리. 첫 100cm 도달 시 한 번만 생성된다.
    /// 현재 길이가 100cm 미만이면 잠금(회색 + 자물쇠), 100cm면 섭취 가능.
    /// </summary>
    public class FinalJelly : MonoBehaviour
    {
        static readonly string[] BodyParts = { "Body", "Head", "EarL", "EarR", "ArmL", "ArmR", "LegL", "LegR", "Belly", "Muzzle" };

        public bool Locked => !JellyChain.I || JellyChain.I.CurrentLength < JellyChain.Goal;

        Transform visual;
        Renderer[] partRenderers;
        Material unlockedMat, lockedMat, bellyMat;
        GameObject lockIcon, ring, pillar;
        bool lastLocked;
        float spawnTime;

        public static FinalJelly Spawn(Vector3 groundPos)
        {
            var go = new GameObject("FinalJelly_1cm");
            go.layer = Layers.Food;
            go.transform.position = groundPos + Vector3.up * 0.22f;
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.3f;
            var fj = go.AddComponent<FinalJelly>();
            fj.Build();
            return fj;
        }

        void Build()
        {
            Color pink = new Color(1f, 0.55f, 0.8f);
            visual = ProtoFactory.BuildBear(transform, 0.4f, pink, false);
            unlockedMat = ProtoFactory.Mat(pink, 0.9f, 0.7f);
            bellyMat = ProtoFactory.Mat(Color.Lerp(pink, Color.white, 0.4f), 0.9f, 0.5f);
            lockedMat = ProtoFactory.Mat(new Color(0.6f, 0.6f, 0.66f), 0.4f);

            var list = new System.Collections.Generic.List<Renderer>();
            foreach (var n in BodyParts)
            {
                var t = visual.Find(n);
                if (t) list.Add(t.GetComponent<Renderer>());
            }
            partRenderers = list.ToArray();

            lockIcon = ProtoFactory.Prim(PrimitiveType.Cube, transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.16f, 0.14f, 0.06f),
                ProtoFactory.Mat(new Color(0.85f, 0.7f, 0.2f), 0.6f), "Lock");
            ProtoFactory.Prim(PrimitiveType.Cylinder, lockIcon.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.6f, 0.4f, 0.4f),
                ProtoFactory.Mat(new Color(0.5f, 0.5f, 0.55f), 0.6f), "Shackle");

            ring = ProtoFactory.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, -0.2f, 0f), new Vector3(0.8f, 0.005f, 0.8f),
                ProtoFactory.Mat(new Color(1f, 0.9f, 0.3f), 0.1f, 1.2f), "Ring");
            ring.SetActive(false);

            // 멀리서도 보이는 빛기둥
            pillar = ProtoFactory.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, 3.5f, 0f), new Vector3(0.08f, 3.2f, 0.08f),
                ProtoFactory.Mat(pink, 0f, 2.5f), "LightPillar");
            pillar.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            spawnTime = Time.time;
            lastLocked = !Locked;
            Refresh();
        }

        void Update()
        {
            if (Locked != lastLocked) Refresh();
            if (!visual) return;
            float age = Time.time - spawnTime;
            float pop = age < 0.5f ? Mathf.SmoothStep(0f, 1f, age / 0.5f) : 1f;
            float bob = Mathf.Sin(Time.time * 3f) * 0.05f;
            visual.localPosition = Vector3.up * (lastLocked ? 0f : bob);
            visual.Rotate(0f, (lastLocked ? 20f : 60f) * Time.deltaTime, 0f, Space.World);
            visual.localScale = Vector3.one * pop;
            if (ring && ring.activeSelf)
            {
                float s = 1f + Mathf.Sin(Time.time * 8f) * 0.08f;
                ring.transform.localScale = new Vector3(0.8f * s, 0.005f, 0.8f * s);
            }
        }

        void Refresh()
        {
            lastLocked = Locked;
            foreach (var r in partRenderers)
            {
                if (!r) continue;
                bool bellyish = r.name == "Belly" || r.name == "Muzzle";
                r.sharedMaterial = lastLocked ? lockedMat : (bellyish ? bellyMat : unlockedMat);
            }
            if (lockIcon) lockIcon.SetActive(lastLocked);
            if (pillar) pillar.SetActive(!lastLocked);
        }

        public void SetHighlighted(bool on)
        {
            if (ring) ring.SetActive(on);
        }
    }
}
