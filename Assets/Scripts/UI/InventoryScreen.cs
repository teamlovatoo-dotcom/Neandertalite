using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Inventory (key I, or Tab in the arena): the nine hotbar cells, details of the chosen weapon, ammo and
    /// resources. Click a cell and then another one (or press 1-9) to swap them; click the same cell twice to take it.
    /// The game keeps running, like in modern shooters.
    /// </summary>
    public static class InventoryScreen
    {
        static int picked = -1;

        public static void Reset() => picked = -1;

        static string KindName(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.Melee: return "Ближній бій";
                case WeaponKind.Spear: return "Спис: удар і кидок";
                case WeaponKind.Bow: return "Дальній бій";
                case WeaponKind.Throw: return "Метальна";
                case WeaponKind.Firearm: return "Вогнепальна";
                default: return "";
            }
        }

        public static void Draw(System.Action close)
        {
            var p = PlayerController.Instance;
            if (p == null) { close(); return; }
            var inv = p.Inventory;
            inv.EnsureHotbar();
            float W = UIStyle.Width, H = UIStyle.Height;
            UIStyle.Rect(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.5f));
            Rect panel = new Rect(W * 0.5f - 640, H * 0.5f - 360, 1280, 700);
            UIStyle.PanelBox(panel);

            UIStyle.Text(new Rect(panel.x + 36, panel.y + 24, 500, 40), UIStyle.Spaced("Інвентар"), 28, Color.white, TextAnchor.MiddleLeft, true);
            UIStyle.Coins(new Rect(panel.xMax - 236, panel.y + 30, 200, 30), Profile.Current.Coins.ToString(), 22, Color.white, true);

            // Hotbar cells
            UIStyle.Text(new Rect(panel.x + 36, panel.y + 78, 600, 22), UIStyle.Spaced("Швидкі слоти 1–9"), 14, UIStyle.TextDim);
            float cell = 116f, gap = 14f;
            float x0 = panel.x + (panel.width - (Inventory.HotbarSize * (cell + gap) - gap)) * 0.5f, y0 = panel.y + 108;
            string cur = p.Weapons.CurrentId;
            var bar = inv.Hotbar;
            for (int i = 0; i < Inventory.HotbarSize; i++)
            {
                Rect r = new Rect(x0 + i * (cell + gap), y0, cell, cell);
                string id = i < bar.Count ? bar[i] : "";
                Hud.DrawHotbarCell(p, id, i, r, cur, false, picked == i);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Click(p, i);
            }

            // Number keys move the picked weapon to that key.
            if (picked >= 0)
                for (int k = 1; k <= Inventory.HotbarSize; k++)
                    if (GameInput.SlotDown(k)) { inv.SwapHotbar(picked, k - 1); picked = -1; Sfx.Play2D("click"); break; }

            // Details of the picked (or held) weapon
            Rect info = new Rect(panel.x + 36, y0 + cell + 34, 700, panel.yMax - (y0 + cell + 34) - 70);
            UIStyle.Rect(info, new Color(1f, 1f, 1f, 0.03f));
            string showId = picked >= 0 && picked < bar.Count && bar[picked] != "" ? bar[picked] : cur;
            var d = showId != null ? ItemDatabase.Weapon(showId) : null;
            if (d != null)
            {
                var icon = WeaponIcons.Get(d.Id);
                if (icon != null) GUI.DrawTexture(new Rect(info.x + 16, info.y + 16, 150, 150), icon, ScaleMode.ScaleToFit, true);
                float tx = info.x + 190;
                UIStyle.Text(new Rect(tx, info.y + 14, 480, 36), d.Name.ToUpperInvariant(), 26, Color.white, TextAnchor.MiddleLeft, true);
                UIStyle.Text(new Rect(tx, info.y + 50, 480, 22), KindName(d.Kind), 15, UIStyle.Gold);
                float sy = info.y + 82;
                Stat(tx, ref sy, "Шкода", d.Pellets > 1 ? $"{d.Damage} × {d.Pellets}" : d.Damage.ToString("0"));
                if (d.Range > 0f) Stat(tx, ref sy, "Дальність удару", $"{d.Range:0.0} м");
                if (d.Cooldown > 0f) Stat(tx, ref sy, "Швидкість", $"{1f / d.Cooldown:0.0} уд/с");
                if (d.Ammo != AmmoType.None) Stat(tx, ref sy, ItemDatabase.AmmoName(d.Ammo), inv.GetAmmo(d.Ammo).ToString());
                UIStyle.Text(new Rect(info.x + 16, info.y + 190, info.width - 32, info.height - 250), d.Description, 16, UIStyle.TextColor);
                if (!inv.Holds(d.Id))
                    UIStyle.Text(new Rect(info.x + 16, info.yMax - 50, 400, 36), "Немає в руках — підбери, щоб знову користуватися.", 15, UIStyle.Bad, TextAnchor.MiddleLeft);
                else if (d.Id != cur && UIStyle.Button(new Rect(info.xMax - 236, info.yMax - 56, 220, 42), "ВЗЯТИ В РУКИ", false, true, 16))
                {
                    p.Weapons.SwitchTo(d.Id);
                    picked = -1;
                }
            }

            // Ammo and resources
            Rect side = new Rect(info.xMax + 30, info.y, panel.xMax - 36 - info.xMax - 30, info.height);
            float y = side.y;
            UIStyle.Text(new Rect(side.x, y, side.width, 24), UIStyle.Spaced("Боєприпаси"), 15, UIStyle.TextDim);
            y += 30;
            bool any = false;
            foreach (AmmoType a in System.Enum.GetValues(typeof(AmmoType)))
            {
                int n = inv.GetAmmo(a);
                if (a == AmmoType.None || n <= 0) continue;
                Row(side.x, ref y, side.width, ItemDatabase.AmmoName(a), n);
                any = true;
            }
            if (!any) { UIStyle.Text(new Rect(side.x, y, side.width, 24), "—", 16, UIStyle.TextDim); y += 26; }
            y += 18;
            UIStyle.Text(new Rect(side.x, y, side.width, 24), UIStyle.Spaced("Ресурси"), 15, UIStyle.TextDim);
            y += 30;
            any = false;
            foreach (Resource res in System.Enum.GetValues(typeof(Resource)))
            {
                int n = inv.Get(res);
                if (n <= 0) continue;
                Row(side.x, ref y, side.width, ItemDatabase.ResourceName(res), n);
                any = true;
            }
            if (!any) UIStyle.Text(new Rect(side.x, y, side.width, 24), "—", 16, UIStyle.TextDim);

            UIStyle.Text(new Rect(panel.x + 36, panel.yMax - 50, panel.width - 72, 30),
                "Клацни слот, потім інший (або цифру 1–9) — поміняти місцями.  Двічі клацни — взяти в руки.  I / Esc — закрити.",
                15, UIStyle.TextDim, TextAnchor.MiddleCenter);
        }

        static void Click(PlayerController p, int i)
        {
            var inv = p.Inventory;
            if (picked < 0)
            {
                if (i < inv.Hotbar.Count && inv.Hotbar[i] != "") { picked = i; Sfx.Play2D("click"); }
                return;
            }
            if (picked == i)
            {
                string id = inv.Hotbar[i];
                if (inv.Holds(id)) p.Weapons.SwitchTo(id); else Sfx.Play2D("deny", 0.4f);
                picked = -1;
                return;
            }
            inv.SwapHotbar(picked, i);
            picked = -1;
            Sfx.Play2D("click");
        }

        static void Stat(float x, ref float y, string label, string value)
        {
            UIStyle.Text(new Rect(x, y, 220, 24), label, 16, UIStyle.TextDim);
            UIStyle.Text(new Rect(x + 220, y, 250, 24), value, 16, Color.white, TextAnchor.UpperLeft, true);
            y += 26;
        }

        static void Row(float x, ref float y, float w, string label, int n)
        {
            UIStyle.Text(new Rect(x, y, w - 60, 24), label, 17, UIStyle.TextColor);
            UIStyle.Text(new Rect(x, y, w, 24), n.ToString(), 17, Color.white, TextAnchor.UpperRight, true);
            y += 26;
        }
    }
}
