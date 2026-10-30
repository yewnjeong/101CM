using System.Collections;
using UnityEngine;

public enum FoodType { Toast, Croissant, Cookie, Candy, Apple, Orange, LastPiece }

/// <summary>
/// 수집 음식. physical=false: 진열대 위에 떠서 회전 / physical=true: 굴러다니는 음식(상자·수레에서 쏟아짐).
/// </summary>
public class Food : MonoBehaviour
{
    public FoodType type;
    public int cm = 5;
    public bool physical;

    public static readonly System.Collections.Generic.List<Food> All = new System.Collections.Generic.List<Food>();

    Transform model;
    bool eaten, highlighted;
    float highlightScale = 1f;
    float phase, spawnTime;
    static PhysicsMaterial bouncy;

    public static int DefaultCm(FoodType t)
    {
        switch (t)
        {
            case FoodType.Toast:
            case FoodType.Croissant: return 5;
            case FoodType.Cookie:
            case FoodType.Candy: return 4;
            case FoodType.Apple:
            case FoodType.Orange: return 6;
            case FoodType.LastPiece: return 1;
        }
        return 3;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public bool CanBeEaten => !eaten && !(physical && Time.time - spawnTime < 0.4f);
    public Vector3 FocusPoint => model ? model.position : transform.position;

    /// <summary>작게 시작해서 톡 커지며 나타남 (흩어진 고양이가 음식으로 돌아갈 때).</summary>
    public void PopIn() => highlightScale = 0f;

    /// <summary>먹을 수 있는 거리 안에 들어오면 살짝 커지며 강조.</summary>
    public void SetHighlighted(bool on) => highlighted = on;

    void LateUpdate()
    {
        if (eaten || !model) return;
        float target = highlighted ? 1.3f + Mathf.Sin(Time.time * 8f) * 0.06f : 1f;
        highlightScale = Mathf.MoveTowards(highlightScale, target, Time.deltaTime * 3f);
        model.localScale = Vector3.one * highlightScale;
    }

    void Awake()
    {
        spawnTime = Time.time;
        phase = Random.value * 10f;
    }

    public static Food Create(FoodType t, Vector3 worldPos, Transform parent = null, bool physical = false)
    {
        var go = new GameObject(t.ToString());
        if (parent) go.transform.SetParent(parent, true);
        go.transform.position = worldPos;

        var f = go.AddComponent<Food>();
        f.type = t;
        f.cm = DefaultCm(t);
        f.physical = physical;

        var model = new GameObject("Model").transform;
        model.SetParent(go.transform, false);
        float radius = BuildModel(t, model);
        f.model = model;

        if (physical)
        {
            if (bouncy == null) bouncy = new PhysicsMaterial("FoodBounce") { bounciness = 0.25f, dynamicFriction = 0.4f, staticFriction = 0.4f };
            var sc = go.AddComponent<SphereCollider>();
            sc.radius = radius;
            sc.sharedMaterial = bouncy;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            rb.linearDamping = 0.15f;
            rb.angularDamping = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var pick = new GameObject("Pickup");
            pick.transform.SetParent(go.transform, false);
            var tc = pick.AddComponent<SphereCollider>();
            tc.isTrigger = true;
            tc.radius = radius + 0.3f;
        }
        else
        {
            model.localPosition = new Vector3(0f, 0.35f, 0f);
            var tc = go.AddComponent<SphereCollider>();
            tc.isTrigger = true;
            tc.radius = 0.9f;
            tc.center = new Vector3(0f, 0.3f, 0f);
        }

        // (마지막 1CM 주변 빛 조명은 제거)
        return f;
    }

    static float BuildModel(FoodType t, Transform m)
    {
        var dark = new Color(0.35f, 0.2f, 0.12f);
        var green = new Color(0.45f, 0.78f, 0.4f);
        switch (t)
        {
            case FoodType.Toast:
            {
                var c = new Color(0.97f, 0.80f, 0.52f);
                Palette.Shape(PrimitiveType.Cube, m, new Vector3(0, -0.05f, 0), new Vector3(0.5f, 0.38f, 0.22f), c);
                Palette.Shape(PrimitiveType.Sphere, m, new Vector3(0, 0.13f, 0), new Vector3(0.52f, 0.3f, 0.22f), c);
                Palette.Shape(PrimitiveType.Cube, m, new Vector3(0, 0f, -0.115f), new Vector3(0.4f, 0.3f, 0.02f), new Color(1f, 0.95f, 0.82f));
                return 0.28f;
            }
            case FoodType.Croissant:
            {
                var c = new Color(0.93f, 0.60f, 0.25f);
                Palette.Shape(PrimitiveType.Capsule, m, Vector3.zero, new Vector3(0.26f, 0.2f, 0.26f), c, euler: new Vector3(0, 0, 90));
                Palette.Shape(PrimitiveType.Capsule, m, new Vector3(-0.2f, -0.03f, 0.08f), new Vector3(0.17f, 0.14f, 0.17f), c, euler: new Vector3(0, 30, 70));
                Palette.Shape(PrimitiveType.Capsule, m, new Vector3(0.2f, -0.03f, 0.08f), new Vector3(0.17f, 0.14f, 0.17f), c, euler: new Vector3(0, -30, -70));
                return 0.25f;
            }
            case FoodType.Cookie:
            {
                Palette.Shape(PrimitiveType.Cylinder, m, Vector3.zero, new Vector3(0.46f, 0.04f, 0.46f), new Color(0.85f, 0.62f, 0.38f), euler: new Vector3(90, 0, 0));
                Vector3[] chips = { new Vector3(-0.1f, 0.08f, -0.05f), new Vector3(0.09f, 0.1f, -0.05f), new Vector3(0.05f, -0.1f, -0.05f), new Vector3(-0.08f, -0.06f, -0.05f) };
                foreach (var p in chips) Palette.Shape(PrimitiveType.Sphere, m, p, Vector3.one * 0.07f, dark);
                return 0.24f;
            }
            case FoodType.Candy:
            {
                Palette.Shape(PrimitiveType.Sphere, m, Vector3.zero, Vector3.one * 0.32f, new Color(1f, 0.55f, 0.75f));
                for (int s = -1; s <= 1; s += 2)
                    Palette.Shape(PrimitiveType.Cube, m, new Vector3(0.22f * s, 0, 0), new Vector3(0.14f, 0.2f, 0.05f), new Color(1f, 0.85f, 0.95f), euler: new Vector3(0, 0, 45));
                return 0.2f;
            }
            case FoodType.Apple:
            {
                Palette.Shape(PrimitiveType.Sphere, m, Vector3.zero, new Vector3(0.44f, 0.42f, 0.44f), new Color(0.93f, 0.22f, 0.22f));
                Palette.Shape(PrimitiveType.Cylinder, m, new Vector3(0, 0.24f, 0), new Vector3(0.04f, 0.06f, 0.04f), dark);
                Palette.Shape(PrimitiveType.Sphere, m, new Vector3(0.07f, 0.25f, 0), new Vector3(0.14f, 0.04f, 0.08f), green, euler: new Vector3(0, 0, 25));
                return 0.22f;
            }
            case FoodType.Orange:
            {
                Palette.Shape(PrimitiveType.Sphere, m, Vector3.zero, Vector3.one * 0.4f, new Color(1f, 0.6f, 0.15f));
                Palette.Shape(PrimitiveType.Sphere, m, new Vector3(0.05f, 0.21f, 0), new Vector3(0.12f, 0.04f, 0.07f), green, euler: new Vector3(0, 0, 20));
                return 0.2f;
            }
            case FoodType.LastPiece:
            {
                // 무지개 젤리 고양이 (Resources/RainbowKittenModel). 없으면 예전 금색 별
                var rainbow = Resources.Load<GameObject>("RainbowKittenModel");
                if (rainbow)
                {
                    var g = Object.Instantiate(rainbow, m, false);
                    g.transform.localPosition = new Vector3(0f, -0.3f, 0f);
                    g.transform.localScale = Vector3.one * 0.75f;
                }
                else Palette.Star(m, Vector3.zero, 0.8f, new Color(1f, 0.84f, 0.3f), 1.8f);
                Palette.Text(m, "1CM", new Vector3(0, 0.75f, 0), 0.35f, new Color(1f, 0.95f, 0.7f));
                return 0.35f;
            }
        }
        return 0.25f;
    }

    void Update()
    {
        if (eaten || physical || !model) return;
        phase += Time.deltaTime;
        model.localPosition = new Vector3(0f, 0.35f + Mathf.Sin(phase * 2.5f) * 0.08f, 0f);
        model.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
    }

    public void Eat(CatPlayer by)
    {
        if (eaten) return;
        if (GameManager.I && !GameManager.I.CanEat) return;
        if (physical && Time.time - spawnTime < 0.4f) return; // 쏟아지는 순간 바로 먹히지 않게
        eaten = true;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        var rb = GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;
        if (GameManager.I) GameManager.I.OnFoodEaten(this, by);
        StartCoroutine(EatRoutine(by.transform));
    }

    IEnumerator EatRoutine(Transform target)
    {
        Vector3 start = transform.position, s0 = transform.localScale;
        float t = 0f;
        while (t < 0.22f)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / 0.22f);
            transform.position = Vector3.Lerp(start, target.position + Vector3.up * 0.6f, n);
            transform.localScale = s0 * (1f - n);
            yield return null;
        }
        Destroy(gameObject);
    }
}
