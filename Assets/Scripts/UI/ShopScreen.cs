using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>The trader's shop: BLUEPRINTS / TOOLS / WEAPONS / GEAR / MISC / SELL, like the concept board.</summary>
    public static class ShopScreen
    {
        public static string Tab = "КРЕСЛЕННЯ";
        static Vector2 scroll;

        /// <param name="menuOnly">From the main menu only blueprints can be bought (no run inventory).</param>
        public static void Draw(bool menuOnly, System.Action close)
        {
            float W = UIStyle.Width, H = UIStyle.Height;
            UIStyle.Rect(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.55f));
            Rect panel = new Rect(W * 0.5f - 640, H * 0.5f - 380, 1280, 760);
            UIStyle.PanelBox(panel);

            UIStyle.Text(new Rect(panel.x + 36, panel.y + 26, 400, 40), UIStyle.Spaced("Магазин"), 28, Color.white, TextAnchor.MiddleLeft, true);
            UIStyle.Text(new Rect(panel.x + 36, panel.y + 64, 600, 24), menuOnly ? "Креслення купуються назавжди: бери нову зброю в дуелі або створюй її на верстаку в тренуванні." : "Старійшина торгує дивними кресленнями з вежі.", 16, UIStyle.TextDim);
            UIStyle.Text(new Rect(panel.xMax - 336, panel.y + 26, 300, 40), $"<color=#ffd15a>◆</color> {Profile.Current.Coins}", 28, Color.white, TextAnchor.MiddleRight, true);

            var player = PlayerController.Instance;
            string[] tabs = menuOnly ? new[] { "КРЕСЛЕННЯ" } : ItemDatabase.ShopTabs;
            if (menuOnly) Tab = "КРЕСЛЕННЯ";
            float ty = panel.y + 120;
            foreach (var t in tabs)
            {
                if (UIStyle.Button(new Rect(panel.x + 36, ty, 200, 44), t, Tab == t, true, 16)) Tab = t;
                ty += 52;
            }

            Rect area = new Rect(panel.x + 270, panel.y + 120, panel.width - 306, panel.height - 190);
            var entries = ItemDatabase.Shop.FindAll(e => e.Tab == Tab);
            int cols = 3;
            float cw = (area.width - (cols - 1) * 16) / cols, ch = 170;
            int rows = Mathf.CeilToInt(entries.Count / (float)cols);
            Rect view = new Rect(0, 0, area.width - 20, rows * (ch + 16));
            scroll = GUI.BeginScrollView(area, scroll, view, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                Rect r = new Rect((i % cols) * (cw + 16), (i / cols) * (ch + 16), cw, ch);
                DrawCard(r, e, player);
            }
            GUI.EndScrollView();

            if (UIStyle.Button(new Rect(panel.xMax - 196, panel.yMax - 60, 160, 40), "ЗАКРИТИ  [Esc]", false, true, 16)) close();
            UIStyle.Text(new Rect(panel.x + 36, panel.yMax - 56, 700, 30), UIStyle.Spaced("Від каменю до сталі"), 14, UIStyle.TextDim, TextAnchor.MiddleLeft);
        }

        static void DrawCard(Rect r, ShopEntry e, PlayerController player)
        {
            bool blueprint = e.Kind == ShopKind.Blueprint;
            UIStyle.Rect(r, blueprint ? new Color(0.09f, 0.14f, 0.22f, 0.95f) : new Color(0.12f, 0.12f, 0.12f, 0.95f));
            UIStyle.Outline(r, blueprint ? new Color(0.45f, 0.65f, 1f, 0.35f) : UIStyle.Line);
            if (blueprint)
            {
                // Faint blueprint grid
                for (float gx = r.x + 20; gx < r.xMax; gx += 20) UIStyle.Rect(new Rect(gx, r.y, 1, r.height), new Color(0.5f, 0.7f, 1f, 0.05f));
                for (float gy = r.y + 20; gy < r.yMax; gy += 20) UIStyle.Rect(new Rect(r.x, gy, r.width, 1), new Color(0.5f, 0.7f, 1f, 0.05f));
            }

            UIStyle.Text(new Rect(r.x + 14, r.y + 10, r.width - 28, 26), e.Name, 19, Color.white, TextAnchor.MiddleLeft, true);
            string price = e.Kind == ShopKind.SellResource ? $"+{e.Price} за шт." : e.Price.ToString();
            UIStyle.Text(new Rect(r.x + 14, r.y + 36, r.width - 28, 22), $"<color=#ffd15a>◆</color> {price}", 16, UIStyle.TextColor, TextAnchor.MiddleLeft);

            string desc = e.Description;
            if (desc == null && e.Kind == ShopKind.Weapon) desc = ItemDatabase.Weapon(e.WeaponId)?.Description;
            if (e.Kind == ShopKind.SellResource && player != null) desc = $"У тебе: {player.Inventory.Get(e.Resource)}.";
            if (desc != null) GUI.Label(new Rect(r.x + 14, r.y + 62, r.width - 28, 56), desc, UIStyle.Wrap(14, UIStyle.TextDim));

            string status;
            bool can = CanBuy(e, player, out status);
            Rect btn = new Rect(r.x + 14, r.yMax - 46, r.width - 28, 34);
            if (status != null)
            {
                UIStyle.Text(btn, status, 15, status == "ПРИДБАНО" ? UIStyle.Good : UIStyle.TextDim, TextAnchor.MiddleCenter, true);
                return;
            }
            if (e.Kind == ShopKind.SellResource)
            {
                Rect half = new Rect(btn.x, btn.y, btn.width * 0.48f, btn.height);
                Rect half2 = new Rect(btn.x + btn.width * 0.52f, btn.y, btn.width * 0.48f, btn.height);
                if (UIStyle.Button(half, "ПРОДАТИ 1", false, can, 15)) Sell(e, player, 1);
                if (UIStyle.Button(half2, "ПРОДАТИ ВСЕ", false, can, 15)) Sell(e, player, player.Inventory.Get(e.Resource));
                return;
            }
            if (UIStyle.Button(btn, can ? "КУПИТИ" : "БРАКУЄ МОНЕТ", can, can, 15)) Buy(e, player);
        }

        static bool CanBuy(ShopEntry e, PlayerController player, out string status)
        {
            status = null;
            switch (e.Kind)
            {
                case ShopKind.Blueprint:
                    if (Profile.Current.HasBlueprint(e.Id)) { status = "ПРИДБАНО"; return false; }
                    break;
                case ShopKind.Weapon:
                    if (player == null) { status = "ЛИШЕ В ТРЕНУВАННІ"; return false; }
                    if (player.Inventory.HasWeapon(e.WeaponId)) { status = "ПРИДБАНО"; return false; }
                    break;
                case ShopKind.SellResource:
                    return player != null && player.Inventory.Get(e.Resource) > 0;
                default:
                    if (player == null) { status = "ЛИШЕ В ТРЕНУВАННІ"; return false; }
                    break;
            }
            return Profile.Current.Coins >= e.Price;
        }

        static void Buy(ShopEntry e, PlayerController player)
        {
            if (!Profile.Current.Spend(e.Price)) { Sfx.Play2D("deny"); return; }
            switch (e.Kind)
            {
                case ShopKind.Blueprint:
                    Profile.Current.AddBlueprint(e.Id);
                    Inventory.Notify($"Отримано креслення: {e.Name}", UIStyle.Gold);
                    break;
                case ShopKind.Weapon: player.Inventory.AddWeapon(e.WeaponId); break;
                case ShopKind.Ammo: player.Inventory.AddAmmo(e.Ammo, e.Amount); break;
                case ShopKind.Resource: player.Inventory.Add(e.Resource, e.Amount); break;
                case ShopKind.Armor: player.Health.AddArmor(e.Amount); Inventory.Notify($"+{e.Amount} броні", UIStyle.Accent); break;
            }
            Sfx.Play2D("buy");
            GameEvents.RaisePurchase(e);
        }

        static void Sell(ShopEntry e, PlayerController player, int count)
        {
            if (player == null || count <= 0) return;
            count = Mathf.Min(count, player.Inventory.Get(e.Resource));
            player.Inventory.Add(e.Resource, -count, false);
            Profile.Current.Earn(e.Price * count);
            Sfx.Play2D("coin");
            Inventory.Notify($"Продано: {ItemDatabase.ResourceName(e.Resource)} ×{count} за {e.Price * count} монет", UIStyle.Gold);
        }
    }

    /// <summary>Crafting: primitive recipes anywhere (Tab), modern recipes at the workbench.</summary>
    public static class CraftingScreen
    {
        static string category;
        static Vector2 scroll;
        static readonly string[] PrimitiveCats = { "Кам'яні знаряддя", "Кістяна зброя", "Вогонь", "Одяг", "Притулок" };

        public static void Draw(bool workbench, System.Action close)
        {
            var player = PlayerController.Instance;
            if (player == null) { close(); return; }
            float W = UIStyle.Width, H = UIStyle.Height;
            UIStyle.Rect(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.55f));
            Rect panel = new Rect(W * 0.5f - 620, H * 0.5f - 370, 1240, 740);
            UIStyle.PanelBox(panel);

            UIStyle.Text(new Rect(panel.x + 36, panel.y + 26, 500, 40), UIStyle.Spaced(workbench ? "Верстак" : "Виготовлення"), 28, Color.white, TextAnchor.MiddleLeft, true);

            // Resources row
            float rx = panel.x + 36;
            foreach (Resource res in System.Enum.GetValues(typeof(Resource)))
            {
                string s = $"{ItemDatabase.ResourceName(res)} <b>{player.Inventory.Get(res)}</b>";
                UIStyle.Text(new Rect(rx, panel.y + 70, 170, 24), s, 16, UIStyle.TextColor);
                rx += 170;
            }

            string[] cats = workbench ? new[] { "Верстак" } : PrimitiveCats;
            if (category == null || System.Array.IndexOf(cats, category) < 0) category = cats[0];
            float ty = panel.y + 120;
            foreach (var c in cats)
            {
                if (UIStyle.Button(new Rect(panel.x + 36, ty, 220, 44), c.ToUpperInvariant(), category == c, true, 15)) category = c;
                ty += 52;
            }

            var recipes = ItemDatabase.Recipes.FindAll(r => r.Category == category);
            Rect area = new Rect(panel.x + 290, panel.y + 120, panel.width - 326, panel.height - 190);
            float rh = 96;
            Rect view = new Rect(0, 0, area.width - 20, recipes.Count * (rh + 10));
            scroll = GUI.BeginScrollView(area, scroll, view, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            for (int i = 0; i < recipes.Count; i++)
                DrawRecipe(new Rect(0, i * (rh + 10), view.width, rh), recipes[i], player);
            GUI.EndScrollView();

            if (UIStyle.Button(new Rect(panel.xMax - 196, panel.yMax - 60, 160, 40), "ЗАКРИТИ  [Esc]", false, true, 16)) close();
            if (!workbench)
                UIStyle.Text(new Rect(panel.x + 36, panel.yMax - 56, 800, 30), "Сучасну зброю виготовляють на верстаку за кресленнями від торговця.", 15, UIStyle.TextDim, TextAnchor.MiddleLeft);
        }

        static void DrawRecipe(Rect r, Recipe recipe, PlayerController player)
        {
            UIStyle.Rect(r, new Color(1f, 1f, 1f, 0.04f));
            UIStyle.Outline(r, UIStyle.Line);
            bool hasBp = recipe.RequiredBlueprint == null || Profile.Current.HasBlueprint(recipe.RequiredBlueprint);
            UIStyle.Text(new Rect(r.x + 16, r.y + 10, 400, 28), recipe.Name, 20, hasBp ? Color.white : UIStyle.TextDim, TextAnchor.MiddleLeft, true);
            if (recipe.Description != null)
                UIStyle.Text(new Rect(r.x + 16, r.y + 40, r.width - 260, 22), recipe.Description, 14, UIStyle.TextDim);

            float cx = r.x + 16;
            foreach (var kv in recipe.Cost)
            {
                bool enough = player.Inventory.Get(kv.Key) >= kv.Value;
                string s = $"{ItemDatabase.ResourceName(kv.Key)} {player.Inventory.Get(kv.Key)}/{kv.Value}";
                UIStyle.Text(new Rect(cx, r.y + 64, 200, 22), s, 15, enough ? UIStyle.Good : UIStyle.Bad);
                cx += 200;
            }

            Rect btn = new Rect(r.xMax - 200, r.y + 26, 180, 44);
            if (!hasBp)
            {
                var bp = ItemDatabase.Blueprint(recipe.RequiredBlueprint);
                UIStyle.Text(btn, "ПОТРІБНЕ КРЕСЛЕННЯ\n" + (bp != null ? bp.Name : recipe.RequiredBlueprint), 14, UIStyle.TextDim, TextAnchor.MiddleCenter);
                return;
            }
            bool can = player.Inventory.CanAfford(recipe.Cost) && !(recipe.GivesWeapon != null && player.Inventory.HasWeapon(recipe.GivesWeapon));
            string label = recipe.GivesWeapon != null && player.Inventory.HasWeapon(recipe.GivesWeapon) ? "ПРИДБАНО" : "СТВОРИТИ";
            if (UIStyle.Button(btn, label, can, can, 16)) Crafting.Craft(recipe, player);
        }
    }

    public static class Crafting
    {
        public static bool Craft(Recipe r, PlayerController player)
        {
            var inv = player.Inventory;
            if (!inv.CanAfford(r.Cost)) { Sfx.Play2D("deny"); return false; }
            if (r.RequiredBlueprint != null && !Profile.Current.HasBlueprint(r.RequiredBlueprint)) return false;
            inv.Spend(r.Cost);
            if (r.GivesWeapon != null) inv.AddWeapon(r.GivesWeapon);
            if (r.GivesAmmo != AmmoType.None) inv.AddAmmo(r.GivesAmmo, r.AmmoAmount);
            if (r.GivesArmor > 0)
            {
                player.Health.AddArmor(r.GivesArmor);
                Inventory.Notify($"+{r.GivesArmor} броні", UIStyle.Accent);
            }
            if (r.Places != null)
            {
                Vector3 fwd = player.transform.forward;
                Vector3 pos = player.transform.position + fwd * 2.5f;
                if (PhysicsUtil.Raycast(pos + Vector3.up * 3f, Vector3.down, 10f, player.transform, out var hit)) pos = hit.point;
                var parent = GameManager.Instance != null ? GameManager.Instance.LevelRoot : null;
                if (r.Places == "campfire") Campfire.Create(parent, pos);
                else Campfire.CreateShelter(parent, pos, player.Yaw + 180f);
                Inventory.Notify($"Збудовано: {r.Name}", UIStyle.Gold);
            }
            Sfx.Play2D("buy", 0.8f, 0.8f);
            GameEvents.RaiseCraft(r);
            return true;
        }
    }
}
