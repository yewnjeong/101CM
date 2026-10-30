using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 메뉴 [101CM > Build Prototype In Open Scene]
/// 현재 열려 있는 씬에 컨셉 이미지 기반 마트마을 프로토타입을 생성한다. (다시 실행하면 이전 생성물 교체)
/// 레이아웃: 원형 섬 / 중앙 광장(별 분수) / 좌 빵 가게 / 뒤 과자 가게 / 우 사과 과일 가게 / 좌하 START / 하단 EXIT 다리
/// </summary>
public static class PrototypeSceneBuilder
{
    const string RootDir = "Assets/_101CM";
    const string MatDir = RootDir + "/Materials";
    static readonly string[] GeneratedRoots = { "101CM_Map", "Foods", "Player", "GameManager", "101CM_PostFX" };

    // START 코너: 섬 외곽 울타리 앞-왼쪽 (컨셉 이미지처럼 START/EXIT가 앞쪽 울타리 라인에 나란히)
    const float StartAngle = 215f;           // 도(0 = +Z/북쪽, 시계방향)
    const float EdgeR = 20.8f;
    static Vector3 Radial(float deg, float r) => new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad) * r, 0f, Mathf.Cos(deg * Mathf.Deg2Rad) * r);
    static bool NearStart(float deg, float tol) => Mathf.Abs(Mathf.DeltaAngle(deg, StartAngle)) < tol;

    static Transform foods;
    static readonly List<Vector4> keepClear = new List<Vector4>(); // x, z, radius

    static readonly Color Grass = new Color(0.62f, 0.80f, 0.52f);
    static readonly Color Paving = new Color(0.94f, 0.78f, 0.84f);
    static readonly Color PavingLight = new Color(0.97f, 0.87f, 0.90f);
    static readonly Color Wood = new Color(0.86f, 0.64f, 0.44f);
    static readonly Color WoodDark = new Color(0.55f, 0.38f, 0.28f);
    static readonly Color Cream = new Color(1f, 0.95f, 0.86f);
    static readonly Color WarmLight = new Color(1f, 0.82f, 0.5f);
    static readonly Color Gold = new Color(1f, 0.84f, 0.35f);
    static readonly Color Navy = new Color(0.3f, 0.27f, 0.4f);
    static readonly Color Dark = new Color(0.12f, 0.1f, 0.12f);

    [MenuItem("101CM/Build Prototype In Open Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var go in scene.GetRootGameObjects())
            if (System.Array.IndexOf(GeneratedRoots, go.name) >= 0) Object.DestroyImmediate(go);

        Palette.ClearCache();
        if (AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.DeleteAsset(MatDir);
        Random.InitState(101);
        keepClear.Clear();

        var map = new GameObject("101CM_Map").transform;
        foods = new GameObject("Foods").transform;

        BuildIsland(map);
        BuildPlaza(map);
        BuildBreadShop(map, new Vector3(-13f, 0, 5f));
        BuildSnackShop(map, new Vector3(3f, 0, 15f));
        BuildFruitShop(map, new Vector3(13.5f, 0, 3f));
        BuildStartGate(map);
        var gate = BuildExit(map);
        BuildShoppingCarts(map);
        BuildMagicBroom(map, new Vector3(3f, 0, 15f));
        BuildDecor(map);

        var player = BuildPlayer(Radial(StartAngle, 23.6f) + Vector3.up * 0.05f);
        SetupCameraAndLights(player.transform);

        var gm = new GameObject("GameManager").AddComponent<GameManager>();
        gm.player = player;
        gm.exitGate = gate;
        gm.foodRoot = foods;
        var lp = new GameObject("LastPieceSpawn").transform;
        lp.SetParent(map, false);
        lp.position = new Vector3(0f, 0f, -3.4f);
        gm.lastPieceSpawn = lp;

        SaveMaterials();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[101CM] 프로토타입 생성 완료: " + scene.path);
    }

    // ================= Helpers =================
    static GameObject S(PrimitiveType t, Transform p, Vector3 pos, Vector3 scale, Color c, bool col = false, float em = 0f, Vector3? euler = null, string name = null)
        => Palette.Shape(t, p, pos, scale, c, col, em, euler, name);

    static Transform Group(string name, Transform parent, Vector3 pos, float yaw = 0f)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, false);
        g.localPosition = pos;
        g.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return g;
    }

    /// <summary>로컬 -Z(정면)가 섬 중심을 향하도록 하는 yaw.</summary>
    static float FaceCenterYaw(Vector3 pos)
    {
        Vector3 d = -pos; d.y = 0f;
        return Quaternion.LookRotation(-d.normalized).eulerAngles.y;
    }

    static void Wall(Transform p, Vector3 pos, Vector3 size, float yaw)
    {
        var g = new GameObject("Wall");
        g.transform.SetParent(p, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        g.AddComponent<BoxCollider>().size = size;
    }

    static void Clear(Vector3 p, float r) => keepClear.Add(new Vector4(p.x, p.z, r, 0));

    static bool Near(Vector3 p)
    {
        foreach (var k in keepClear)
            if (new Vector2(p.x - k.x, p.z - k.y).magnitude < k.z) return true;
        return false;
    }

    static void PointLight(Transform p, Vector3 lp, Color c, float range, float intensity, bool enabled = true)
    {
        var l = new GameObject("Light").AddComponent<Light>();
        l.transform.SetParent(p, false);
        l.transform.localPosition = lp;
        l.type = LightType.Point;
        l.color = c;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        l.enabled = enabled;
    }

    static void Awning(Transform p, Vector3 center, float width, Color a, Color b, int stripes)
    {
        var g = Group("Awning", p, center);
        float sw = width / stripes;
        for (int i = 0; i < stripes; i++)
        {
            float x = -width / 2f + sw * (i + 0.5f);
            S(PrimitiveType.Cube, g, new Vector3(x, 0, 0), new Vector3(sw, 0.08f, 1.3f), i % 2 == 0 ? a : b, euler: new Vector3(-20, 0, 0));
            S(PrimitiveType.Sphere, g, new Vector3(x, -0.25f, -0.62f), new Vector3(sw, 0.3f, 0.12f), i % 2 == 0 ? a : b);
        }
    }

    static void Label(Transform p, Vector3 lp, string title, string sub)
    {
        var g = Group("Label", p, lp);
        g.gameObject.AddComponent<Billboard>();
        S(PrimitiveType.Cube, g, Vector3.zero, new Vector3(5.6f, 1.45f, 0.08f), Color.white, em: 0.35f, name: "Panel");
        Palette.Text(g, title, new Vector3(0, 0.25f, -0.06f), 0.5f, new Color(0.3f, 0.22f, 0.45f));
        Palette.Text(g, sub, new Vector3(0, -0.32f, -0.06f), 0.27f, new Color(0.45f, 0.4f, 0.55f));
    }

    static void Table(Transform p, Vector3 lp)
    {
        var t = Group("Table", p, lp);
        S(PrimitiveType.Cube, t, new Vector3(0, 0.38f, 0), new Vector3(1.5f, 0.76f, 0.9f), Wood, col: true);
        S(PrimitiveType.Cube, t, new Vector3(0, 0.78f, 0), new Vector3(1.7f, 0.06f, 1.05f), new Color(1f, 0.97f, 0.92f));
    }

    static void Crate(Transform p, Vector3 lp)
    {
        var t = Group("Crate", p, lp);
        S(PrimitiveType.Cube, t, new Vector3(0, 0.33f, 0), new Vector3(0.9f, 0.66f, 0.9f), Wood, col: true);
        S(PrimitiveType.Cube, t, new Vector3(0, 0.45f, 0), new Vector3(0.92f, 0.08f, 0.92f), WoodDark);
    }

    static void FoodAt(Transform p, FoodType t, Vector3 lp) => Food.Create(t, p.TransformPoint(lp), foods, false);

    static void Path(Transform p, Vector3 a, Vector3 b, float w)
    {
        Vector3 d = b - a; d.y = 0f;
        Vector3 mid = (a + b) / 2f; mid.y = 0.012f;
        S(PrimitiveType.Cube, p, mid, new Vector3(w, 0.02f, d.magnitude), Paving,
            euler: new Vector3(0, Quaternion.LookRotation(d).eulerAngles.y, 0), name: "Path");
    }

    // ================= Island =================
    static void BuildIsland(Transform map)
    {
        var g = Group("Island", map, Vector3.zero);
        S(PrimitiveType.Cylinder, g, new Vector3(0, -1.5f, 0), new Vector3(44, 1.5f, 44), Grass, col: true, name: "Ground");
        S(PrimitiveType.Cylinder, g, new Vector3(0, -2.6f, 0), new Vector3(45.5f, 1.2f, 45.5f), new Color(0.95f, 0.80f, 0.80f), name: "Cliff");
        S(PrimitiveType.Cylinder, g, new Vector3(0, -4.4f, 0), new Vector3(40f, 1.0f, 40f), new Color(0.85f, 0.72f, 0.82f), name: "CliffLow");

        // 투명 외곽 벽 (출구 다리 쪽은 비움)
        var walls = Group("Boundary", g, Vector3.zero);
        const int n = 80; const float r = 21.3f;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            var p = new Vector3(Mathf.Sin(a) * r, 1.5f, Mathf.Cos(a) * r);
            if (p.z < 0 && Mathf.Abs(p.x) < 3.5f) continue;              // EXIT 다리
            if (NearStart(a * Mathf.Rad2Deg, 6.5f)) continue;            // START 선착장
            Wall(walls, p, new Vector3(1.9f, 3f, 0.6f), a * Mathf.Rad2Deg);
        }

        // 울타리 (시각용)
        var fence = Group("Fence", g, Vector3.zero);
        const int fn = 72; const float fr = 20.8f;
        Vector3? prev = null;
        for (int i = 0; i <= fn; i++)
        {
            float a = i * Mathf.PI * 2f / fn;
            var p = new Vector3(Mathf.Sin(a) * fr, 0, Mathf.Cos(a) * fr);
            if ((p.z < 0 && Mathf.Abs(p.x) < 3.4f) || NearStart(a * Mathf.Rad2Deg, 6f)) { prev = null; continue; }
            if (i < fn) S(PrimitiveType.Cube, fence, p + Vector3.up * 0.45f, new Vector3(0.22f, 0.9f, 0.22f), Wood, name: "Post");
            if (prev.HasValue)
            {
                Vector3 mid = (prev.Value + p) / 2f;
                float len = Vector3.Distance(prev.Value, p);
                float yaw = Quaternion.LookRotation(p - prev.Value).eulerAngles.y;
                S(PrimitiveType.Cube, fence, mid + Vector3.up * 0.65f, new Vector3(0.1f, 0.12f, len), Wood, euler: new Vector3(0, yaw, 0), name: "Rail");
                S(PrimitiveType.Cube, fence, mid + Vector3.up * 0.32f, new Vector3(0.1f, 0.12f, len), Wood, euler: new Vector3(0, yaw, 0), name: "Rail");
            }
            prev = p;
        }
    }

    // ================= Plaza =================
    static void BuildPlaza(Transform map)
    {
        var g = Group("Plaza", map, Vector3.zero);
        Clear(Vector3.zero, 12.5f);
        S(PrimitiveType.Cylinder, g, new Vector3(0, 0.005f, 0), new Vector3(21, 0.01f, 21), Paving, name: "Paving");
        S(PrimitiveType.Cylinder, g, new Vector3(0, 0.02f, 0), new Vector3(13, 0.01f, 13), PavingLight, name: "PavingInner");
        Path(g, Vector3.zero, new Vector3(-13, 0, 5), 3.2f);
        Path(g, Vector3.zero, new Vector3(3, 0, 15), 3.2f);
        Path(g, Vector3.zero, new Vector3(13.5f, 0, 3), 3.2f);
        Path(g, Vector3.zero, Radial(StartAngle, EdgeR), 3.2f);
        Path(g, Vector3.zero, new Vector3(0, 0, -21), 4.6f);

        var f = Group("Fountain", g, Vector3.zero);
        S(PrimitiveType.Cylinder, f, new Vector3(0, 0.35f, 0), new Vector3(5f, 0.35f, 5f), new Color(0.96f, 0.88f, 0.93f), col: true, name: "Basin");
        S(PrimitiveType.Cylinder, f, new Vector3(0, 0.71f, 0), new Vector3(4.4f, 0.02f, 4.4f), new Color(0.55f, 0.88f, 0.92f), em: 0.3f, name: "Water");
        S(PrimitiveType.Cylinder, f, new Vector3(0, 1.2f, 0), new Vector3(1.2f, 0.5f, 1.2f), Cream, col: true, name: "Pedestal");
        S(PrimitiveType.Cylinder, f, new Vector3(0, 1.75f, 0), new Vector3(1.8f, 0.08f, 1.8f), new Color(0.85f, 0.95f, 0.97f), name: "Cap");
        var star = Palette.Star(f, new Vector3(0, 3.0f, 0), 1.5f, Gold, 1.8f);
        var sp = star.gameObject.AddComponent<Spin>();
        sp.bobAmplitude = 0.15f;
        PointLight(f, new Vector3(0, 3f, -1f), Gold, 9f, 3f);
        for (int k = 0; k < 6; k++)
        {
            float a = k * Mathf.PI / 3f + 0.5f;
            S(PrimitiveType.Sphere, f, new Vector3(Mathf.Sin(a) * 2.5f, 0.8f, Mathf.Cos(a) * 2.5f), Vector3.one * 0.25f, Gold, em: 1.5f, name: "RimStar");
        }
        for (int k = 0; k < 8; k++)
        {
            float a = (22.5f + 45f * k) * Mathf.Deg2Rad;
            Lamp(g, new Vector3(Mathf.Sin(a) * 10.3f, 0, Mathf.Cos(a) * 10.3f));
        }
    }

    static void Lamp(Transform p, Vector3 pos)
    {
        var g = Group("Lamp", p, pos);
        S(PrimitiveType.Cylinder, g, new Vector3(0, 0.15f, 0), new Vector3(0.45f, 0.15f, 0.45f), Navy, col: true);
        S(PrimitiveType.Cylinder, g, new Vector3(0, 1.5f, 0), new Vector3(0.14f, 1.4f, 0.14f), Navy, col: true);
        S(PrimitiveType.Sphere, g, new Vector3(0, 3.1f, 0), Vector3.one * 0.55f, WarmLight, em: 2.5f);
        S(PrimitiveType.Cube, g, new Vector3(0, 3.45f, 0), new Vector3(0.5f, 0.12f, 0.5f), Navy);
        PointLight(g, new Vector3(0, 3.0f, 0), WarmLight, 6f, 2.5f);
    }

    // ================= Shops =================
    static void BuildBreadShop(Transform map, Vector3 pos)
    {
        var g = Group("BreadShop", map, pos, FaceCenterYaw(pos));
        Clear(pos, 8f);
        var wall = new Color(1f, 0.90f, 0.74f);
        var loaf = new Color(0.93f, 0.63f, 0.33f);
        var red = new Color(0.94f, 0.42f, 0.36f);

        S(PrimitiveType.Cube, g, new Vector3(0, 0.15f, 0), new Vector3(6.3f, 0.3f, 4.7f), new Color(0.85f, 0.62f, 0.45f), name: "Base");
        S(PrimitiveType.Cube, g, new Vector3(0, 1.75f, 0), new Vector3(6f, 3.5f, 4.4f), wall, col: true, name: "Body");
        S(PrimitiveType.Capsule, g, new Vector3(0, 3.9f, 0.2f), new Vector3(2.6f, 3.4f, 2.8f), loaf, euler: new Vector3(0, 0, 90), name: "RoofLoaf");
        S(PrimitiveType.Sphere, g, new Vector3(-2.2f, 4.6f, 0.6f), new Vector3(1.6f, 1.3f, 1.6f), loaf, name: "RoofBun");

        // 식빵 얼굴
        S(PrimitiveType.Sphere, g, new Vector3(0.4f, 4.4f, -1.25f), new Vector3(2.0f, 1.6f, 0.9f), new Color(1f, 0.92f, 0.78f), name: "BunFace");
        for (int s = -1; s <= 1; s += 2)
        {
            S(PrimitiveType.Sphere, g, new Vector3(0.4f + 0.35f * s, 4.55f, -1.68f), new Vector3(0.14f, 0.18f, 0.06f), Dark, name: "Eye");
            S(PrimitiveType.Sphere, g, new Vector3(0.4f + 0.62f * s, 4.25f, -1.62f), new Vector3(0.24f, 0.14f, 0.05f), new Color(1f, 0.6f, 0.65f), name: "Cheek");
        }
        S(PrimitiveType.Cube, g, new Vector3(0.4f, 4.25f, -1.7f), new Vector3(0.3f, 0.06f, 0.04f), Dark, name: "Smile");
        S(PrimitiveType.Cylinder, g, new Vector3(2.1f, 5.3f, 0.8f), new Vector3(0.6f, 0.7f, 0.6f), new Color(0.85f, 0.45f, 0.35f), name: "Chimney");

        S(PrimitiveType.Cube, g, new Vector3(0, 1.05f, -2.22f), new Vector3(1.2f, 2.0f, 0.1f), WoodDark, name: "Door");
        for (int s = -1; s <= 1; s += 2)
            S(PrimitiveType.Cube, g, new Vector3(1.95f * s, 1.6f, -2.22f), new Vector3(1.1f, 1.0f, 0.1f), WarmLight, em: 1.2f, name: "Window");
        Awning(g, new Vector3(0, 2.75f, -2.75f), 6.2f, red, Color.white, 8);
        S(PrimitiveType.Cube, g, new Vector3(0, 3.35f, -2.3f), new Vector3(3.4f, 0.75f, 0.15f), red, name: "Sign");
        Palette.Text(g, "BREAD", new Vector3(0, 3.35f, -2.4f), 0.6f, Color.white);

        Table(g, new Vector3(-1.9f, 0, -4.0f));
        Table(g, new Vector3(1.9f, 0, -4.0f));
        FoodAt(g, FoodType.Toast, new Vector3(-2.35f, 0.8f, -4f));
        FoodAt(g, FoodType.Toast, new Vector3(-1.45f, 0.8f, -4f));
        FoodAt(g, FoodType.Croissant, new Vector3(1.45f, 0.8f, -4f));
        FoodAt(g, FoodType.Croissant, new Vector3(2.35f, 0.8f, -4f));
        Crate(g, new Vector3(-4.0f, 0, -3.0f));
        FoodAt(g, FoodType.Croissant, new Vector3(-4.0f, 0.66f, -3.0f));
        Crate(g, new Vector3(4.0f, 0, -3.0f));
        FoodAt(g, FoodType.Toast, new Vector3(4.0f, 0.66f, -3.0f));
        PointLight(g, new Vector3(0, 2.4f, -3.6f), WarmLight, 6f, 1.5f);
    }

    static void BuildSnackShop(Transform map, Vector3 pos)
    {
        var g = Group("SnackShop", map, pos, FaceCenterYaw(pos));
        Clear(pos, 8f);
        var pink = new Color(0.98f, 0.56f, 0.70f);
        var pinkDark = new Color(0.86f, 0.36f, 0.54f);
        var choco = new Color(0.45f, 0.28f, 0.2f);

        S(PrimitiveType.Cube, g, new Vector3(0, 0.15f, 0), new Vector3(6.9f, 0.3f, 4.9f), new Color(0.9f, 0.7f, 0.75f), name: "Base");
        S(PrimitiveType.Cube, g, new Vector3(0, 1.9f, 0), new Vector3(6.4f, 3.8f, 4.4f), pink, col: true, name: "Body");
        S(PrimitiveType.Cube, g, new Vector3(0, 3.95f, 0), new Vector3(6.9f, 0.4f, 4.9f), pinkDark, name: "Roof");
        for (int i = 0; i < 9; i++)
            S(PrimitiveType.Sphere, g, new Vector3(-3.2f + i * 0.8f, 3.75f, -2.45f), new Vector3(0.75f, 0.45f, 0.2f), Color.white, name: "Scallop");

        // 지붕 위 별 쿠키 / 초코 막대사탕
        var cookieStar = Palette.Star(g, new Vector3(1.4f, 5.7f, 0), 1.9f, new Color(0.96f, 0.76f, 0.42f), 0f);
        S(PrimitiveType.Sphere, cookieStar, new Vector3(0, 0, -0.12f), new Vector3(0.6f, 0.6f, 0.2f), Cream, name: "Icing");
        S(PrimitiveType.Cylinder, g, new Vector3(-2.0f, 4.9f, 0.3f), new Vector3(0.12f, 0.8f, 0.12f), Color.white, name: "Stick");
        S(PrimitiveType.Sphere, g, new Vector3(-2.0f, 6.1f, 0.3f), new Vector3(1.4f, 1.4f, 0.5f), choco, name: "Lollipop");
        Color[] sprinkles = { Color.white, new Color(1f, 0.6f, 0.8f), new Color(0.6f, 0.85f, 1f), new Color(1f, 0.9f, 0.4f) };
        for (int i = 0; i < 6; i++)
        {
            float a = i * 1.05f;
            S(PrimitiveType.Sphere, g, new Vector3(-2.0f + Mathf.Sin(a) * 0.4f, 6.1f + Mathf.Cos(a) * 0.4f, 0.04f), Vector3.one * 0.12f, sprinkles[i % 4], name: "Sprinkle");
        }

        S(PrimitiveType.Cube, g, new Vector3(0, 1.05f, -2.22f), new Vector3(1.2f, 2.0f, 0.1f), WoodDark, name: "Door");
        for (int s = -1; s <= 1; s += 2)
            S(PrimitiveType.Cube, g, new Vector3(2.0f * s, 1.6f, -2.22f), new Vector3(1.2f, 1.0f, 0.1f), WarmLight, em: 1.2f, name: "Window");
        Awning(g, new Vector3(0, 2.8f, -2.75f), 6.6f, pink, Color.white, 9);
        S(PrimitiveType.Cube, g, new Vector3(0, 3.4f, -2.3f), new Vector3(3.4f, 0.75f, 0.15f), Color.white, name: "Sign");
        Palette.Text(g, "SNACK", new Vector3(0, 3.4f, -2.4f), 0.6f, pinkDark);

        Table(g, new Vector3(0, 0, -3.9f));
        FoodAt(g, FoodType.Cookie, new Vector3(-0.45f, 0.8f, -3.9f));
        FoodAt(g, FoodType.Candy, new Vector3(0.45f, 0.8f, -3.9f));
        PointLight(g, new Vector3(0, 2.4f, -3.6f), WarmLight, 6f, 1.5f);
        BoxStack(map, new Vector3(-3.0f, 0, 9.8f), 15f);
        BoxStack(map, new Vector3(8.2f, 0, 10.2f), -25f);
    }

    static void BoxStack(Transform map, Vector3 pos, float yaw)
    {
        var g = Group("SnackBoxStack", map, pos, yaw);
        Clear(pos, 3.5f);
        var boxes = Group("Boxes", g, Vector3.zero);
        Color[] cols =
        {
            new Color(1f, 0.75f, 0.82f), new Color(1f, 0.88f, 0.7f), new Color(0.78f, 0.9f, 1f),
            new Color(0.85f, 0.8f, 1f), new Color(0.8f, 0.95f, 0.82f)
        };
        const float s = 0.85f;
        int[] rows = { 3, 2, 1 };
        int k = 0;
        for (int r = 0; r < rows.Length; r++)
        for (int i = 0; i < rows[r]; i++)
        {
            float x = (i - (rows[r] - 1) / 2f) * (s + 0.05f);
            var b = S(PrimitiveType.Cube, boxes, new Vector3(x, s / 2f + r * s, 0), Vector3.one * s, cols[k++ % cols.Length], col: true,
                euler: new Vector3(0, Random.Range(-8f, 8f), 0), name: "Box");
            S(PrimitiveType.Cube, b.transform, Vector3.zero, new Vector3(1.02f, 0.18f, 1.02f), Color.white, name: "Ribbon");
            S(PrimitiveType.Cube, b.transform, Vector3.zero, new Vector3(0.18f, 1.02f, 1.02f), Color.white, name: "Ribbon");
        }
        var trig = g.gameObject.AddComponent<BoxCollider>();
        trig.isTrigger = true;
        trig.size = new Vector3(3.6f, 2.8f, 1.9f);
        trig.center = new Vector3(0, 1.3f, 0);
        var st = g.gameObject.AddComponent<SnackBoxStack>();
        st.boxesRoot = boxes;
        st.requiredFollowers = 3;
        st.dropCount = 5;
    }

    static void BuildFruitShop(Transform map, Vector3 pos)
    {
        var g = Group("FruitShop", map, pos, FaceCenterYaw(pos));
        Clear(pos, 8f);
        var green = new Color(0.5f, 0.8f, 0.5f);

        S(PrimitiveType.Sphere, g, new Vector3(0, 3.3f, 0), new Vector3(7f, 6.6f, 7f), new Color(0.93f, 0.26f, 0.26f), col: true, name: "BigApple");
        S(PrimitiveType.Sphere, g, new Vector3(-1.4f, 4.8f, -2.4f), new Vector3(1.2f, 0.9f, 0.4f), new Color(1f, 0.6f, 0.6f), name: "Highlight");
        S(PrimitiveType.Cylinder, g, new Vector3(0, 6.9f, 0), new Vector3(0.35f, 0.7f, 0.35f), WoodDark, euler: new Vector3(0, 0, 8), name: "Stem");
        S(PrimitiveType.Sphere, g, new Vector3(1.1f, 7.2f, 0), new Vector3(2.4f, 0.25f, 1.2f), new Color(0.45f, 0.75f, 0.4f), euler: new Vector3(0, 0, 18), name: "Leaf");

        S(PrimitiveType.Cube, g, new Vector3(0, 1.2f, -2.9f), new Vector3(3.4f, 2.4f, 1.2f), Cream, col: true, name: "Front");
        S(PrimitiveType.Cube, g, new Vector3(0, 1.0f, -3.52f), new Vector3(1.1f, 1.8f, 0.06f), WoodDark, name: "Door");
        for (int s = -1; s <= 1; s += 2)
            S(PrimitiveType.Cube, g, new Vector3(1.15f * s, 1.3f, -3.52f), new Vector3(0.7f, 0.8f, 0.06f), WarmLight, em: 1.2f, name: "Window");
        Awning(g, new Vector3(0, 2.55f, -3.9f), 3.8f, green, Color.white, 6);
        S(PrimitiveType.Cube, g, new Vector3(0, 3.2f, -3.62f), new Vector3(2.8f, 0.7f, 0.15f), new Color(0.35f, 0.65f, 0.4f), name: "Sign");
        Palette.Text(g, "FRUIT", new Vector3(0, 3.2f, -3.72f), 0.55f, Color.white);

        Crate(g, new Vector3(-2.6f, 0, -4.4f));
        FoodAt(g, FoodType.Apple, new Vector3(-2.6f, 0.66f, -4.4f));
        Crate(g, new Vector3(2.6f, 0, -4.4f));
        FoodAt(g, FoodType.Orange, new Vector3(2.6f, 0.66f, -4.4f));
        PointLight(g, new Vector3(0, 2.4f, -4.6f), WarmLight, 6f, 1.5f);
        Cart(map, new Vector3(8.0f, 0, -4.0f), 30f);
        Cart(map, new Vector3(13.5f, 0, -7.5f), -20f);
    }

    static void Cart(Transform map, Vector3 pos, float yaw)
    {
        var g = Group("FruitCart", map, pos, yaw);
        Clear(pos, 3.5f);
        var body = Group("Body", g, Vector3.zero);
        S(PrimitiveType.Cube, body, new Vector3(0, 0.75f, 0), new Vector3(1.9f, 0.7f, 1.1f), Wood, col: true, name: "Bed");
        S(PrimitiveType.Cube, body, new Vector3(0, 1.12f, 0), new Vector3(2.0f, 0.08f, 1.2f), WoodDark, name: "Rim");
        for (int sx = -1; sx <= 1; sx += 2)
        for (int sz = -1; sz <= 1; sz += 2)
            S(PrimitiveType.Cylinder, body, new Vector3(0.6f * sx, 0.32f, 0.6f * sz), new Vector3(0.64f, 0.05f, 0.64f), WoodDark, euler: new Vector3(90, 0, 0), name: "Wheel");
        S(PrimitiveType.Cube, body, new Vector3(1.3f, 0.95f, 0), new Vector3(0.8f, 0.08f, 0.08f), WoodDark, euler: new Vector3(0, 0, 15), name: "Handle");
        S(PrimitiveType.Cylinder, body, new Vector3(-0.7f, 1.9f, 0.4f), new Vector3(0.06f, 0.8f, 0.06f), Color.white, name: "Pole");
        S(PrimitiveType.Sphere, body, new Vector3(-0.7f, 2.65f, 0.4f), new Vector3(2.0f, 0.45f, 2.0f), new Color(1f, 0.7f, 0.5f), name: "Umbrella");

        var pile = Group("Pile", body, new Vector3(0, 1.2f, 0));
        for (int i = 0; i < 8; i++)
        {
            var p = new Vector3(Random.Range(-0.7f, 0.7f), Random.Range(0f, 0.2f), Random.Range(-0.35f, 0.35f));
            S(PrimitiveType.Sphere, pile, p, Vector3.one * 0.38f, i % 2 == 0 ? new Color(0.93f, 0.22f, 0.22f) : new Color(1f, 0.6f, 0.15f), name: "Fruit");
        }

        var trig = g.gameObject.AddComponent<BoxCollider>();
        trig.isTrigger = true;
        trig.size = new Vector3(2.8f, 2.2f, 2.0f);
        trig.center = new Vector3(0, 1f, 0);
        var fc = g.gameObject.AddComponent<FruitCart>();
        fc.pile = pile.gameObject;
        fc.body = body;
        fc.requiredFollowers = 6;
        fc.fruitCount = 4;
    }

    // ================= Gates =================
    static void BuildStartGate(Transform map)
    {
        // 게이트는 울타리 라인 위, 정면(-Z)이 섬 바깥(선착장/플레이어 시작 위치)을 향함
        var pos = Radial(StartAngle, EdgeR);
        var g = Group("StartGate", map, pos, StartAngle + 180f);
        Clear(pos, 5f);
        Clear(Radial(StartAngle, 17f), 3f);
        for (int s = -1; s <= 1; s += 2)
        {
            S(PrimitiveType.Cube, g, new Vector3(2.1f * s, 1.7f, 0), new Vector3(0.55f, 3.4f, 0.55f), Wood, col: true, name: "Post");
            S(PrimitiveType.Cube, g, new Vector3(2.1f * s, 3.5f, 0), new Vector3(0.75f, 0.2f, 0.75f), WoodDark, name: "Cap");
            S(PrimitiveType.Sphere, g, new Vector3(2.1f * s, 3.85f, 0), Vector3.one * 0.5f, WarmLight, em: 2f, name: "Lantern");
            Wall(g, new Vector3(2.75f * s, 1.5f, -2.6f), new Vector3(0.5f, 3f, 5.6f), 0f);   // 선착장 옆 벽
        }
        S(PrimitiveType.Cube, g, new Vector3(0, 3.0f, 0), new Vector3(4.6f, 0.85f, 0.25f), Cream, name: "Sign");
        Palette.Text(g, "START", new Vector3(0, 3.0f, -0.16f), 0.7f, new Color(0.85f, 0.4f, 0.3f));
        PointLight(g, new Vector3(0, 2.6f, -1.2f), WarmLight, 6f, 2f);

        // 섬 밖 작은 선착장 (플레이어 시작 지점)
        S(PrimitiveType.Cube, g, new Vector3(0, -0.23f, -2.6f), new Vector3(4.6f, 0.5f, 5.4f), new Color(0.9f, 0.74f, 0.55f), col: true, name: "Landing");
        for (int i = 0; i < 5; i++)
            S(PrimitiveType.Cube, g, new Vector3(0, 0.03f, -0.6f - i), new Vector3(4.6f, 0.01f, 0.08f), WoodDark, name: "Plank");
        for (int s = -1; s <= 1; s += 2)
            for (int i = 0; i < 3; i++)
                S(PrimitiveType.Cube, g, new Vector3(2.2f * s, 0.4f, -1.2f - i * 1.8f), new Vector3(0.2f, 0.8f, 0.2f), Wood, name: "LandingPost");
        Wall(g, new Vector3(0, 1.5f, -5.55f), new Vector3(5.6f, 3f, 0.5f), 0f);
        var star = Palette.Star(g, new Vector3(-2.2f, 1.3f, -5.0f), 0.6f, Gold, 1.6f);
        star.gameObject.AddComponent<Spin>();
    }

    static ExitGate BuildExit(Transform map)
    {
        var g = Group("Exit", map, Vector3.zero);
        Clear(new Vector3(0, 0, -20f), 5f);

        S(PrimitiveType.Cube, g, new Vector3(0, -0.23f, -26.5f), new Vector3(5f, 0.5f, 12f), new Color(0.9f, 0.74f, 0.55f), col: true, name: "Bridge");
        for (int i = 0; i < 12; i++)
            S(PrimitiveType.Cube, g, new Vector3(0, 0.03f, -21f - i), new Vector3(5f, 0.01f, 0.08f), WoodDark, name: "Plank");
        for (int s = -1; s <= 1; s += 2)
        {
            for (int i = 0; i < 6; i++)
                S(PrimitiveType.Cube, g, new Vector3(2.4f * s, 0.45f, -21.5f - i * 2f), new Vector3(0.2f, 0.9f, 0.2f), Wood, name: "BridgePost");
            S(PrimitiveType.Cube, g, new Vector3(2.4f * s, 0.75f, -26.5f), new Vector3(0.12f, 0.12f, 11f), Wood, name: "BridgeRail");
            Wall(g, new Vector3(2.75f * s, 1.5f, -26.5f), new Vector3(0.5f, 3f, 12.5f), 0f);
            Wall(g, new Vector3(4.2f * s, 1.5f, -21.3f), new Vector3(3.2f, 3f, 0.6f), 0f);
        }
        Wall(g, new Vector3(0, 1.5f, -32.8f), new Vector3(6f, 3f, 0.5f), 0f);

        var gate = Group("ExitGate", g, new Vector3(0, 0, -20.9f));
        for (int s = -1; s <= 1; s += 2)
        {
            S(PrimitiveType.Cube, gate, new Vector3(2.3f * s, 1.8f, 0), new Vector3(0.6f, 3.6f, 0.6f), Wood, col: true, name: "Post");
            var st = Palette.Star(gate, new Vector3(2.3f * s, 4.3f, 0), 0.7f, Gold, 1.6f);
            st.gameObject.AddComponent<Spin>();
        }
        var sign = S(PrimitiveType.Cube, gate, new Vector3(0, 3.4f, 0), new Vector3(5.2f, 1.0f, 0.3f), new Color(0.95f, 0.85f, 0.75f), name: "Sign");
        Palette.Text(gate, "EXIT", new Vector3(0, 3.4f, -0.2f), 0.8f, new Color(0.75f, 0.35f, 0.3f));

        var bars = Group("Bars", gate, Vector3.zero);
        var barColor = new Color(0.75f, 0.55f, 0.85f);
        for (int i = -3; i <= 3; i++)
            S(PrimitiveType.Cylinder, bars, new Vector3(i * 0.6f, 1.3f, 0), new Vector3(0.12f, 1.3f, 0.12f), barColor, name: "Bar");
        S(PrimitiveType.Cube, bars, new Vector3(0, 2.2f, 0), new Vector3(4f, 0.15f, 0.15f), barColor, name: "Cross");
        S(PrimitiveType.Cube, bars, new Vector3(0, 0.6f, 0), new Vector3(4f, 0.15f, 0.15f), barColor, name: "Cross");
        var blocker = bars.gameObject.AddComponent<BoxCollider>();
        blocker.size = new Vector3(4.2f, 3f, 0.4f);
        blocker.center = new Vector3(0, 1.5f, 0);

        var light = new GameObject("GateLight").AddComponent<Light>();
        light.transform.SetParent(gate, false);
        light.transform.localPosition = new Vector3(0, 3f, -1.2f);
        light.type = LightType.Point;
        light.color = Gold;
        light.range = 9f;
        light.intensity = 4f;
        light.enabled = false;

        var eg = gate.gameObject.AddComponent<ExitGate>();
        eg.bars = bars;
        eg.blocker = blocker;
        eg.glowRenderers = new[] { sign.GetComponent<Renderer>() };
        eg.gateLight = light;

        var zone = new GameObject("ExitZone");
        zone.transform.SetParent(g, false);
        zone.transform.localPosition = new Vector3(0, 1f, -28.5f);
        var zc = zone.AddComponent<BoxCollider>();
        zc.isTrigger = true;
        zc.size = new Vector3(4.6f, 3f, 3f);
        zone.AddComponent<ExitZone>();
        return eg;
    }

    // ================= Shopping carts (방해 요소) =================
    static void BuildShoppingCarts(Transform map)
    {
        var g = Group("ShoppingCarts", map, Vector3.zero);

        // A: 분수 둘레를 도는 원형 경로 (시계 방향)
        const int n = 20;
        var ring = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            ring[i] = new Vector3(Mathf.Sin(a) * 7f, 0f, Mathf.Cos(a) * 7f);
        }
        CartRoute(g, ring, true);
        Cart(g, "Cart_Ring", ring, true, 3.2f, 0, new Color(1f, 0.55f, 0.7f));

        // B: START·EXIT 앞을 가로지르는 직선 왕복 경로
        var lane = new[] { new Vector3(-9f, 0f, -13f), new Vector3(0f, 0f, -13f), new Vector3(9f, 0f, -13f) };
        CartRoute(g, lane, false);
        Cart(g, "Cart_Lane", lane, false, 4.2f, 0, new Color(0.55f, 0.75f, 1f));
        foreach (var p in lane) Clear(p, 3f);
    }

    // ================= Magic broom (방해 요소) =================
    /// <summary>과자 가게 앞을 왕복하며 쓰는 마법 빗자루. shopPos = 과자 가게 위치.</summary>
    static void BuildMagicBroom(Transform map, Vector3 shopPos)
    {
        Vector3 toCenter = -shopPos; toCenter.y = 0f; toCenter.Normalize();
        Vector3 tangent = new Vector3(-toCenter.z, 0f, toCenter.x);
        Vector3 mid = shopPos + toCenter * 5.6f;
        var route = new[] { mid - tangent * 2.0f, mid + tangent * 0.6f, mid + tangent * 3.2f };  // 왼쪽 과자 상자와 겹치지 않게
        foreach (var q in route) Clear(q, 2.5f);

        var g = Group("MagicBroom", map, Vector3.zero);
        CartRoute(g, route, false, new Color(0.93f, 0.8f, 1f));

        var go = new GameObject("Broom");
        go.transform.SetParent(g, false);
        go.transform.position = route[0];
        go.transform.rotation = Quaternion.LookRotation(route[1] - route[0]);

        var vis = Group("Visual", go.transform, Vector3.zero);
        var visScale = Group("Scale", vis, Vector3.zero);
        visScale.localScale = Vector3.one * 1.4f;
        var wood = new Color(0.62f, 0.42f, 0.3f);
        var straw = new Color(1f, 0.85f, 0.45f);
        var magic = new Color(0.75f, 0.5f, 1f);
        var tilt = new Vector3(-35f, 0f, 0f);
        S(PrimitiveType.Cylinder, visScale, new Vector3(0, 1.25f, 0.35f), new Vector3(0.09f, 0.8f, 0.09f), wood, euler: tilt, name: "Handle");
        S(PrimitiveType.Cylinder, visScale, new Vector3(0, 0.38f, -0.25f), new Vector3(0.62f, 0.3f, 0.34f), straw, euler: tilt, name: "Bristles");
        S(PrimitiveType.Cylinder, visScale, new Vector3(0, 0.62f, -0.08f), new Vector3(0.4f, 0.07f, 0.26f), magic, em: 1.5f, euler: tilt, name: "Band");
        var star = Palette.Star(visScale, new Vector3(0, 1.95f, 0.85f), 0.35f, new Color(1f, 0.84f, 0.35f), 1.8f);
        star.localRotation = Quaternion.Euler(0, 180f, 0);

        var sparkles = Group("Sparkles", go.transform, new Vector3(0, 0.9f, 0));
        Color[] sc = { new Color(0.85f, 0.7f, 1f), new Color(1f, 0.75f, 0.9f), new Color(0.7f, 0.85f, 1f), new Color(1f, 0.92f, 0.6f) };
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 0.5f;
            S(PrimitiveType.Sphere, sparkles, new Vector3(Mathf.Sin(a) * 0.85f, (i % 2) * 0.4f, Mathf.Cos(a) * 0.85f), Vector3.one * 0.14f, sc[i], em: 2.2f, name: "Sparkle");
        }
        PointLight(go.transform, new Vector3(0, 1f, 0), magic, 4f, 1.8f);

        var trig = go.AddComponent<BoxCollider>();
        trig.isTrigger = true;
        trig.center = new Vector3(0, 0.9f, 0);
        trig.size = new Vector3(1.6f, 1.8f, 1.9f);
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        var b = go.AddComponent<MagicBroom>();
        b.waypoints = route;
        b.speed = 2.2f;
        b.pauseAtEnds = 1.2f;
        b.visual = vis;
        b.sparkles = sparkles;
    }

    /// <summary>카트가 다니는 길을 바닥 점선으로 표시.</summary>
    static void CartRoute(Transform p, Vector3[] pts, bool loop) => CartRoute(p, pts, loop, new Color(0.86f, 0.74f, 0.95f));

    static void CartRoute(Transform p, Vector3[] pts, bool loop, Color c)
    {
        var g = Group("Route", p, Vector3.zero);
        int segs = loop ? pts.Length : pts.Length - 1;
        for (int i = 0; i < segs; i++)
        {
            Vector3 a = pts[i], b = pts[(i + 1) % pts.Length];
            int dots = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.9f));
            for (int k = 0; k < dots; k++)
            {
                Vector3 q = Vector3.Lerp(a, b, (k + 0.5f) / dots);
                S(PrimitiveType.Cylinder, g, q + Vector3.up * 0.04f, new Vector3(0.35f, 0.01f, 0.35f), c, name: "Dot");
            }
        }
    }

    static void Cart(Transform p, string name, Vector3[] route, bool loop, float speed, int startIndex, Color accent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        Vector3 start = route[startIndex];
        go.transform.position = start;
        go.transform.rotation = Quaternion.LookRotation(route[(startIndex + 1) % route.Length] - start);

        var vis = Group("Visual", go.transform, Vector3.zero);
        var metal = new Color(0.86f, 0.9f, 0.97f);
        // 바구니
        S(PrimitiveType.Cube, vis, new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.06f, 1.3f), metal, name: "Bottom");
        for (int s = -1; s <= 1; s += 2)
        {
            S(PrimitiveType.Cube, vis, new Vector3(0.45f * s, 0.75f, 0), new Vector3(0.05f, 0.6f, 1.3f), metal, name: "Side");
            S(PrimitiveType.Cube, vis, new Vector3(0.45f * s, 1.15f, -0.78f), new Vector3(0.05f, 0.3f, 0.05f), Navy, name: "HandlePost");
            S(PrimitiveType.Cube, vis, new Vector3(0.4f * s, 0.25f, 0), new Vector3(0.05f, 0.4f, 1.2f), Navy, name: "Frame");
            for (int z = -1; z <= 1; z += 2)
                S(PrimitiveType.Cylinder, vis, new Vector3(0.38f * s, 0.1f, 0.5f * z), new Vector3(0.2f, 0.03f, 0.2f), Navy, euler: new Vector3(0, 0, 90), name: "Wheel");
        }
        S(PrimitiveType.Cube, vis, new Vector3(0, 0.75f, 0.65f), new Vector3(0.9f, 0.6f, 0.05f), metal, name: "Front");
        S(PrimitiveType.Cube, vis, new Vector3(0, 0.75f, -0.65f), new Vector3(0.9f, 0.6f, 0.05f), metal, name: "Back");
        S(PrimitiveType.Cube, vis, new Vector3(0, 1.07f, 0), new Vector3(0.98f, 0.06f, 1.38f), accent, name: "Rim");
        S(PrimitiveType.Cube, vis, new Vector3(0, 1.3f, -0.8f), new Vector3(1.0f, 0.09f, 0.09f), accent, name: "Handle");
        // 장바구니 속 물건
        S(PrimitiveType.Sphere, vis, new Vector3(-0.18f, 0.72f, 0.2f), Vector3.one * 0.38f, new Color(0.93f, 0.25f, 0.25f), name: "Goods");
        S(PrimitiveType.Cube, vis, new Vector3(0.18f, 0.75f, -0.15f), new Vector3(0.32f, 0.45f, 0.25f), new Color(1f, 0.88f, 0.6f), euler: new Vector3(0, 20, 8), name: "Goods");
        S(PrimitiveType.Capsule, vis, new Vector3(0.05f, 0.72f, 0.35f), new Vector3(0.22f, 0.15f, 0.22f), new Color(0.93f, 0.6f, 0.25f), euler: new Vector3(0, 0, 90), name: "Goods");
        // 앞쪽 경고등
        S(PrimitiveType.Sphere, vis, new Vector3(0, 0.75f, 0.7f), new Vector3(0.22f, 0.22f, 0.08f), new Color(1f, 0.45f, 0.4f), em: 1.6f, name: "WarnLight");

        var solid = go.AddComponent<BoxCollider>();
        solid.center = new Vector3(0, 0.65f, 0);
        solid.size = new Vector3(1.0f, 1.3f, 1.5f);
        var trig = go.AddComponent<BoxCollider>();
        trig.isTrigger = true;
        trig.center = new Vector3(0, 0.6f, 0.1f);
        trig.size = new Vector3(1.4f, 1.2f, 1.9f);
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        var sc = go.AddComponent<ShoppingCart>();
        sc.waypoints = route;
        sc.loop = loop;
        sc.speed = speed;
        sc.startIndex = startIndex;
        sc.visual = vis;
    }

    // ================= Decor =================
    static void BuildDecor(Transform map)
    {
        var g = Group("Decor", map, Vector3.zero);

        int idx = 0;
        for (float a = 0; a < 360f; a += 15f)
        {
            float[] radii = { 18.5f, 16f };
            for (int ri = 0; ri < radii.Length; ri++)
            {
                float ang = (a + ri * 7.5f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(ang) * radii[ri], 0, Mathf.Cos(ang) * radii[ri]);
                if (Near(p)) continue;
                if (ri == 1 && Random.value < 0.35f) continue;
                Tree(g, p, Random.Range(0.85f, 1.25f), idx++ % 4 == 0);
            }
        }

        Color[] flowers = { new Color(1f, 0.7f, 0.85f), new Color(1f, 0.93f, 0.55f), Color.white, new Color(0.8f, 0.7f, 1f) };
        for (int i = 0; i < 110; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(11.5f, 20.3f);
            var p = new Vector3(Mathf.Sin(ang) * r, 0.12f, Mathf.Cos(ang) * r);
            if (Near(p)) continue;
            S(PrimitiveType.Sphere, g, p, Vector3.one * Random.Range(0.18f, 0.3f), flowers[i % flowers.Length], em: 0.15f, name: "Flower");
        }

        // 섬 주변 구름
        var clouds = Group("Clouds", g, Vector3.zero);
        for (int i = 0; i < 22; i++)
        {
            float ang = (i * 360f / 22f + Random.Range(-6f, 6f)) * Mathf.Deg2Rad;
            float r = Random.Range(24f, 31f);
            var c = Group("Cloud", clouds, new Vector3(Mathf.Sin(ang) * r, Random.Range(-3.5f, -0.8f), Mathf.Cos(ang) * r));
            int puffs = Random.Range(3, 6);
            Color cc = i % 3 == 0 ? new Color(1f, 0.9f, 0.96f) : new Color(1f, 0.98f, 1f);
            for (int k = 0; k < puffs; k++)
                S(PrimitiveType.Sphere, c, new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(-0.5f, 0.8f), Random.Range(-2.5f, 2.5f)),
                    Vector3.one * Random.Range(3f, 5.5f), cc, em: 0.25f, name: "Puff");
        }

        // 하늘의 작은 별
        for (int i = 0; i < 10; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            var st = Palette.Star(g, new Vector3(Mathf.Sin(ang) * 30f, Random.Range(6f, 14f), Mathf.Cos(ang) * 30f), Random.Range(0.8f, 1.5f), Gold, 2f);
            st.gameObject.AddComponent<Spin>().bobAmplitude = 0.4f;
        }
    }

    static void Tree(Transform p, Vector3 pos, float s, bool blossom)
    {
        var g = Group("Tree", p, pos, Random.Range(0f, 360f));
        S(PrimitiveType.Cylinder, g, new Vector3(0, 0.6f * s, 0), new Vector3(0.35f, 0.6f, 0.35f) * s, new Color(0.62f, 0.45f, 0.35f), col: true, name: "Trunk");
        Color c1 = blossom ? new Color(1f, 0.78f, 0.88f) : new Color(0.45f, 0.72f, 0.45f);
        Color c2 = blossom ? new Color(1f, 0.86f, 0.93f) : new Color(0.55f, 0.80f, 0.50f);
        S(PrimitiveType.Sphere, g, new Vector3(0, 1.7f, 0) * s, Vector3.one * 1.8f * s, c1, name: "Leaves");
        S(PrimitiveType.Sphere, g, new Vector3(0.35f, 2.5f, 0.1f) * s, Vector3.one * 1.35f * s, c2, name: "Leaves");
        S(PrimitiveType.Sphere, g, new Vector3(-0.1f, 3.1f, -0.1f) * s, Vector3.one * 0.9f * s, c1, name: "Leaves");
    }

    // ================= Player / Camera =================
    static CatPlayer BuildPlayer(Vector3 pos)
    {
        var go = new GameObject("Player");
        go.transform.position = pos;
        go.layer = 2;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        var cc = go.AddComponent<CapsuleCollider>();
        cc.center = new Vector3(0, 0.5f, 0);
        cc.radius = 0.35f;
        cc.height = 1.0f;
        var vis = CatVisual.Build(go.transform, Palette.CatBlack, 1f);
        vis.yaw = StartAngle - 180f;
        vis.transform.localRotation = Quaternion.Euler(0, StartAngle - 180f, 0);
        var cp = go.AddComponent<CatPlayer>();
        cp.visual = vis;
        return cp;
    }

    static void SetupCameraAndLights(Transform player)
    {
        var cam = Camera.main;
        if (!cam)
        {
            cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.42f, 0.36f, 0.66f);
        cam.farClipPlane = 250f;
        cam.fieldOfView = 40f;
        var follow = cam.GetComponent<CameraFollow>();
        if (!follow) follow = cam.gameObject.AddComponent<CameraFollow>();
        follow.target = player;
        follow.pitch = 40f;
        follow.yaw = StartAngle - 180f;
        follow.distance = 11f;
        var rot = Quaternion.Euler(40f, StartAngle - 180f, 0f);
        cam.transform.SetPositionAndRotation(player.position + Vector3.up * 0.8f - rot * Vector3.forward * 11f, rot);
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        Light sun = null;
        foreach (var l in Object.FindObjectsByType<Light>())
            if (l.type == LightType.Directional) { sun = l; break; }
        if (!sun)
        {
            sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
        sun.color = new Color(1f, 0.94f, 0.88f);
        sun.intensity = 1.25f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.6f;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.78f, 0.72f, 0.95f);
        RenderSettings.ambientEquatorColor = new Color(0.88f, 0.78f, 0.88f);
        RenderSettings.ambientGroundColor = new Color(0.6f, 0.52f, 0.62f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = cam.backgroundColor;
        RenderSettings.fogStartDistance = 45f;
        RenderSettings.fogEndDistance = 120f;

        // 몽환적인 글로우용 포스트 프로세싱
        EnsureFolder(RootDir);
        string profilePath = RootDir + "/PP_101CM.asset";
        AssetDatabase.DeleteAsset(profilePath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, profilePath);
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.6f);
        bloom.threshold.Override(1.05f);
        bloom.scatter.Override(0.7f);
        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);
        var ca = profile.Add<ColorAdjustments>(true);
        ca.saturation.Override(8f);
        ca.postExposure.Override(0f);
        var vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.22f);
        vig.color.Override(new Color(0.3f, 0.2f, 0.45f));
        foreach (var c in profile.components)
        {
            c.name = c.GetType().Name;
            AssetDatabase.AddObjectToAsset(c, profile);
        }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var vol = new GameObject("101CM_PostFX").AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 10f;
        vol.sharedProfile = profile;
    }

    // ================= Assets =================
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }

    static void SaveMaterials()
    {
        EnsureFolder(MatDir);
        foreach (var m in new List<Material>(Palette.CachedMaterials))
        {
            if (m == null || AssetDatabase.Contains(m)) continue;
            AssetDatabase.CreateAsset(m, $"{MatDir}/{m.name}.mat");
        }
        AssetDatabase.SaveAssets();
    }
}
