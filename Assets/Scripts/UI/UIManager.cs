using UnityEngine;

namespace Neanderthal
{
    public enum UIScreen { MainMenu, Settings, DuelSetup, MenuShop, Hud, Shop, Crafting, Pause, Result, Inventory }

    /// <summary>Owns which screen is visible, cursor locking and pausing. All drawing is IMGUI.</summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }
        public UIScreen Screen = UIScreen.MainMenu;
        UIScreen settingsReturn = UIScreen.MainMenu;
        bool workbench;
        int shownFrame = -1;

        public string ResultTitle;
        public string ResultBody;

        public static bool AnyMenuOpen => Instance != null && Instance.Screen != UIScreen.Hud;
        /// <summary>True for a couple of frames after any screen change, so the closing key/click doesn't leak into gameplay.</summary>
        public static bool JustChanged => Instance != null && Time.frameCount - Instance.shownFrame <= 1;

        void Awake()
        {
            Instance = this;
            Hud.Hook();
        }

        public void OpenShop() => Show(UIScreen.Shop);

        public void OpenCrafting(bool atWorkbench)
        {
            workbench = atWorkbench;
            Show(UIScreen.Crafting);
        }

        public void ShowResult(string title, string body)
        {
            ResultTitle = title;
            ResultBody = body;
            Show(UIScreen.Result);
        }

        public void Show(UIScreen s)
        {
            Screen = s;
            shownFrame = Time.frameCount;
            Sfx.Play2D("click");
        }

        static bool PlayerDead => PlayerController.Instance != null && PlayerController.Instance.Health.IsDead;

        void Update()
        {
            WeaponIcons.Pump();
            DebugOverlay.Tick();
            bool inGame = GameManager.Instance != null && GameManager.Instance.Mode != GameMode.Menu;

            // The key that opened a screen this frame must not also close it.
            if (Time.frameCount == shownFrame) { }
            else if (inGame)
            {
                bool survival = GameManager.Instance.Mode == GameMode.Survival;   // there Tab is crafting
                if (Screen == UIScreen.Hud && GameInput.PauseDown) Show(UIScreen.Pause);
                else if (Screen == UIScreen.Hud && (GameInput.InventoryDown || GameInput.CraftingDown && !survival) && !PlayerDead)
                {
                    InventoryScreen.Reset();
                    Show(UIScreen.Inventory);
                }
                else if (Screen == UIScreen.Inventory && (GameInput.PauseDown || GameInput.InventoryDown || GameInput.CraftingDown || PlayerDead))
                    Show(UIScreen.Hud);
                else if ((Screen == UIScreen.Shop || Screen == UIScreen.Crafting) && (GameInput.PauseDown || GameInput.CraftingDown || GameInput.InteractDown && Screen == UIScreen.Shop))
                    Show(UIScreen.Hud);
                else if (Screen == UIScreen.Pause && GameInput.PauseDown) Show(UIScreen.Hud);
                else if (Screen == UIScreen.Settings && GameInput.PauseDown) Show(settingsReturn);
            }
            else if ((Screen == UIScreen.Settings || Screen == UIScreen.DuelSetup || Screen == UIScreen.MenuShop) && GameInput.PauseDown)
            {
                Show(UIScreen.MainMenu);
            }

            bool paused = inGame && (Screen == UIScreen.Pause || Screen == UIScreen.Shop || Screen == UIScreen.Crafting || Screen == UIScreen.Settings || Screen == UIScreen.Result);
            Time.timeScale = paused ? 0f : 1f;

            bool locked = inGame && Screen == UIScreen.Hud;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void OnGUI()
        {
            UIStyle.BeginCanvas();
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gm.Mode != GameMode.Menu)
            {
                Hud.Draw();
                gm.DrawModeHud();
            }

            switch (Screen)
            {
                case UIScreen.MainMenu: Menus.DrawMainMenu(); break;
                case UIScreen.Settings: Menus.DrawSettings(() => Show(settingsReturn)); break;
                case UIScreen.DuelSetup: Menus.DrawDuelSetup(); break;
                case UIScreen.MenuShop: ShopScreen.Draw(true, () => Show(UIScreen.MainMenu)); break;
                case UIScreen.Shop: ShopScreen.Draw(false, () => Show(UIScreen.Hud)); break;
                case UIScreen.Crafting: CraftingScreen.Draw(workbench, () => Show(UIScreen.Hud)); break;
                case UIScreen.Inventory: InventoryScreen.Draw(() => Show(UIScreen.Hud)); break;
                case UIScreen.Pause: Menus.DrawPause(); break;
                case UIScreen.Result: Menus.DrawResult(ResultTitle, ResultBody); break;
            }
        }

        public void OpenSettings(UIScreen returnTo)
        {
            settingsReturn = returnTo;
            Show(UIScreen.Settings);
        }
    }
}
