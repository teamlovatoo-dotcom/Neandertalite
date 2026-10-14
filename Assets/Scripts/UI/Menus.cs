using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    public enum ArenaMap { CaveForest, CanyonFortress, Catacombs }

    public class ArenaSettings
    {
        public ArenaMap Map = ArenaMap.Catacombs;
        public int Opponents = 1;  // 1 = duel, more = free-for-all deathmatch
        public string Primary = "bow";
        public string Secondary = "axe";
        public string Melee = "knife";
        public int Difficulty = 1; // 0 easy, 1 normal, 2 hard
        public int ScoreToWin = 5;
    }

    /// <summary>Main menu, settings, duel setup, pause and result screens.</summary>
    public static class Menus
    {
        public static readonly ArenaSettings Duel = new ArenaSettings();
        // Only the author's weapons are in the game (ItemDatabase.Available).
        static readonly string[] Primaries = { "bow" };
        static readonly string[] Secondaries = { "axe", "knife" };
        static readonly string[] Melees = { "knife", "axe" };
        static bool confirmReset;

        static readonly float[] hover = new float[8];

        public static void DrawMainMenu()
        {
            float W = UIStyle.Width, H = UIStyle.Height;

            // Cinematic grading over the 3D backdrop: dark left side, soft top and bottom bands.
            UIStyle.Icon(new Rect(0, 0, 1150, H), UIStyle.FadeRight, new Color(0.02f, 0.025f, 0.03f, 0.92f));
            UIStyle.Icon(new Rect(0, H - 260, W, 260), UIStyle.FadeUp, new Color(0.02f, 0.025f, 0.03f, 0.85f));
            UIStyle.IconFlipped(new Rect(0, 0, W, 160), UIStyle.FadeUp, new Color(0.02f, 0.025f, 0.03f, 0.6f));

            float logoW = DrawLogo(new Vector2(96, 64), 150);
            var sub = UIStyle.Label(24, new Color(0.82f, 0.83f, 0.84f), TextAnchor.MiddleCenter, UIStyle.FontRegular);
            GUI.Label(new Rect(96 + 150 * 0.54f, 262, logoW, 36), UIStyle.Spaced("Дуелі   //   Мультиплеєр"), sub);

            float x = 104, y = 336;
            const float step = 70;
            if (MenuItem(new Rect(x, y, 520, 62), 0, "Нова гра", "дуель 1 на 1  ·  усі проти всіх", true)) UIManager.Instance.Show(UIScreen.DuelSetup);
            y += step;
            if (MenuItem(new Rect(x, y, 520, 62), 1, "Створити сервер", "назва, карта, режим, гравці"))
            {
                ServerDraft = new ServerRecord();
                UIManager.Instance.Show(UIScreen.CreateServer);
            }
            y += step;
            int servers = Profile.Current.ServerHistory.Count;
            if (MenuItem(new Rect(x, y, 520, 62), 2, "Історія серверів", servers > 0 ? $"збережено: {servers}" : "поки порожньо")) UIManager.Instance.Show(UIScreen.ServerHistory);
            y += step;
            if (MenuItem(new Rect(x, y, 520, 62), 3, "Магазин", ItemDatabase.ModernWeapons ? "креслення сучасної зброї" : "зброя кам'яної доби")) UIManager.Instance.Show(UIScreen.MenuShop);
            y += step;
            if (MenuItem(new Rect(x, y, 520, 62), 4, "Опції", null)) UIManager.Instance.OpenSettings(UIScreen.MainMenu);
            y += step;
            if (MenuItem(new Rect(x, y, 520, 62), 5, "Вихід", null)) GameManager.Instance.Quit();

            // Stacked tagline, as on the concept boards.
            var tag = UIStyle.Label(20, new Color(0.85f, 0.86f, 0.86f, 0.8f), TextAnchor.MiddleLeft, UIStyle.FontLight);
            string[] words = { "Первісні", "навички", "сучасний", "вибір" };
            for (int i = 0; i < words.Length; i++)
                GUI.Label(new Rect(110, H - 300 + i * 32, 400, 30), UIStyle.Spaced(words[i]), tag);

            var prof = Profile.Current;
            UIStyle.Coins(new Rect(110, H - 120, 200, 30), prof.Coins.ToString(), 20, Color.white, false, true);
            var stat = UIStyle.Label(16, UIStyle.TextDim, TextAnchor.MiddleLeft, UIStyle.FontRegular);
            string bpText = ItemDatabase.ModernWeapons ? $"креслення {prof.Blueprints.Count}/8   ·   " : "";
            GUI.Label(new Rect(110, H - 86, 900, 26), UIStyle.Spaced($"{bpText}перемоги {prof.DuelWins}   ·   поразки {prof.DuelLosses}"), stat);

            var corner = UIStyle.Label(15, new Color(0.8f, 0.8f, 0.8f, 0.75f), TextAnchor.MiddleRight, UIStyle.FontLight);
            GUI.Label(new Rect(W - 420, H - 120, 360, 24), UIStyle.Spaced("Від каменю"), corner);
            GUI.Label(new Rect(W - 420, H - 94, 360, 24), UIStyle.Spaced("до сталі"), corner);
            UIStyle.Rect(new Rect(W - 120, H - 62, 60, 1), new Color(1f, 1f, 1f, 0.5f));
        }

        /// <summary>
        /// The NEANDERTHAL wordmark: three claw scratches and brushed-steel lettering with a dark rim.
        /// Returns the width of the lettering.
        /// </summary>
        public static float DrawLogo(Vector2 pos, int size)
        {
            const string word = "NEANDERTHAL";
            var font = UIStyle.FontBold;
            float clawW = size * 0.5f;
            float tx = pos.x + clawW + size * 0.04f;
            var measure = UIStyle.Label(size, Color.white, TextAnchor.UpperLeft, font);
            Vector2 sz = measure.CalcSize(new GUIContent(word));
            Rect r = new Rect(tx, pos.y, sz.x + 10, sz.y);

            // Claw marks, leaning like "///".
            var m = GUI.matrix;
            float clawH = size * 1.05f;
            for (int i = 0; i < 3; i++)
            {
                Vector2 c = new Vector2(pos.x + clawW * 0.25f + i * clawW * 0.32f, pos.y + sz.y * 0.52f);
                GUIUtility.RotateAroundPivot(24f, c * UIStyle.Scale);
                var cr = new Rect(c.x - clawW * 0.13f, c.y - clawH * 0.5f + (i - 1) * size * 0.05f, clawW * 0.26f, clawH);
                UIStyle.Icon(new Rect(cr.x + 3, cr.y + 4, cr.width, cr.height), UIStyle.Claw, new Color(0f, 0f, 0f, 0.55f));
                UIStyle.Icon(cr, UIStyle.Claw, new Color(0.88f, 0.88f, 0.86f, 0.95f));
                GUI.matrix = m;
            }

            // Drop shadow + dark rim
            GUI.Label(new Rect(r.x + 6, r.y + 8, r.width, r.height), word, UIStyle.Label(size, new Color(0f, 0f, 0f, 0.5f), TextAnchor.UpperLeft, font));
            var rim = UIStyle.Label(size, new Color(0.05f, 0.05f, 0.06f, 0.95f), TextAnchor.UpperLeft, font);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                GUI.Label(new Rect(r.x + Mathf.Cos(a) * 2.5f, r.y + Mathf.Sin(a) * 2.5f, r.width, r.height), word, rim);
            }
            // Steel: darker base, bright upper band clipped by a group, thin specular line.
            GUI.Label(r, word, UIStyle.Label(size, new Color(0.52f, 0.53f, 0.55f), TextAnchor.UpperLeft, font));
            // Oswald: ascent 1.193em, cap height 0.81em -> caps span 25.8%..80.5% of the line.
            float capTop = r.y + sz.y * 0.258f, capH = sz.y * 0.547f;
            GUI.BeginGroup(new Rect(r.x, r.y, r.width, capTop - r.y + capH * 0.48f));
            GUI.Label(new Rect(0, 0, r.width, r.height), word, UIStyle.Label(size, new Color(0.86f, 0.86f, 0.85f), TextAnchor.UpperLeft, font));
            GUI.EndGroup();
            GUI.BeginGroup(new Rect(r.x, capTop + capH * 0.46f, r.width, 3));
            GUI.Label(new Rect(0, -(capTop + capH * 0.46f - r.y), r.width, r.height), word, UIStyle.Label(size, new Color(1f, 1f, 1f, 0.9f), TextAnchor.UpperLeft, font));
            GUI.EndGroup();
            return sz.x;
        }

        static bool MenuItem(Rect r, int index, string label, string subtitle, bool primary = false)
        {
            var e = Event.current;
            bool over = r.Contains(e.mousePosition);
            if (e.type == EventType.Repaint)
                hover[index] = Mathf.MoveTowards(hover[index], over ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            float h = Mathf.Max(hover[index], primary ? 0.55f : 0f);

            UIStyle.Icon(r, UIStyle.FadeRight, new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0.75f * h));
            UIStyle.Rect(new Rect(r.x, r.y, 4, r.height), new Color(0.55f, 0.75f, 1f, 0.25f + 0.75f * h));
            float slide = 8f * hover[index];

            var idx = UIStyle.Label(16, new Color(1f, 1f, 1f, 0.45f + 0.4f * h), TextAnchor.MiddleLeft, UIStyle.FontLight);
            GUI.Label(new Rect(r.x + 22 + slide, r.y, 40, r.height), (index + 1).ToString("00"), idx);
            var main = UIStyle.Label(30, Color.Lerp(new Color(0.85f, 0.86f, 0.87f), Color.white, h), TextAnchor.MiddleLeft, UIStyle.FontSemiBold);
            float textY = subtitle != null ? r.y - 9 : r.y;
            GUI.Label(new Rect(r.x + 70 + slide, textY, r.width - 80, r.height), UIStyle.Spaced(label), main);
            if (subtitle != null)
            {
                var subSt = UIStyle.Label(14, new Color(0.75f, 0.8f, 0.85f, 0.55f + 0.35f * h), TextAnchor.MiddleLeft, UIStyle.FontRegular);
                GUI.Label(new Rect(r.x + 72 + slide, r.y + 19, r.width - 80, r.height), subtitle.ToUpperInvariant(), subSt);
            }

            if (e.type == EventType.MouseDown && e.button == 0 && over)
            {
                e.Use();
                Sfx.Play2D("click");
                return true;
            }
            return false;
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
            GUI.Label(new Rect(panel.x + 40, panel.yMax - 130, panel.width - 80, 50),
                "WASD рух · Shift біг · Ctrl присід (на бігу — підкат) · Пробіл стрибок (у стіну — залізти) · V / Alt кувирок · у воді: Пробіл вгору, Ctrl вниз · ЛКМ атака · ПКМ приціл / кидок списа · R перезарядка · E дія · Tab крафт · I інвентар · H лікування · 1–9 зброя · F3 діагностика",
                UIStyle.Wrap(14, UIStyle.TextDim));
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
            if (UIStyle.Button(new Rect(x, y, 380, 90), "ПІДЗЕМЕЛЛЯ\n<size=13>велика долина, водоспад, катакомби й лабіринт</size>", Duel.Map == ArenaMap.Catacombs, true, 20)) Duel.Map = ArenaMap.Catacombs;
            if (UIStyle.Button(new Rect(x + 400, y, 380, 90), "ПЕЧЕРНИЙ ЛІС\n<size=13>ліс, струмок, печерний тунель, мости</size>", Duel.Map == ArenaMap.CaveForest, true, 20)) Duel.Map = ArenaMap.CaveForest;
            if (UIStyle.Button(new Rect(x + 800, y, 380, 90), "ФОРТЕЦЯ В КАНЬЙОНІ\n<size=13>кам'яна фортеця, пандуси, тунелі</size>", Duel.Map == ArenaMap.CanyonFortress, true, 20)) Duel.Map = ArenaMap.CanyonFortress;

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
                (ItemDatabase.ModernWeapons ? "Креслення з МАГАЗИНУ відкривають сучасну зброю." : "Камінь → рогатка → лук.\nОбсидіан гостріший за кремінь.") + "\nПредмети на карті з'являються знову.\nПеремога +150, поразка +40, вбивство +10 монет.\nПоки що суперники — боти.",
                UIStyle.Wrap(13, UIStyle.TextDim));

            if (UIStyle.Button(new Rect(panel.xMax - 420, panel.yMax - 80, 180, 50), "НАЗАД", false, true, 18)) UIManager.Instance.Show(UIScreen.MainMenu);
            if (UIStyle.Button(new Rect(panel.xMax - 220, panel.yMax - 80, 180, 50), "У БІЙ", true, true, 20)) GameManager.Instance.StartArena(Duel);
        }

        // ------------------------------------------------------------------ servers
        static ServerRecord ServerDraft = new ServerRecord();
        static readonly string[] MapNames = { "ПЕЧЕРНИЙ ЛІС", "ФОРТЕЦЯ В КАНЬЙОНІ", "ПІДЗЕМЕЛЛЯ" };   // indexed by ArenaMap
        static Vector2 historyScroll;

        static string ModeName(int opponents) => opponents == 1 ? "дуель 1 на 1" : $"усі проти всіх · {opponents + 1}";

        /// <summary>Starts a match from a server entry. Networking is not in yet, so the match runs locally with bots.</summary>
        static void LaunchServer(ServerRecord server)
        {
            Profile.Current.RememberServer(server);
            var settings = new ArenaSettings
            {
                Map = (ArenaMap)server.Map,
                Opponents = server.Opponents,
                ScoreToWin = server.ScoreToWin,
                Primary = Duel.Primary,
                Secondary = Duel.Secondary,
                Melee = Duel.Melee,
                Difficulty = Duel.Difficulty,
            };
            GameManager.Instance.StartArena(settings);
        }

        public static void DrawCreateServer()
        {
            float W = UIStyle.Width, H = UIStyle.Height;
            var s = ServerDraft;
            UIStyle.Rect(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.6f));
            Rect panel = new Rect(W * 0.5f - 600, H * 0.5f - 380, 1200, 760);
            UIStyle.PanelBox(panel);
            UIStyle.Text(new Rect(panel.x + 40, panel.y + 26, 600, 40), UIStyle.Spaced("Створити сервер"), 30, Color.white, TextAnchor.MiddleLeft, true);
            UIStyle.Text(new Rect(panel.x + 40, panel.y + 66, 900, 24), UIStyle.Spaced(ModeName(s.Opponents) + $"   ·   до {s.ScoreToWin} вбивств"), 15, UIStyle.TextDim);

            float x = panel.x + 40, y = panel.y + 120;
            UIStyle.Text(new Rect(x, y, 300, 24), UIStyle.Spaced("Назва сервера"), 14, UIStyle.TextDim);
            y += 30;
            Rect field = new Rect(x, y, 700, 50);
            UIStyle.Rect(field, new Color(1f, 1f, 1f, 0.06f));
            UIStyle.Outline(field, UIStyle.Line);
            var fieldStyle = UIStyle.Label(20, Color.white, TextAnchor.MiddleLeft, UIStyle.FontRegular);
            fieldStyle.padding = new RectOffset(16, 16, 0, 0);
            GUI.skin.settings.cursorColor = Color.white;
            s.Name = GUI.TextField(field, s.Name ?? "", 32, fieldStyle);

            y += 80;
            UIStyle.Text(new Rect(x, y, 300, 24), UIStyle.Spaced("Карта"), 14, UIStyle.TextDim);
            y += 30;
            ArenaMap[] maps = { ArenaMap.Catacombs, ArenaMap.CaveForest, ArenaMap.CanyonFortress };
            for (int i = 0; i < maps.Length; i++)
                if (UIStyle.Button(new Rect(x + i * 370, y, 360, 60), MapNames[(int)maps[i]], s.Map == (int)maps[i], true, 18)) s.Map = (int)maps[i];

            y += 90;
            UIStyle.Text(new Rect(x, y, 300, 24), UIStyle.Spaced("Режим"), 14, UIStyle.TextDim);
            UIStyle.Text(new Rect(x + 560, y, 300, 24), UIStyle.Spaced("До перемоги"), 14, UIStyle.TextDim);
            y += 30;
            int[] counts = { 1, 3, 5, 9 };
            string[] countLabels = { "ДУЕЛЬ 1×1", "ВСІ — 4", "ВСІ — 6", "ВСІ — 10" };
            for (int i = 0; i < counts.Length; i++)
                if (UIStyle.Button(new Rect(x + i * 132, y, 124, 44), countLabels[i], s.Opponents == counts[i], true, 15))
                {
                    s.Opponents = counts[i];
                    s.MaxPlayers = counts[i] + 1;
                }
            int[] scores = { 5, 10, 20 };
            for (int i = 0; i < scores.Length; i++)
                if (UIStyle.Button(new Rect(x + 560 + i * 132, y, 124, 44), $"{scores[i]} ВБИВСТВ", s.ScoreToWin == scores[i], true, 15)) s.ScoreToWin = scores[i];

            y += 80;
            GUI.Label(new Rect(x, y, panel.width - 80, 80),
                $"Гравців: до {s.MaxPlayers}   ·   порт {s.Port}\n" +
                "Мережева гра ще в розробці: поки сервер запускається на цьому комп'ютері, вільні місця займають боти.\n" +
                "Сервер зберігається в «Історії серверів», звідти його можна запустити знову.",
                UIStyle.Wrap(14, UIStyle.TextDim));

            bool nameOk = !string.IsNullOrWhiteSpace(s.Name);
            if (UIStyle.Button(new Rect(panel.xMax - 420, panel.yMax - 80, 180, 50), "НАЗАД", false, true, 18)) UIManager.Instance.Show(UIScreen.MainMenu);
            if (UIStyle.Button(new Rect(panel.xMax - 220, panel.yMax - 80, 180, 50), "СТВОРИТИ", true, nameOk, 20))
            {
                s.Name = s.Name.Trim();
                LaunchServer(s.Copy());
            }
        }

        public static void DrawServerHistory()
        {
            float W = UIStyle.Width, H = UIStyle.Height;
            var history = Profile.Current.ServerHistory;
            UIStyle.Rect(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.6f));
            Rect panel = new Rect(W * 0.5f - 600, H * 0.5f - 400, 1200, 800);
            UIStyle.PanelBox(panel);
            UIStyle.Text(new Rect(panel.x + 40, panel.y + 26, 600, 40), UIStyle.Spaced("Історія серверів"), 30, Color.white, TextAnchor.MiddleLeft, true);
            UIStyle.Text(new Rect(panel.x + 40, panel.y + 66, 900, 24), UIStyle.Spaced("сервери, які ти створював"), 15, UIStyle.TextDim);

            Rect list = new Rect(panel.x + 40, panel.y + 120, panel.width - 80, panel.height - 230);
            if (history.Count == 0)
            {
                UIStyle.Text(new Rect(list.x, list.y + 40, list.width, 40), "Тут поки порожньо. Створи сервер у головному меню.", 20, UIStyle.TextDim, TextAnchor.MiddleCenter);
            }
            else
            {
                const float rowH = 84;
                Rect content = new Rect(0, 0, list.width - 20, history.Count * rowH);
                historyScroll = GUI.BeginScrollView(list, historyScroll, content);
                ServerRecord launch = null, forget = null;
                for (int i = 0; i < history.Count; i++)
                {
                    var srv = history[i];
                    Rect row = new Rect(0, i * rowH, content.width, rowH - 10);
                    UIStyle.Rect(row, new Color(1f, 1f, 1f, 0.04f));
                    UIStyle.Outline(row, UIStyle.Line);
                    UIStyle.Text(new Rect(row.x + 20, row.y + 8, 600, 34), srv.Name, 22, Color.white, TextAnchor.MiddleLeft, true);
                    string map = srv.Map >= 0 && srv.Map < MapNames.Length ? MapNames[srv.Map] : "?";
                    UIStyle.Text(new Rect(row.x + 20, row.y + 42, 760, 24),
                        $"{map.ToLowerInvariant()}   ·   {ModeName(srv.Opponents)}   ·   до {srv.ScoreToWin} вбивств   ·   востаннє {srv.LastPlayed}", 14, UIStyle.TextDim, TextAnchor.MiddleLeft);
                    if (UIStyle.Button(new Rect(row.xMax - 330, row.y + 14, 170, 46), "ЗАПУСТИТИ", true, true, 16)) launch = srv;
                    if (UIStyle.Button(new Rect(row.xMax - 150, row.y + 14, 130, 46), "ВИДАЛИТИ", false, true, 14)) forget = srv;
                }
                GUI.EndScrollView();
                if (forget != null) Profile.Current.ForgetServer(forget);
                if (launch != null) LaunchServer(launch.Copy());
            }

            if (UIStyle.Button(new Rect(panel.xMax - 420, panel.yMax - 80, 180, 50), "НАЗАД", false, true, 18)) UIManager.Instance.Show(UIScreen.MainMenu);
            if (UIStyle.Button(new Rect(panel.xMax - 220, panel.yMax - 80, 180, 50), "НОВИЙ", false, true, 18))
            {
                ServerDraft = new ServerRecord();
                UIManager.Instance.Show(UIScreen.CreateServer);
            }
        }

        static string Choice(ref float y, float x, string title, string[] options, string current)
        {
            UIStyle.Text(new Rect(x, y, 300, 24), UIStyle.Spaced(title), 14, UIStyle.TextDim);
            y += 30;
            float bx = x;
            foreach (var id in options)
            {
                if (bx + 180 > x + 1200) { bx = x; y += 58; }
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
