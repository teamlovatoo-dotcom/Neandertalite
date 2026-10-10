using UnityEngine;

namespace Neanderthal
{
    public enum GameMode { Menu, Arena, Survival }

    /// <summary>
    /// Top-level flow: main menu backdrop, starting/restarting modes, level lifetime.
    /// Everything for a match lives under <see cref="LevelRoot"/> and is destroyed on exit.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public GameMode Mode { get; private set; } = GameMode.Menu;
        public Transform LevelRoot { get; private set; }
        public ModeBase ActiveMode { get; private set; }

        Camera menuCamera;
        float menuOrbit;
        Vector3 respawnPoint;
        System.Action restartAction;

        public string ObjectiveHeader => ActiveMode != null ? ActiveMode.ObjectiveHeader : "";
        public string ObjectiveText => ActiveMode != null ? ActiveMode.ObjectiveText : "";
        public string ObjectiveHint => ActiveMode != null ? ActiveMode.ObjectiveHint : "";
        public Vector3 RespawnPoint => respawnPoint;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Application.targetFrameRate = 144;
            ItemDatabase.Build();
            Profile.Load();
            Sfx.Init();
            if (GetComponent<UIManager>() == null) gameObject.AddComponent<UIManager>();
        }

        void Start() => ReturnToMenu();

        public bool TryGetRadarTarget(out Vector3 pos, out Color color)
        {
            if (ActiveMode != null) return ActiveMode.TryGetRadarTarget(out pos, out color);
            pos = default;
            color = default;
            return false;
        }

        public void DrawModeHud()
        {
            if (ActiveMode != null) ActiveMode.DrawHud();
        }

        public void SetRespawn(Vector3 pos) => respawnPoint = pos;

        void ClearLevel()
        {
            Time.timeScale = 1f;
            GameEvents.ClearAll();
            Creature.All.Clear();
            if (LevelRoot != null) Destroy(LevelRoot.gameObject);
            LevelRoot = new GameObject("Level").transform;
            ActiveMode = null;
            menuCamera = null;
        }

        public void ReturnToMenu()
        {
            ClearLevel();
            Mode = GameMode.Menu;
            // Backdrop: the valley with the tower, seen from a slowly orbiting camera.
            ValleyLevel.Build(LevelRoot, false);
            var camGo = new GameObject("MenuCamera");
            camGo.transform.SetParent(LevelRoot, false);
            menuCamera = camGo.AddComponent<Camera>();
            menuCamera.fieldOfView = 55f;
            menuCamera.farClipPlane = 900f;
            camGo.AddComponent<AudioListener>();
            UIManager.Instance.Show(UIScreen.MainMenu);
        }

        public void StartArena(ArenaSettings settings)
        {
            ClearLevel();
            Mode = GameMode.Arena;
            var mode = LevelRoot.gameObject.AddComponent<ArenaMode>();
            ActiveMode = mode;
            mode.Begin(settings);
            restartAction = () => StartArena(settings);
            UIManager.Instance.Show(UIScreen.Hud);
        }

        public void StartSurvival()
        {
            ClearLevel();
            Mode = GameMode.Survival;
            var mode = LevelRoot.gameObject.AddComponent<SurvivalMode>();
            ActiveMode = mode;
            mode.Begin();
            restartAction = StartSurvival;
            UIManager.Instance.Show(UIScreen.Hud);
        }

        public void Restart()
        {
            if (restartAction != null) restartAction();
            else ReturnToMenu();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Update()
        {
            if (Mode == GameMode.Menu && menuCamera != null)
            {
                menuOrbit += Time.unscaledDeltaTime * 2.5f;
                float a = menuOrbit * Mathf.Deg2Rad;
                Vector3 focus = new Vector3(0f, 30f, 80f);
                Vector3 pos = new Vector3(Mathf.Sin(a) * 60f, 0f, -40f + Mathf.Cos(a) * 30f);
                pos.y = ValleyLevel.Height(pos.x, pos.z) + 9f;
                menuCamera.transform.position = pos;
                menuCamera.transform.rotation = Quaternion.LookRotation(focus - pos);
            }
        }
    }
}
