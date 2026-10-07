using UnityEngine;

namespace CM101
{
    /// <summary>음식 모양(FoodKind)에 맞는 모델을 붙인다. 바닥 음식 = Visual 아래 음식 모델, 동료 = Visual/JellyModel(리깅 젤리 + 머리 위 음식).</summary>
    public static class FoodArt
    {
        static FoodArtLibrary lib;
        static bool tried;
        /// <summary>리깅 동료 모델 키(유닛). 메인 젤리처럼 키 = 몸 크기 × 1.08.</summary>
        public const float FollowerModelHeight = 0.664f;
        public const float FollowerHeightPerSize = 1.08f;

        public static FoodArtLibrary Lib
        {
            get
            {
                if (!lib && !tried) { tried = true; lib = Resources.Load<FoodArtLibrary>("FoodArtLibrary"); }
                return lib;
            }
        }

        public static GameObject FloorPrefab(FoodKind k) { var e = Lib ? Lib.Get(k) : null; return e != null ? e.floorPrefab : null; }
        public static GameObject FollowerPrefab(FoodKind k) { var e = Lib ? Lib.Get(k) : null; return e != null ? e.followerPrefab : null; }

        /// <summary>바닥 음식 모델을 놓을 높이: 모델 바닥이 땅에 닿지 않게, 키가 크면 가운데가 Visual 중심(바닥+0.25)에 오게.</summary>
        public static float FloorOffsetY(GameObject prefab)
        {
            var mf = prefab ? prefab.GetComponentInChildren<MeshFilter>() : null;
            float h = mf && mf.sharedMesh ? mf.sharedMesh.bounds.size.y * prefab.transform.localScale.y : 0.2f;
            return -Mathf.Min(h * 0.5f, 0.2f);
        }

        /// <summary>FoodItem의 Visual 아래 프로토 모양을 지우고 실제 음식 모델을 붙인다(실행 중 생성용).</summary>
        public static bool ApplyFloorVisual(FoodItem f)
        {
            var prefab = FloorPrefab(f.kind);
            if (!prefab) return false;
            var vis = f.transform.Find("Visual");
            if (!vis) { vis = new GameObject("Visual").transform; vis.SetParent(f.transform, false); }
            for (int i = vis.childCount - 1; i >= 0; i--)
            {
                var c = vis.GetChild(i).gameObject;
                if (Application.isPlaying) { c.SetActive(false); Object.Destroy(c); } else Object.DestroyImmediate(c);
            }
            var art = Object.Instantiate(prefab, vis);
            art.name = "Art_" + f.kind;
            art.transform.localPosition = new Vector3(0f, FloorOffsetY(prefab), 0f);
            art.transform.localRotation = Quaternion.identity;
            f.RefreshRefs();
            return true;
        }

        /// <summary>리깅 동료 젤리(머리 위 음식 포함)를 Visual/JellyModel로 만든다. 발바닥 = 몸 구의 바닥. 없으면 null.</summary>
        public static Transform BuildFollower(Transform root, FoodKind k, float size)
        {
            var prefab = FollowerPrefab(k);
            if (!prefab) return null;
            var vis = new GameObject("Visual").transform;
            vis.SetParent(root, false);
            var m = Object.Instantiate(prefab, vis);
            m.name = "JellyModel";
            m.transform.localPosition = new Vector3(0f, -size * 0.5f, 0f);
            m.transform.localRotation = Quaternion.identity;
            m.transform.localScale = Vector3.one * (size * FollowerHeightPerSize / FollowerModelHeight);
            return vis;
        }
    }
}
