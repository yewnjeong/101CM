using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CM101
{
    public enum GameState { Playing, Paused, Won, Failed }

    /// <summary>
    /// 목표와 게임 상태(기획서 05장): 첫 100cm 기록, 최종 젤리 잠금, 동시 판정, 실패/클리어, 소멸분 보충, 자원 총량 검증.
    /// 실패 조건은 없다(2026-09-29: 청소기 실패 삭제, 체크포인트 저장 안 함). Fail()은 비상용으로만 남겨 둔다.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }
        public static bool IsPlaying => I != null && I.State == GameState.Playing;

        public LeaderController leader;
        public JellyChain chain;
        public OrbitCamera cam;
        public Transform finalSpawnPoint;
        public Transform exitPoint;
        public GameObject exitDoor;
        public float resultDelay = 3.5f;

        [Header("Tutorial (창고)")]
        public bool startWithTutorial = true;
        public Bounds warehouseBounds = new Bounds(new Vector3(7f, 2f, 9f), new Vector3(14.6f, 6f, 18f)); // 창고를 벗어나면 튜토리얼 종료
        public GameObject tutorialGate;            // 종료 시 닫히는 창고 셔터
        public bool TutorialActive { get; private set; }
        bool tutorialAteOnce;

        public GameState State { get; private set; } = GameState.Playing;
        public bool FirstHundredReached { get; private set; }
        public FinalJelly Final { get; private set; }
        public bool FinalConsumed { get; private set; }
        public string FailReason { get; private set; }
        public float WinTime { get; private set; } = -1f;
        public float PlayTime => (State == GameState.Won ? WinTime : State == GameState.Failed ? FailTime : Time.time) - startTime;
        public float FailTime { get; private set; }
        public int FoodEatenCount { get; private set; }
        /// <summary>연결에서 떨어져 나간 동료 수 누적(같은 젤리가 여러 번 떨어지면 매번 센다).</summary>
        public int LostCount { get; private set; }
        /// <summary>청소기에 완전히 흡입되어 사라진 동료 수.</summary>
        public int AbsorbedCount { get; private set; }
        public int RecoveredCount { get; private set; }
        public bool LedgerOk { get; private set; } = true;
        public bool ShowResult => State == GameState.Won && Time.time - WinTime >= resultDelay;

        public int DisplayLength => FinalConsumed ? JellyChain.Goal + 1 : (chain ? chain.CurrentLength : JellyChain.LeaderLength);

        public class ToastEntry
        {
            public string text;
            public MsgKind kind;
            public float time;
            public float duration;
        }

        public readonly List<ToastEntry> Toasts = new List<ToastEntry>();

        /// <summary>선두가 지금 있는 구역 (HUD 위치 표시용).</summary>
        public Level.ZoneVolume CurrentZone { get; private set; }
        Level.ZoneVolume[] zones;
        readonly HashSet<Level.ZoneId> visitedZones = new HashSet<Level.ZoneId>();
        public readonly List<FoodItem> RecentReplacements = new List<FoodItem>();

        float startTime;
        bool ledgerWarned;

        void Awake()
        {
            I = this;
            Time.timeScale = 1f;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        void Start()
        {
            int total = FoodItem.RemainingLength;
            if (total != JellyChain.FoodTotal)
                Debug.LogError($"[101CM] 음식 총량이 {total}cm입니다. 선두 10cm와 합쳐 100cm가 되려면 90cm여야 합니다.");
            else
                Debug.Log($"[101CM] 음식 총량 검증 OK: {FoodItem.All.Count}개, 합계 {total}cm");

            startTime = Time.time;
            LockCursor(true);
            zones = FindObjectsByType<Level.ZoneVolume>(FindObjectsSortMode.None);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!GetComponent<DevCheats>()) gameObject.AddComponent<DevCheats>(); // F1~F4 개발자 치트 (배포 빌드에는 없음)
#endif
            TutorialActive = startWithTutorial;
            if (tutorialGate) tutorialGate.SetActive(false);
            if (TutorialActive)
            {
                Notify("튜토리얼: WASD로 움직이고 마우스로 둘러보세요", MsgKind.Info, 5f);
                Notify("음식이 발밑 노란 링 안에 들어오면 Space로 먹어요. 뒤에 동료가 붙어요", MsgKind.Info, 6f);
            }
            else Notify("음식을 모아 100cm를 만드세요", MsgKind.Info, 4f);
        }

        void EndTutorial()
        {
            TutorialActive = false;
            int n = chain ? chain.ClearAllForTutorial() : 0;
            foreach (var f in FoodItem.All.ToArray())
                if (f && f.isTutorial && !f.Consumed) Destroy(f.gameObject);
            if (tutorialGate) tutorialGate.SetActive(true);

            // 본게임 기록은 여기서부터
            startTime = Time.time;
            FoodEatenCount = 0;
            LostCount = 0;
            AbsorbedCount = 0;
            RecoveredCount = 0;
            ledgerWarned = false;

            Notify(n > 0 ? $"튜토리얼 끝! 연습한 동료 {n}마리는 창고에 두고 가요" : "튜토리얼 끝!", MsgKind.Gold, 4f);
            Notify("이제부터 진짜예요. 음식을 모아 100cm를 만드세요", MsgKind.Info, 5f);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    if (State == GameState.Playing) Pause();
                    else if (State == GameState.Paused) Resume();
                }
                if (kb.rKey.wasPressedThisFrame && (State == GameState.Failed || ShowResult)) RestartFromBeginning();
            }

            if (State == GameState.Playing && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                Cursor.lockState != CursorLockMode.Locked)
                LockCursor(true);

            if (State == GameState.Playing && TutorialActive && leader && !warehouseBounds.Contains(leader.Body.position))
                EndTutorial();

            UpdateZone();

            if (State == GameState.Playing && !TutorialActive && !FirstHundredReached && chain && chain.CurrentLength >= JellyChain.Goal)
            {
                FirstHundredReached = true;
                Final = FinalJelly.Spawn(finalSpawnPoint ? finalSpawnPoint.position : leader.transform.position + leader.transform.forward * 2f);
                Notify("100cm 달성! 출구 홀에 마지막 1cm가 나타났어요", MsgKind.Gold, 5f);
            }

            if (ShowResult && Cursor.lockState == CursorLockMode.Locked) LockCursor(false);

            float now = Time.unscaledTime;
            Toasts.RemoveAll(t => now - t.time > t.duration);
            RecentReplacements.RemoveAll(f => !f || Time.time - f.SpawnTime > 15f);

            // 자원 총량 검증: 연결 + 분리 + 남은 음식 = 90cm
            if (chain && Time.frameCount % 20 == 0 && !FinalConsumed && !TutorialActive)
            {
                int sum = LedgerSum();
                LedgerOk = sum == JellyChain.FoodTotal;
                if (!LedgerOk && !ledgerWarned)
                {
                    ledgerWarned = true;
                    Debug.LogWarning($"[101CM] 자원 총량 불일치: 연결 {chain.CurrentLength - JellyChain.LeaderLength} + 분리 {chain.DetachedLength} + 음식 {FoodItem.RemainingLength} = {sum}cm");
                }
            }
        }

        /// <summary>선두가 있는 구역을 갱신하고, 처음 들어간 구역이면 이름을 크게 알린다(창고는 시작 구역이라 알리지 않는다).</summary>
        void UpdateZone()
        {
            if (!leader || zones == null) return;
            Vector3 p = leader.Body.position;
            if (CurrentZone && CurrentZone.Contains(p)) return;
            Level.ZoneVolume found = null;
            foreach (var z in zones) if (z && z.Contains(p)) { found = z; break; }
            if (!found) return;
            CurrentZone = found;
            if (visitedZones.Add(found.zone) && found.zone != Level.ZoneId.Z0 && State == GameState.Playing)
                Notify(found.zone == Level.ZoneId.Z6 ? $"{found.displayName} — 여기서 마지막 1cm를 먹어요" : $"{found.displayName}에 들어왔어요", MsgKind.Gold, 2.8f);
        }

        public int LedgerSum() => chain.CurrentLength - JellyChain.LeaderLength + chain.DetachedLength + FoodItem.RemainingLength;

        // ------------------------------------------------------------------ 진행

        public void OnFoodEaten(FoodType t, int len)
        {
            FoodEatenCount++;
            Notify($"{FoodData.DisplayName(t)} +{len}cm", MsgKind.Good, 1.5f);
            if (TutorialActive && !tutorialAteOnce)
            {
                tutorialAteOnce = true;
                Notify("Shift로 점프! 상자 위 음식도 먹어 보세요", MsgKind.Info, 7f);
                Notify("준비되면 FRESH 문으로! 연습한 동료는 창고에 남아요", MsgKind.Info, 8f);
            }
        }

        /// <summary>같은 갱신 시점의 피해는 JellyChain.Update(-100)에서 먼저 확정되므로 여기서 현재 길이로 판정한다.</summary>
        public void TryEatFinal(FinalJelly f)
        {
            if (State != GameState.Playing || FinalConsumed || !f) return;
            int cur = chain.CurrentLength;
            if (cur < JellyChain.Goal)
            {
                Notify($"100cm가 되어야 먹을 수 있어요 ({JellyChain.Goal - cur}cm 부족)", MsgKind.Warn);
                return;
            }
            FinalConsumed = true;
            State = GameState.Won;
            WinTime = Time.time;
            Destroy(f.gameObject);
            Final = null;
            if (exitDoor) exitDoor.SetActive(false);
            if (leader && exitPoint) leader.AutoMoveTarget = exitPoint.position + exitPoint.forward * 3f;
            if (chain) chain.PlayEmote(EmoteType.Spin);
            Notify("101CM! 모두 함께 탈출해요", MsgKind.Gold, resultDelay);
        }

        public void Fail(string reason)
        {
            if (State != GameState.Playing) return;
            State = GameState.Failed;
            FailReason = reason;
            FailTime = Time.time;
            LockCursor(false);
        }

        public void SpawnReplacement(FoodType type, int variant, Vector3 from, int len)
        {
            var sp = SupplyPoint.Nearest(from);
            Vector3 pos = sp ? sp.NextSpawnPosition() : from;
            var f = FoodItem.Create(type, variant, pos);
            f.isReplacement = true;
            RecentReplacements.Add(f);
            Notify($"동료가 흡입됐어요. {(sp ? sp.label : "근처")}에 +{len}cm 음식이 생겼어요", MsgKind.Warn, 4f);
        }

        // ------------------------------------------------------------------ 메뉴

        public void Pause()
        {
            if (State != GameState.Playing) return;
            State = GameState.Paused;
            Time.timeScale = 0f;
            LockCursor(false);
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            State = GameState.Playing;
            Time.timeScale = 1f;
            LockCursor(true);
        }

        public void RestartFromBeginning()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>커서를 다시 잠근 프레임. 그 클릭은 먹기로 치지 않는다.</summary>
        public static int CursorLockFrame = -1;

        static void LockCursor(bool locked)
        {
            if (locked && Cursor.lockState != CursorLockMode.Locked) CursorLockFrame = Time.frameCount;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        bool recoverTipShown;
        public static void RecordLoss(int n)
        {
            if (!I) return;
            I.LostCount += n;
            if (!I.recoverTipShown)
            {
                I.recoverTipShown = true;
                Notify("떨어진 동료를 링 안에 넣고 Space를 누르면 다시 붙어요. 5초가 지나면 음식으로 돌아가요", MsgKind.Info, 6f);
            }
        }
        public static void RecordAbsorbed(int n) { if (I) I.AbsorbedCount += n; }
        public static void RecordRecovered(int n) { if (I) I.RecoveredCount += n; }

        public static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return s >= 60 ? $"{s / 60}분 {s % 60:00}초" : $"{s}초";
        }

        public static void Notify(string text, MsgKind kind = MsgKind.Info, float duration = 2.5f)
        {
            if (!I) return;
            I.Toasts.Add(new ToastEntry { text = text, kind = kind, time = Time.unscaledTime, duration = duration });
            if (I.Toasts.Count > 5) I.Toasts.RemoveAt(0);
        }
    }
}
