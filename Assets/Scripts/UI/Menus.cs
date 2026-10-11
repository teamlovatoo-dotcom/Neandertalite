using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    public enum ArenaMap { CaveForest, CanyonFortress }

    public class ArenaSettings
    {
        public ArenaMap Map = ArenaMap.CaveForest;
        public int Opponents = 1;  // 1 = duel, more = free-for-all deathmatch
        public string Primary = "bow";
        public string Secondary = "spear";
        public string Melee = "knife";
        public int Difficulty = 1; // 0 easy, 1 normal, 2 hard
        public int ScoreToWin = 5;
    }

    /// <summary>Main menu, settings, duel setup, pause and result screens.</summary>
    public static class Menus
    {
        public static readonly ArenaSettings Duel = new ArenaSettings();
        static readonly string[] Primaries = { "bow", "compound_bow", "crossbow", "shotgun", "rifle", "sniper" };
        static readonly string[] Secondaries = { "spear", "pistol" };
        static readonly string[] Melees = { "knife", "club", "axe", "bone_dagger", "torch" };
        static bool confirmReset;

        public static void DrawMainMenu()
        {
            float W = UIStyle.Width, H = UIStyle.Height;
            // Left-side gradient so the title reads over the 3D backdrop.
            for (int i = 0; i < 20; i++)
                UIStyle.Rect(new Rect(i * 40, 0, 40, H), new Color(0.03f, 0.03f, 0.04f, 0.8f * (1f - i / 20f)));

            UIStyle.Shadowed(new Rect(80, 120, 900, 120), "НЕАНДЕРТАЛЕЦЬ", 104, new Color(0.85f, 0.84f, 0.8f), TextAnchor.MiddleLeft, true);
            UIStyle.Shadowed(new Rect(88, 236, 900, 40), UIStyle.Spaced("Дуелі  //  Мультиплеєр"), 22, UIStyle.TextColor, TextAnchor.MiddleLeft);

            float y = 340;
            float x = 88;
            if (UIStyle.Button(new Rect(x, y, 380, 56), "ГРАТИ  —  БІЙ НА АРЕНІ", true, true, 20)) UIManager.Instance.Show(UIScreen.DuelSetup);
            y += 70;
            if (UIStyle.Button(new Rect(x, y, 380, 56), "ТРЕНУВАННЯ  —  ДОЛИНА", false, true, 20)) GameManager.Instance.StartSurvival();
            y += 70;
            if (UIStyle.Button(new Rect(x, y, 380, 56), "МАГАЗИН / КРЕСЛЕННЯ", false, true, 20)) UIManager.Instance.Show(UIScreen.MenuShop);
            y += 70;
            if (UIStyle.Button(new Rect(x, y, 380, 56), "НАЛАШТУВАННЯ", false, true, 20)) UIManager.Instance.OpenSettings(UIScreen.MainMenu);
            y += 70;
            if (UIStyle.Button(new Rect(x, y, 380, 56), "ВИХІД", false, true, 20)) GameManager.Instance.Quit();

            var prof = Profile.Current;
            UIStyle.Shadowed(new Rect(x, H - 150, 600, 30), $"<color=#ffd15a>◆</color> {prof.Coins} монет     Креслення: {prof.Blueprints.Count}/8     Дуелі: {prof.DuelWins} перемог / {prof.DuelLosses} поразок", 18, UIStyle.TextColor);
            if (prof.ChapterOneComplete) UIStyle.Shadowed(new Rect(x, H - 120, 600, 30), "Розділ 1 пройдено", 16, UIStyle.Gold);
            UIStyle.Shadowed(new Rect(x, H - 70, 900, 30), "WASD рух · Shift біг · Ctrl присід · Пробіл стрибок · ЛКМ атака · ПКМ приціл/кидок · R перезарядка · E дія · Tab крафт · H лікування · 1-5 зброя", 15, UIStyle.TextDim);
            UIStyle.Shadowed(new Rect(W - 400, H - 70, 360, 30), UIStyle.Spaced("Первісні навички. Сучасний вибір."), 14, UIStyle.TextDim, TextAnchor.MiddleRight);
        }

        public static void DrawSettings(System.Action back)
        {
            float W = UIStyle.Width, H = UIStyle.Height;
            UIStyle.Rect(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.6f));
            Rect panel = new Rect(W * 0.5f - 420, H * 0.5f - 260, 840, 520);
            UIStyle.PanelBox(panel);
            UIStyle.Text(new Rect(panel.x + 40, panel.y + 30, 500, 40), UIStyle.Spaced("Налаштування"), 28, Color.white, TextAnchor.MiddleLeft, true);
            var p = Profile.Current;
            float y = panel.y + 110;
            float sens = UIStyle.Slider(new Rect(panel.x + 40, y, panel.width - 80, 40), "Чутливість миші", p.MouseSensitivity, 0.2f, 6f, "0.0");
            y += 60;
            float fov = UIStyle.Slider(new Rect(panel.x + 40, y, panel.width - 80, 40), "Кут огляду", p.Fov, 60f, 110f, "0");
            y += 60;
            float vol = UIStyle.Slider(new Rect(panel.x + 40, y, panel.width - 80, 40), "Гучність", p.Volume, 0f, 1f, "0.00");
            if (!Mathf.Approximately(sens, p.MouseSensitivity) || !Mathf.Approximately(fov, p.Fov) || !Mathf.Approximately(vol, p.Volume))
            {
                p.MouseSensitivity = sens;
                p.Fov = fov;
                p.Volume = vol;
                p.Apply();
            }
            y += 90;
            if (!confirmReset)
            {
                if (UIStyle.Button(new Rect(panel.x + 40, y, 300, 44), "СКИНУТИ ПРОГРЕС", false, true, 16)) confirmReset = true;
            }
            else
            {
                UIStyle.Text(new Rect(panel.x + 40, y - 30, 600, 24), "Стерти монети, креслення і статистику?", 16, UIStyle.Bad);
                if (UIStyle.Button(new Rect(panel.x + 40, y, 140, 44), "ТАК, СКИНУТИ", false, true, 16)) { Profile.Reset(); confirmReset = false; }
                if (UIStyle.Button(new Rect(panel.x + 200, y, 140, 44), "СКАСУВАТИ", false, true, 16)) confirmReset = false;
            }
            if (UIStyle.Button(new Rect(panel.xMax - 200, panel.yMax - 70, 160, 44), "НАЗАД", true, true, 18))
            {
                p.Save();
                back();
            }
        }

        public static void DrawDuelSetup()
        {
            float W = UIStyle.Width, H = UIStyle.Height;
            UIStyle.Rect(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.6f));
            Rect panel = new Rect(W * 0.5f - 640, H * 0.5f - 400, 1280, 800);
            UIStyle.PanelBox(panel);
            UIStyle.Text(new Rect(panel.x + 40, panel.y + 26, 600, 40), UIStyle.Spaced(Duel.Opponents == 1 ? "Дуель" : "Усі проти всіх"), 30, Color.white, TextAnchor.MiddleLeft, true);
            UIStyle.Text(new Rect(panel.x + 40, panel.y + 66, 600, 24), UIStyle.Spaced($"до {Duel.ScoreToWin} вбивств") + (Duel.Opponents == 1 ? "   ·   1 на 1" : $"   ·   кожен сам за себе, мисливців: {Duel.Opponents + 1}"), 15, UIStyle.TextDim);

            float x = panel.x + 40, y = panel.y + 120;
            UIStyle.Text(new Rect(x, y, 300, 24), UIStyle.Spaced("Карта"), 14, UIStyle.TextDim);
            y += 30;
            if (UIStyle.Button(new Rect(x, y, 380, 90), "ПЕЧЕРНИЙ ЛІС\n<size=13>ліс, струмок, печерний тунель, мости</size>", Duel.Map == ArenaMap.CaveForest, true, 20)) Duel.Map = ArenaMap.CaveForest;
            if (UIStyle.Button(new Rect(x + 400, y, 380, 90), "ФОРТЕЦЯ В КАНЬЙОНІ\n<size=13>кам'яна фортеця, пандуси, тунелі</size>", Duel.Map == ArenaMap.CanyonFortress, true, 20)) Duel.Map = ArenaMap.CanyonFortress;

            y += 120;
            Duel.Primary = Choice(ref y, x, "Основна", Primaries, Duel.Primary);
            Duel.Secondary = Choice(ref y, x, "Додаткова", Secondaries, Duel.Secondary);
            Duel.Melee = Choice(ref y, x, "Ближній бій", Melees, Duel.Melee);

            UIStyle.Text(new Rect(x, y, 300, 24), UIStyle.Spaced("Суперники"), 14, UIStyle.TextDim);
            UIStyle.Text(new Rect(x + 560, y, 300, 24), UIStyle.Spaced("Рівень ботів"), 14, UIStyle.TextDim);
            y += 30;
            int[] counts = { 1, 3, 5 };
            string[] countLabels = { "1 — ДУЕЛЬ", "3 — ВСІ НА ВСІХ", "5 — ВСІ НА ВСІХ" };
            for (int i = 0; i < 3; i++)
                if (UIStyle.Button(new Rect(x + i * 170, y, 160, 44), countLabels[i], Duel.Opponents == counts[i], true, 16)) Duel.Opponents = counts[i];
            string[] diffs = { "ЛЕГКО", "НОРМАЛЬНО", "ВАЖКО" };
            for (int i = 0; i < 3; i++)
                if (UIStyle.Button(new Rect(x + 560 + i * 130, y, 120, 44), diffs[i], Duel.Difficulty == i, true, 16)) Duel.Difficulty = i;

            GUI.Label(new Rect(panel.xMax - 270, panel.y + 560, 240, 140),
                "Креслення з МАГАЗИНУ відкривають сучасну зброю.\nПредмети на карті з'являються знову.\nПеремога +150, поразка +40, вбивство +10 монет.\nПоки що суперники — боти.",
                UIStyle.Wrap(13, UIStyle.TextDim));

            if (UIStyle.Button(new Rect(panel.xMax - 420, panel.yMax - 80, 180, 50), "НАЗАД", false, true, 18)) UIManager.Instance.Show(UIScreen.MainMenu);
            if (UIStyle.Button(new Rect(panel.xMax - 220, panel.yMax - 80, 180, 50), "У БІЙ", true, true, 20)) GameManager.Instance.StartArena(Duel);
        }

        static string Choice(ref float y, float x, string title, string[] options, string current)
        {
            UIStyle.Text(new Rect(x, y, 300, 24), UIStyle.Spaced(title), 14, UIStyle.TextDim);
            y += 30;
            float bx = x;
            foreach (var id in options)
            {
                var data = ItemDatabase.Weapon(id);
                string bp = ItemDatabase.BlueprintForWeapon(id);
                bool unlocked = bp == null || Profile.Current.HasBlueprint(bp);
                string label = unlocked ? data.Name : data.Name + "\n<size=11>потрібне креслення</size>";
                if (UIStyle.Button(new Rect(bx, y, 180, 50), label, current == id, unlocked, 14)) current = id;
                bx += 190;
            }
            // Fall back if the chosen weapon got locked (e.g. after a progress reset).
            string curBp = ItemDatabase.BlueprintForWeapon(current);
            if (curBp != null && !Profile.Current.HasBlueprint(curBp)) current = options[0];
            y += 70;
            return current;
        }

        public static void DrawPause()
        {
            float W = UIStyle.Width, H = UIStyle.Height;
            UIStyle.Rect(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.6f));
            Rect panel = new Rect(W * 0.5f - 220, H * 0.5f - 210, 440, 420);
            UIStyle.PanelBox(panel);
            UIStyle.Text(new Rect(panel.x, panel.y + 26, panel.width, 40), UIStyle.Spaced("Пауза"), 28, Color.white, TextAnchor.MiddleCenter, true);
            float y = panel.y + 100;
            if (UIStyle.Button(new Rect(panel.x + 40, y, panel.width - 80, 50), "ПРОДОВЖИТИ", true, true, 18)) UIManager.Instance.Show(UIScreen.Hud);
            y += 64;
            if (UIStyle.Button(new Rect(panel.x + 40, y, panel.width - 80, 50), "НАЛАШТУВАННЯ", false, true, 18)) UIManager.Instance.OpenSettings(UIScreen.Pause);
            y += 64;
            if (UIStyle.Button(new Rect(panel.x + 40, y, panel.width - 80, 50), "ЗАНОВО", false, true, 18)) GameManager.Instance.Restart();
            y += 64;
            if (UIStyle.Button(new Rect(panel.x + 40, y, panel.width - 80, 50), "ГОЛОВНЕ МЕНЮ", false, true, 18)) GameManager.Instance.ReturnToMenu();
        }

        public static void DrawResult(string title, string body)
        {
            float W = UIStyle.Width, H = UIStyle.Height;
            UIStyle.Rect(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.75f));
            UIStyle.Shadowed(new Rect(0, H * 0.32f, W, 100), title, 72, Color.white, TextAnchor.MiddleCenter, true);
            UIStyle.Shadowed(new Rect(W * 0.5f - 500, H * 0.32f + 110, 1000, 120), body, 22, UIStyle.TextColor, TextAnchor.UpperCenter);
            if (UIStyle.Button(new Rect(W * 0.5f - 210, H * 0.7f, 200, 54), "ЩЕ РАЗ", false, true, 18)) GameManager.Instance.Restart();
            if (UIStyle.Button(new Rect(W * 0.5f + 10, H * 0.7f, 200, 54), "ГОЛОВНЕ МЕНЮ", true, true, 18)) GameManager.Instance.ReturnToMenu();
        }
    }
}
