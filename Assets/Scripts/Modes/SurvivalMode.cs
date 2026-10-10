using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Training / story mode in the valley: a short Half-Life-style objective chain that teaches
    /// gathering, crafting, hunting, trading blueprints and ends at the tower.
    /// </summary>
    public class SurvivalMode : ModeBase
    {
        enum Step { Gather, CraftSpear, Wolves, FindCave, Mammoth, Trade, Camp, Tower, Done }

        ValleyLevel level;
        PlayerController player;
        Step step;
        int wolfKills;
        int campKills;
        string lastKiller;
        float ambient = 1f;
        Color baseSky, baseEquator, baseGround, baseFog;

        public override string ObjectiveHeader => "Chapter 1 · Valley of the Tower";

        public override string ObjectiveText
        {
            get
            {
                switch (step)
                {
                    case Step.Gather: return $"Gather 3 Wood and 2 Stone  ({Mathf.Min(3, player.Inventory.Get(Resource.Wood))}/3, {Mathf.Min(2, player.Inventory.Get(Resource.Stone))}/2)";
                    case Step.CraftSpear: return "Craft a Spear  [Tab]";
                    case Step.Wolves: return $"Hunt the wolves by the river  ({wolfKills}/{level.Wolves.Count})";
                    case Step.FindCave: return "Find the cave in the western cliffs";
                    case Step.Mammoth: return "Slay the cave mammoth";
                    case Step.Trade: return "Trade with the Old One deep in the cave";
                    case Step.Camp: return $"Break through the rival camp  ({campKills}/{level.Campers.Count})";
                    case Step.Tower: return "Reach the Tower";
                    default: return "Chapter complete";
                }
            }
        }

        public override string ObjectiveHint
        {
            get
            {
                switch (step)
                {
                    case Step.Gather: return "Walk over sticks and stones, or hit trees and rocks.";
                    case Step.CraftSpear: return "LMB thrusts, hold RMB to throw — then pick it back up.";
                    case Step.Wolves: return "Wolves fear fire. Knife kills give extra hide.";
                    case Step.Mammoth: return "Keep your distance. Throw spears, shoot arrows, use the pillars.";
                    case Step.Trade: return "Sell hides & bones, buy the Workbench blueprint, build modern weapons.";
                    case Step.Camp: return "Archers on the flanks. Campfires heal.";
                    default: return "";
                }
            }
        }

        public void Begin()
        {
            level = ValleyLevel.Build(transform, true);
            player = PlayerController.Spawn(transform, level.PlayerStart + Vector3.up * 0.3f, 0f);
            GameManager.Instance.SetRespawn(level.PlayerStart + Vector3.up * 0.3f);
            player.Health.Died += OnPlayerDied;

            GameEvents.CreatureKilled += OnKill;
            GameEvents.ZoneEntered += OnZone;
            GameEvents.Interacted += OnInteract;

            baseSky = RenderSettings.ambientSkyColor;
            baseEquator = RenderSettings.ambientEquatorColor;
            baseGround = RenderSettings.ambientGroundColor;
            baseFog = RenderSettings.fogColor;
            Inventory.Notify("Chapter 1 · Valley of the Tower", UIStyle.Gold);
        }

        void OnPlayerDied(DamageInfo info)
        {
            lastKiller = NameOf(info.Attacker);
            deadTimer = 4f;
        }

        void OnKill(Creature c, DamageInfo info)
        {
            if (level.Wolves.Contains(c)) wolfKills++;
            if (level.Campers.Contains(c)) campKills++;
            if (c == level.Mammoth && step <= Step.Mammoth) Advance(Step.Trade);
        }

        void OnZone(string id)
        {
            if (id == "cave" && step <= Step.FindCave) Advance(Step.Mammoth);
            if (id == "tower" && step >= Step.Camp) Complete();
        }

        void OnInteract(string id)
        {
            if (id == "trader" && step == Step.Trade) Advance(Step.Camp);
        }

        void Advance(Step next)
        {
            if (next <= step) return;
            step = next;
            Sfx.Play2D("objective", 0.8f);
        }

        void Complete()
        {
            if (step == Step.Done) return;
            step = Step.Done;
            Profile.Current.ChapterOneComplete = true;
            Profile.Current.Earn(300);
            Sfx.Play2D("objective", 1f, 0.8f);
            UIManager.Instance.ShowResult("CHAPTER 1 COMPLETE",
                "The tower hums. Its door is sealed... for now.\nFrom stone to steel.\n\n+300 coins");
        }

        void Update()
        {
            if (player == null) return;

            switch (step)
            {
                case Step.Gather:
                    if (player.Inventory.HasWeapon("spear")) Advance(Step.Wolves);
                    else if (player.Inventory.Get(Resource.Wood) >= 3 && player.Inventory.Get(Resource.Stone) >= 2) Advance(Step.CraftSpear);
                    break;
                case Step.CraftSpear:
                    if (player.Inventory.HasWeapon("spear")) Advance(Step.Wolves);
                    break;
                case Step.Wolves:
                    if (wolfKills >= level.Wolves.Count) Advance(Step.FindCave);
                    break;
                case Step.Camp:
                    if (campKills >= level.Campers.Count) Advance(Step.Tower);
                    break;
            }

            if (deadTimer >= 0f)
            {
                deadTimer -= Time.deltaTime;
                if (deadTimer < 0f) Respawn();
            }

            // Darken ambient light inside the cave so torches matter.
            Vector3 p = player.transform.position;
            bool inCave = level.CaveBounds.Contains(new Vector2(p.x, p.z)) && p.x < -89f && p.y < 12f;
            ambient = Mathf.MoveTowards(ambient, inCave ? 0.18f : 1f, Time.deltaTime * 0.8f);
            RenderSettings.ambientSkyColor = baseSky * ambient;
            RenderSettings.ambientEquatorColor = baseEquator * ambient;
            RenderSettings.ambientGroundColor = baseGround * ambient;
            RenderSettings.fogColor = Color.Lerp(new Color(0.05f, 0.05f, 0.06f), baseFog, ambient);
        }

        void Respawn()
        {
            int lost = Profile.Current.Coins / 10;
            if (lost > 0) Profile.Current.Spend(lost);
            player.ResetForRespawn();
            player.Teleport(GameManager.Instance.RespawnPoint, player.Yaw);
            Inventory.Notify(lost > 0 ? $"You lost {lost} coins" : "You wake up...", UIStyle.Bad);
        }

        public override bool TryGetRadarTarget(out Vector3 pos, out Color color)
        {
            color = new Color(1f, 0.6f, 0.2f);
            pos = default;
            switch (step)
            {
                case Step.Wolves:
                    foreach (var w in level.Wolves) if (w != null && !w.IsDead) { pos = w.transform.position; return true; }
                    return false;
                case Step.FindCave: pos = level.CaveEntrance; return true;
                case Step.Mammoth: if (level.Mammoth != null) { pos = level.Mammoth.transform.position; return true; } return false;
                case Step.Trade: pos = level.TraderPos; return true;
                case Step.Camp: pos = level.CampCenter; return true;
                case Step.Tower: pos = level.TowerPos; return true;
                default: return false;
            }
        }

        public override void DrawHud() => DrawDeathOverlay(lastKiller);
    }
}
