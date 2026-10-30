using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 시작 화면 / 엔딩 화면 (Canvas UI 틀).
/// 이미지는 Inspector의 Sprite 칸에 넣으면 바로 교체되고, 비어 있으면 임시 색 박스와 글자로 표시된다.
/// GameManager가 씬에서 못 찾으면 실행 시 자동으로 붙인다. 이미지를 지정하려면 씬의 GameManager에 미리 붙여둘 것.
/// </summary>
public class GameFlowUI : MonoBehaviour
{
    [Header("공통")]
    public Font font;                     // 비우면 기본 폰트
    public Sprite buttonSprite;           // 버튼 배경 (9-slice 권장)
    public Sprite panelSprite;            // 조작 방법 창 배경 (9-slice 권장)

    [Header("시작 화면")]
    public Sprite titleLogo;              // 비우면 글자 로고
    public Sprite titleBackground;        // 화면 전체 덮는 그림 (비우면 섬이 도는 장면이 보임)

    [Header("엔딩 화면")]
    public Sprite endingBackground;       // 비우면 반투명 보라 화면
    public Sprite endingTitle;            // '탈출 성공!' 그림 (비우면 글자)
    public float endingDelay = 1.2f;      // 다리 끝 도착 → 엔딩 화면까지 (축하 모션 보여줄 시간)

    static readonly Color ButtonColor = new Color(0.55f, 0.42f, 0.85f);
    static readonly Color Ink = new Color(0.32f, 0.22f, 0.5f);

    GameObject titleRoot, controlsPopup, endingRoot;
    Text endingInfo;
    bool endingShown;

    void Start()
    {
        if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildCanvas();
        var gm = GameManager.I;
        titleRoot.SetActive(gm && gm.OnTitle);
    }

    void Update()
    {
        var gm = GameManager.I;
        if (!gm) return;
        if (gm.IsWon && !endingShown)
        {
            endingShown = true;
            StartCoroutine(ShowEnding());
        }
    }

    // ---------------- 버튼 동작 ----------------
    void OnStart()
    {
        titleRoot.SetActive(false);
        if (GameManager.I) GameManager.I.BeginGame();
    }

    void OnControls(bool open) => controlsPopup.SetActive(open);

    void OnPlayAgain() { if (GameManager.I) GameManager.I.Restart(false); }
    void OnToTitle() { if (GameManager.I) GameManager.I.Restart(true); }

    IEnumerator ShowEnding()
    {
        yield return new WaitForSecondsRealtime(endingDelay);
        var gm = GameManager.I;
        float t = gm.RecordTime;
        int cats = gm.player ? gm.player.FollowerCount + 1 : 1;
        endingInfo.text = $"101CM 완성\n기록  {(int)t / 60:00}:{(int)t % 60:00}     고양이  {cats}마리";
        endingRoot.SetActive(true);
        CameraFollow.SetCursorLocked(false);
    }

