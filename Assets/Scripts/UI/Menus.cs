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

            UIStyle.Shadowed(new Rect(80, 120, 900, 120), "NEANDERTHAL", 104, new Color(0.85f, 0.84f, 0.8f), TextAnchor.MiddleLeft, true);
            UIStyle.Shadowed(new Rect(88, 236, 900, 40), UIStyle.Spaced("Duels  //  Multiplayer"), 22, UIStyle.TextColor, TextAnchor.MiddleLeft);

            float y = 340;
            float x = 88;
            if (UIStyle.Button(new Rect(x, y, 380, 56), "PLAY  —  PVP ARENA", true, true, 20)) UIManager.Instance.Show(UIScreen.DuelSetup);
            y += 70;
            if (UIStyle.Button(new Rect(x, y, 380, 56), "TRAINING  —  SURVIVAL VALLEY", false, true, 20)) GameManager.Instance.StartSurvival();
            y += 70;
            if (UIStyle.Button(new Rect(x, y, 380, 56), "SHOP / BLUEPRINTS", false, true, 20)) UIManager.Instance.Show(UIScreen.MenuShop);
            y += 70;
            if (UIStyle.Button(new Rect(x, y, 380, 56), "SETTINGS", false, true, 20)) UIManager.Instance.OpenSettings(UIScreen.MainMenu);
            y += 70;
            if (UIStyle.Button(new Rect(x, y, 380, 56), "QUIT", false, true, 20)) GameManager.Instance.Quit();

            var prof = Profile.Current;
            UIStyle.Shadowed(new Rect(x, H - 150, 600, 30), $"<color=#ffd15a>◆</color> {prof.Coins} coins     Blueprints: {prof.Blueprints.Count}/8     Duels: {prof.DuelWins}W / {prof.DuelLosses}L", 18, UIStyle.TextColor);
            if (prof.ChapterOneComplete) UIStyle.Shadowed(new Rect(x, H - 120, 600, 30), "Chapter 1 complete", 16, UIStyle.Gold);
            UIStyle.Shadowed(new Rect(x, H - 70, 900, 30), "WASD move · Shift sprint · Ctrl crouch · Space jump · LMB attack · RMB aim/throw · R reload · E use · Tab craft · H heal · 1-5 weapons", 15, UIStyle.TextDim);
            UIStyle.Shadowed(new Rect(W - 400, H - 70, 360, 30), UIStyle.Spaced("Primitive skills. Modern choices."), 14, UIStyle.TextDim, TextAnchor.MiddleRight);
        }

        public static void DrawSettings(System.Action back)
        {
            float W = UIStyle.Width, H = UIStyle.Height;
            UIStyle.Rect(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.6f));
            Rect panel = new Rect(W * 0.5f - 420, H * 0.5f - 260, 840, 520);
            UIStyle.PanelBox(panel);
            UIStyle.Text(new Rect(panel.x + 40, panel.y + 30, 500, 40), UIStyle.Spaced("Settings"), 28, Color.white, TextAnchor.MiddleLeft, true);
            var p = Profile.Current;
            float y = panel.y + 110;
            float sens = UIStyle.Slider(new Rect(panel.x + 40, y, panel.width - 80, 40), "Mouse sensitivity", p.MouseSensitivity, 0.2f, 6f, "0.0");
            y += 60;
            float fov = UIStyle.Slider(new Rect(panel.x + 40, y, panel.width - 80, 40), "Field of view", p.Fov, 60f, 110f, "0");
            y += 60;
            float vol = UIStyle.Slider(new Rect(panel.x + 40, y, panel.width - 80, 40), "Volume", p.Volume, 0f, 1f, "0.00");
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
                if (UIStyle.Button(new Rect(panel.x + 40, y, 300, 44), "RESET PROGRESS", false, true, 16)) confirmReset = true;
            }
            else
            {
                UIStyle.Text(new Rect(panel.x + 40, y - 30, 600, 24), "Erase coins, blueprints and stats?", 16, UIStyle.Bad);
                if (UIStyle.Button(new Rect(panel.x + 40, y, 140, 44), "YES, RESET", false, true, 16)) { Profile.Reset(); confirmReset = false; }
                if (UIStyle.Button(new Rect(panel.x + 200, y, 140, 44), "CANCEL", false, true, 16)) confirmReset = false;
            }
            if (UIStyle.Button(new Rect(panel.xMax - 200, panel.yMax - 70, 160, 44), "BACK", true, true, 18))
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
            UIStyle.Text(new Rect(panel.x + 40, panel.y + 26, 600, 40), UIStyle.Spaced(Duel.Opponents == 1 ? "Duel" : "Deathmatch"), 30, Color.white, TextAnchor.MiddleLeft, true);
            UIStyle.Text(new Rect(panel.x + 40, panel.y + 66, 600, 24), UIStyle.Spaced($"first to {Duel.ScoreToWin}") + (Duel.Opponents == 1 ? "   ·   1 vs 1" : $"   ·   free-for-all, {Duel.Opponents + 1} hunters"), 15, UIStyle.TextDim);

            float x = panel.x + 40, y = panel.y + 120;
            UIStyle.Text(new Rect(x, y, 300, 24), UIStyle.Spaced("Map"), 14, UIStyle.TextDim);
            y += 30;
            if (UIStyle.Button(new Rect(x, y, 380, 90), "CAVE FOREST\n<size=13>forest, stream, cave tunnel, bridge</size>", Duel.Map == ArenaMap.CaveForest, true, 20)) Duel.Map = ArenaMap.CaveForest;
            if (UIStyle.Button(new Rect(x + 400, y, 380, 90), "CANYON FORTRESS\n<size=13>stone fortress, ramps, tunnels</size>", Duel.Map == ArenaMap.CanyonFortress, true, 20)) Duel.Map = ArenaMap.CanyonFortress;

            y += 120;
            Duel.Primary = Choice(ref y, x, "Primary", Primaries, Duel.Primary);
            Duel.Secondary = Choice(ref y, x, "Secondary", Secondaries, Duel.Secondary);
            Duel.Melee = Choice(ref y, x, "Melee", Melees, Duel.Melee);

            UIStyle.Text(new Rect(x, y, 300, 24), UIStyle.Spaced("Opponents"), 14, UIStyle.TextDim);
            UIStyle.Text(new Rect(x + 560, y, 300, 24), UIStyle.Spaced("Bot skill"), 14, UIStyle.TextDim);
            y += 30;
            int[] counts = { 1, 3, 5 };
            string[] countLabels = { "1  (DUEL)", "3  (FFA)", "5  (FFA)" };
            for (int i = 0; i < 3; i++)
                if (UIStyle.Button(new Rect(x + i * 170, y, 160, 44), countLabels[i], Duel.Opponents == counts[i], true, 16)) Duel.Opponents = counts[i];
            string[] diffs = { "EASY", "NORMAL", "HARD" };
            for (int i = 0; i < 3; i++)
                if (UIStyle.Button(new Rect(x + 560 + i * 130, y, 120, 44), diffs[i], Duel.Difficulty == i, true, 16)) Duel.Difficulty = i;

            UIStyle.Text(new Rect(panel.xMax - 440, panel.y + 120, 400, 300),
                "Modern weapons unlock with blueprints from the SHOP.\n\nWeapons, arrows, herbs and armor on the map respawn every 20-40 seconds.\n\nWin: +150 coins\nLose: +40 coins\nEach kill: +10 coins\n\nOpponents are bots for now - the same rules will run over the network.",
                16, UIStyle.TextDim);

            if (UIStyle.Button(new Rect(panel.xMax - 420, panel.yMax - 80, 180, 50), "BACK", false, true, 18)) UIManager.Instance.Show(UIScreen.MainMenu);
            if (UIStyle.Button(new Rect(panel.xMax - 220, panel.yMax - 80, 180, 50), "FIGHT", true, true, 20)) GameManager.Instance.StartArena(Duel);
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
                string label = unlocked ? data.Name : data.Name + "\n<size=11>blueprint needed</size>";
                if (UIStyle.Button(new Rect(bx, y, 150, 50), label, current == id, unlocked, 15)) current = id;
                bx += 160;
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
            UIStyle.Text(new Rect(panel.x, panel.y + 26, panel.width, 40), UIStyle.Spaced("Paused"), 28, Color.white, TextAnchor.MiddleCenter, true);
            float y = panel.y + 100;
            if (UIStyle.Button(new Rect(panel.x + 40, y, panel.width - 80, 50), "RESUME", true, true, 18)) UIManager.Instance.Show(UIScreen.Hud);
            y += 64;
            if (UIStyle.Button(new Rect(panel.x + 40, y, panel.width - 80, 50), "SETTINGS", false, true, 18)) UIManager.Instance.OpenSettings(UIScreen.Pause);
            y += 64;
            if (UIStyle.Button(new Rect(panel.x + 40, y, panel.width - 80, 50), "RESTART", false, true, 18)) GameManager.Instance.Restart();
            y += 64;
            if (UIStyle.Button(new Rect(panel.x + 40, y, panel.width - 80, 50), "MAIN MENU", false, true, 18)) GameManager.Instance.ReturnToMenu();
        }

        public static void DrawResult(string title, string body)
        {
            float W = UIStyle.Width, H = UIStyle.Height;
            UIStyle.Rect(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.75f));
            UIStyle.Shadowed(new Rect(0, H * 0.32f, W, 100), title, 72, Color.white, TextAnchor.MiddleCenter, true);
            UIStyle.Shadowed(new Rect(W * 0.5f - 500, H * 0.32f + 110, 1000, 120), body, 22, UIStyle.TextColor, TextAnchor.UpperCenter);
            if (UIStyle.Button(new Rect(W * 0.5f - 210, H * 0.7f, 200, 54), "PLAY AGAIN", false, true, 18)) GameManager.Instance.Restart();
            if (UIStyle.Button(new Rect(W * 0.5f + 10, H * 0.7f, 200, 54), "MAIN MENU", true, true, 18)) GameManager.Instance.ReturnToMenu();
        }
    }
}
