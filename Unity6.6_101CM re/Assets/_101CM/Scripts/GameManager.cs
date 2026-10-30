using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// CM 집계, 100CM → 마지막 1CM 등장, 101CM → 출구 개방, 클리어.
/// HUD(OnGUI): 상단 CM 진행 바만 표시. 안내 문구는 모두 Esc 메뉴로 이동.
/// Esc 메뉴: 계속하기 / 다시 시작 / 조작 키 / 게임 안내(상자·수레·카트·빗자루)
/// 시작 화면·엔딩 화면은 GameFlowUI(Canvas)가 담당.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }
    public const int Goal = 101;

    public CatPlayer player;
    public ExitGate exitGate;
    public Transform lastPieceSpawn;
    public Transform foodRoot;

    /// <summary>다시 시작 시 시작 화면을 건너뛰고 바로 플레이 (씬 다시 불러와도 유지되도록 static).</summary>
    static bool skipTitle;

    int rawCm, lastStage;
    bool lastSpawned, lastCollected, won, menuOpen, onTitle;
    int menuTab;   // 0 = 조작 키, 1 = 게임 안내
    float startTime, endTime;

    public int CM => lastCollected ? Goal : Mathf.Min(rawCm, Goal - 1);
    public float Progress => CM / (float)Goal;
    public bool CanEat => !won && !menuOpen && !onTitle;
    public bool IsWon => won;
    public bool IsPaused => menuOpen || onTitle;
    public bool OnTitle => onTitle;
    /// <summary>플레이 기록 시간(초). Esc 메뉴로 멈춘 시간은 제외.</summary>
    public float RecordTime => (won ? endTime : Time.time) - startTime;

    void Awake()
    {
        I = this;
        Time.timeScale = 1f;
        onTitle = !skipTitle;
        skipTitle = false;
        // 씬에 GameFlowUI가 없으면 자동으로 붙임 (이미지 지정하려면 씬의 GameManager에 미리 붙여두기)
        if (!FindAnyObjectByType<GameFlowUI>()) gameObject.AddComponent<GameFlowUI>();
    }

    void OnDestroy()
    {
        if (I == this) Time.timeScale = 1f;
    }

    void Start()
    {
        startTime = Time.time;
        if (player) player.SetProgress(0f, true);
        CameraFollow.SetCursorLocked(!onTitle);
    }

    /// <summary>시작 화면에서 [시작하기].</summary>
    public void BeginGame()
    {
        if (!onTitle) return;
        onTitle = false;
        startTime = Time.time;
        CameraFollow.SetCursorLocked(true);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && !won && !onTitle) SetMenu(!menuOpen);
    }

    void SetMenu(bool open)
    {
        menuOpen = open;
        Time.timeScale = open ? 0f : 1f;
        CameraFollow.SetCursorLocked(!open);
    }

    // ---------------- Progress ----------------
    public void OnFoodEaten(Food f, CatPlayer p)
    {
        if (f.type == FoodType.LastPiece)
        {
            lastCollected = true;
            if (exitGate) exitGate.Open();
            p.SetProgress(1f);
            p.Emote(3);
            return;
        }

        rawCm += f.cm;
        p.AddFollower(f.cm, f.type);
        p.SetProgress(Progress);

        int stage = Mathf.FloorToInt(Progress * 4f);
        if (stage > lastStage)
        {
            lastStage = stage;
            p.Emote(3);   // 색이 한 단계 돌아올 때 빙글
        }
        CheckLastPiece();
    }

    void CheckLastPiece()
    {
        if (lastSpawned || rawCm < Goal - 1) return;
        lastSpawned = true;
        Vector3 pos = lastPieceSpawn ? lastPieceSpawn.position : Vector3.zero;
        Food.Create(FoodType.LastPiece, pos, foodRoot, false);
        CameraFollow.Shake(0.2f);
    }

    // ---------------- Obstacles ----------------
    public void OnKittensLost(int cm, int count, CatPlayer p)
    {
        CameraFollow.Shake(0.35f);
        if (count == 0) return;
        rawCm = Mathf.Max(0, rawCm - cm);
        p.SetProgress(Progress);
    }

    public void OnSwept(CatPlayer p) => CameraFollow.Shake(0.3f);

    public void OnKittenRejoined(Follower f, CatPlayer p)
    {
        rawCm += f.cm;
        p.SetProgress(Progress);
        CheckLastPiece();
    }

    public void TryExit()
    {
        if (won || !lastCollected) return;
        won = true;
        endTime = Time.time;
        player.InputEnabled = false;
        player.StartCelebration();
        CameraFollow.SetCursorLocked(false);
    }

    /// <summary>다시 시작. toTitle = true면 시작 화면부터, 아니면 바로 플레이.</summary>
    public void Restart() => Restart(false);

    public void Restart(bool toTitle)
    {
        skipTitle = !toTitle;
        Time.timeScale = 1f;
        var scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(scene.buildIndex);
#endif
    }

    // ---------------- GUI ----------------
    GUIStyle barStyle, subStyle, bigStyle, headStyle, bodyStyle, btnStyle, tabStyle, tabOnStyle;
    Texture2D texBtn, texBtnHover, texTab, texTabOn;
    float styleScale = -1f;

    static Texture2D Tex(Color c)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    void EnsureStyles(float s)
    {
        if (Mathf.Approximately(s, styleScale) && barStyle != null) return;
        styleScale = s;
        if (!texBtn)
        {
            texBtn = Tex(new Color(0.55f, 0.42f, 0.85f));
            texBtnHover = Tex(new Color(0.68f, 0.55f, 0.95f));
            texTab = Tex(new Color(0.9f, 0.86f, 0.97f));
            texTabOn = Tex(new Color(1f, 1f, 1f));
        }
        GUIStyle Make(int size, TextAnchor anchor, Color col, FontStyle fs = FontStyle.Bold)
        {
            var st = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(size * s), alignment = anchor, fontStyle = fs, wordWrap = true, richText = true
            };
            st.normal.textColor = col;
            return st;
        }
        barStyle = Make(26, TextAnchor.MiddleCenter, Color.white);
        subStyle = Make(20, TextAnchor.UpperCenter, Color.white);
        bigStyle = Make(72, TextAnchor.MiddleCenter, Color.white);
        headStyle = Make(40, TextAnchor.MiddleCenter, new Color(0.32f, 0.22f, 0.5f));
        bodyStyle = Make(21, TextAnchor.UpperLeft, new Color(0.3f, 0.25f, 0.4f), FontStyle.Normal);

        GUIStyle Btn(Texture2D normal, Texture2D hover, Color text, int size)
        {
            var st = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(size * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            st.normal.background = normal; st.normal.textColor = text;
            st.hover.background = hover; st.hover.textColor = text;
            st.active.background = hover; st.active.textColor = text;
            st.focused.background = normal; st.focused.textColor = text;
            st.border = new RectOffset(0, 0, 0, 0);
            return st;
        }
        btnStyle = Btn(texBtn, texBtnHover, Color.white, 24);
        tabStyle = Btn(texTab, texTabOn, new Color(0.45f, 0.38f, 0.6f), 22);
        tabOnStyle = Btn(texTabOn, texTabOn, new Color(0.32f, 0.22f, 0.5f), 22);
    }

    static void Rect(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    static void ShadowLabel(Rect r, string text, GUIStyle st)
    {
        var col = st.normal.textColor;
        st.normal.textColor = new Color(0.15f, 0.1f, 0.25f, 0.6f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, st);
        st.normal.textColor = col;
        GUI.Label(r, text, st);
    }

    public const string ControlsText =
        "<b>WASD / 방향키</b>   이동\n\n" +
        "<b>마우스</b>   시점 돌리기\n\n" +
        "<b>Space</b>   점프\n\n" +
        "<b>E</b>   음식 훔쳐 먹기  (바로 앞까지 가면 음식이 커져요)\n\n" +
        "<b>1 ~ 4</b>   감정표현  ♥  !  ?  ♪  (행렬도 따라 해요)\n\n" +
        "<b>Esc</b>   메뉴 열기 / 닫기";

    public const string GuideText =
        "<b>목표</b>   음식을 훔쳐 먹을수록 고양이 행렬이 길어지고 몸에 색이 돌아와요. 100CM가 되면 분수 앞에 '마지막 1CM'가 나타나고, 101CM를 채우면 EXIT가 열려요.\n\n" +
        "<b>과자 상자</b>   고양이 3마리 이상 행렬로 부딪히면 와르르 무너지면서 과자가 쏟아져요.\n\n" +
        "<b>과일 수레</b>   고양이 6마리 이상 행렬로 부딪히면 과일이 데굴데굴 굴러가요. 쫓아가서 먹어요.\n\n" +
        "<b>쇼핑카트</b>   점선 길을 따라 혼자 굴러다녀요. 치이면 행렬 고양이들이 흩어지고, 잠시 뒤 원래 음식으로 돌아가요. 다시 훔쳐 먹으면 돼요.\n\n" +
        "<b>마법 빗자루</b>   과자 가게 앞을 쓸고 다녀요. 닿으면 행렬째 멀리 쓸려 날아가요.";

    void OnGUI()
    {
        if (onTitle || won) return;   // 시작·엔딩 화면은 GameFlowUI가 그림
        float s = Screen.height / 1080f;
        EnsureStyles(s);

        // 진행 바
        float w = 640 * s, h = 46 * s, x = (Screen.width - w) / 2f, y = 24 * s;
        Rect(new Rect(x - 6 * s, y - 6 * s, w + 12 * s, h + 12 * s), new Color(1f, 1f, 1f, 0.85f));
        Rect(new Rect(x, y, w, h), new Color(0.25f, 0.2f, 0.35f, 0.92f));
        Color fill = Progress < 0.05f ? new Color(0.4f, 0.4f, 0.46f) : Palette.EvaluateProgress(Progress);
        Rect(new Rect(x, y, w * Progress, h), fill);
        Rect(new Rect(x + w * (100f / 101f) - 1.5f * s, y, 3 * s, h), new Color(1f, 0.85f, 0.3f));
        ShadowLabel(new Rect(x, y, w, h), $"{CM} / {Goal} CM", barStyle);
        if (player) ShadowLabel(new Rect(x, y + h + 10 * s, w, 30 * s), $"따라오는 고양이 {player.FollowerCount}마리", subStyle);

        if (menuOpen) DrawMenu(s);
    }

    void DrawMenu(float s)
    {
        Rect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.2f, 0.14f, 0.35f, 0.55f));
        float pw = 980 * s, ph = 640 * s;
        var panel = new Rect((Screen.width - pw) / 2f, (Screen.height - ph) / 2f, pw, ph);
        Rect(new Rect(panel.x - 6 * s, panel.y - 6 * s, panel.width + 12 * s, panel.height + 12 * s), new Color(0.75f, 0.62f, 0.95f, 0.9f));
        Rect(panel, new Color(0.98f, 0.96f, 1f, 0.97f));

        GUI.Label(new Rect(panel.x, panel.y + 18 * s, panel.width, 60 * s), "일시정지", headStyle);

        // 왼쪽 버튼
        float bx = panel.x + 40 * s, by = panel.y + 110 * s, bw = 240 * s, bh = 70 * s;
        if (GUI.Button(new Rect(bx, by, bw, bh), "계속하기", btnStyle)) SetMenu(false);
        if (GUI.Button(new Rect(bx, by + bh + 20 * s, bw, bh), "다시 시작", btnStyle)) Restart();
        if (GUI.Button(new Rect(bx, by + (bh + 20 * s) * 2f, bw, bh), "처음으로", btnStyle)) Restart(true);

        // 오른쪽 탭 + 내용
        float cx = bx + bw + 40 * s, cw = panel.xMax - 40 * s - cx;
        float tw = cw / 2f, th = 56 * s;
        if (GUI.Button(new Rect(cx, by, tw, th), "조작 키", menuTab == 0 ? tabOnStyle : tabStyle)) menuTab = 0;
        if (GUI.Button(new Rect(cx + tw, by, tw, th), "게임 안내", menuTab == 1 ? tabOnStyle : tabStyle)) menuTab = 1;
        var content = new Rect(cx, by + th, cw, panel.yMax - 40 * s - (by + th));
        Rect(content, Color.white);
        GUI.Label(new Rect(content.x + 28 * s, content.y + 24 * s, content.width - 56 * s, content.height - 40 * s),
            menuTab == 0 ? ControlsText : GuideText, bodyStyle);
    }
}