    // ---------------- 화면 만들기 ----------------
    void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>()) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    void BuildCanvas()
    {
        var cgo = new GameObject("FlowCanvas");
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();
        var root = cgo.transform;

        // ===== 시작 화면 =====
        titleRoot = Panel("Title", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, titleBackground,
            titleBackground ? Color.white : new Color(0f, 0f, 0f, 0f));
        // 아래쪽 은은한 그라데이션 대신 반투명 띠
        Panel("BottomShade", titleRoot.transform, new Vector2(0, 0), new Vector2(1, 0.45f), Vector2.zero, Vector2.zero, null, new Color(0.2f, 0.14f, 0.35f, 0.35f));

        if (titleLogo)
        {
            var logo = Panel("Logo", titleRoot.transform, new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(900, 420), titleLogo, Color.white);
            logo.GetComponent<Image>().preserveAspect = true;
        }
        else
        {
            Label("LogoText", titleRoot.transform, new Vector2(0.5f, 0.70f), new Vector2(1400, 220), "101CM", 170, Color.white, true);
            Label("SubTitle", titleRoot.transform, new Vector2(0.5f, 0.565f), new Vector2(1400, 80), "색을 훔치는 고양이", 54, Color.white, true);
        }
        MakeButton("StartButton", titleRoot.transform, new Vector2(0.5f, 0.34f), new Vector2(380, 96), "시작하기", OnStart);
        MakeButton("ControlsButton", titleRoot.transform, new Vector2(0.5f, 0.22f), new Vector2(380, 96), "조작 방법", () => OnControls(true));

        // 조작 방법 창
        controlsPopup = Panel("ControlsPopup", titleRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, null, new Color(0.2f, 0.14f, 0.35f, 0.55f));
        var box = Panel("Box", controlsPopup.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 760),
            panelSprite, panelSprite ? Color.white : new Color(0.98f, 0.96f, 1f, 0.98f));
        Label("Head", box.transform, new Vector2(0.5f, 0.92f), new Vector2(900, 80), "조작 방법", 46, Ink, false);
        var body = Label("Body", box.transform, new Vector2(0.5f, 0.50f), new Vector2(920, 520), GameManager.ControlsText, 30, new Color(0.3f, 0.25f, 0.4f), false);
        body.alignment = TextAnchor.UpperLeft;
        MakeButton("Close", box.transform, new Vector2(0.5f, 0.08f), new Vector2(300, 80), "닫기", () => OnControls(false));
        controlsPopup.SetActive(false);

        // ===== 엔딩 화면 =====
        endingRoot = Panel("Ending", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, endingBackground,
            endingBackground ? Color.white : new Color(0.25f, 0.18f, 0.4f, 0.55f));
        if (endingTitle)
        {
            var et = Panel("EndingTitle", endingRoot.transform, new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.66f), Vector2.zero, new Vector2(1000, 300), endingTitle, Color.white);
            et.GetComponent<Image>().preserveAspect = true;
        }
        else
        {
            Label("EndingText", endingRoot.transform, new Vector2(0.5f, 0.66f), new Vector2(1400, 180), "탈출 성공!", 120, Color.white, true);
        }
        endingInfo = Label("EndingInfo", endingRoot.transform, new Vector2(0.5f, 0.47f), new Vector2(1400, 160), "", 44, Color.white, true);
        MakeButton("PlayAgain", endingRoot.transform, new Vector2(0.4f, 0.27f), new Vector2(340, 96), "다시 하기", OnPlayAgain);
        MakeButton("ToTitle", endingRoot.transform, new Vector2(0.6f, 0.27f), new Vector2(340, 96), "처음으로", OnToTitle);
        endingRoot.SetActive(false);
    }

    // ---------------- UI 헬퍼 ----------------
    static GameObject Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        if (sprite && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
        return go;
    }

    Text Label(string name, Transform parent, Vector2 anchor, Vector2 size, string text, int fontSize, Color color, bool shadow)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.sizeDelta = size;
        var t = go.GetComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        if (shadow)
        {
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0.15f, 0.1f, 0.25f, 0.6f);
            sh.effectDistance = new Vector2(3, -3);
        }
        return t;
    }

    void MakeButton(string name, Transform parent, Vector2 anchor, Vector2 size, string label, UnityEngine.Events.UnityAction onClick)
    {
        var go = Panel(name, parent, anchor, anchor, Vector2.zero, size, buttonSprite, buttonSprite ? Color.white : ButtonColor);
        var b = go.AddComponent<Button>();
        var cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.2f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.9f);
        cb.colorMultiplier = 1.2f;
        b.colors = cb;
        b.onClick.AddListener(onClick);
        var t = Label("Label", go.transform, new Vector2(0.5f, 0.5f), size, label, 40, Color.white, false);
        ((RectTransform)t.transform).anchorMin = Vector2.zero;
        ((RectTransform)t.transform).anchorMax = Vector2.one;
        ((RectTransform)t.transform).sizeDelta = Vector2.zero;
    }
}
