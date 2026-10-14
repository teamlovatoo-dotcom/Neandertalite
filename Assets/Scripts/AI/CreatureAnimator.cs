using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Neanderthal
{
    /// <summary>
    /// Humanoid animation clips found in <c>Resources/Animations</c> (e.g. Quaternius' free CC0 "Universal Animation
    /// Library" FBX files dropped there). Clips are sorted into roles by their names. If the folder is empty, or a role
    /// is missing, the game keeps its procedural animation - nothing breaks.
    /// </summary>
    public static class AnimLibrary
    {
        public static AnimationClip Idle, Walk, Run, Sprint;
        // Environment (Mixamo Env_*, Crouch_*, Swim*): loaded for bodies that jump, crouch, swim, climb, pick up or heal.
        public static AnimationClip Jump, Roll, Climb, Swim, SwimIdle, CrouchIdle, CrouchWalk, PickUp, Heal;
        public static readonly List<AnimationClip> Attacks = new List<AnimationClip>();
        public static readonly List<AnimationClip> Stabs = new List<AnimationClip>();
        public static readonly List<AnimationClip> Hits = new List<AnimationClip>();
        public static readonly List<AnimationClip> HeadHits = new List<AnimationClip>();
        public static readonly List<AnimationClip> Deaths = new List<AnimationClip>();
        public static readonly List<AnimationClip> Shoots = new List<AnimationClip>();
        static bool loaded;

        /// <summary>Enough clips to animate walking people.</summary>
        public static bool Ready { get { Load(); return Idle != null && Walk != null && Run != null; } }

        public static string Summary { get; private set; } = "кліпи не завантажені";

        static readonly string[] NotForward = { "left", "right", "back", "bwd", "strafe", "turn", "crouch", "injured", "zombie",
                                                 "swim", "crawl", "push", "carry", "sit", "drunk", "_l_", "_r_", "torch" };

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            var all = Resources.LoadAll<AnimationClip>("Animations");
            var human = new List<AnimationClip>();
            foreach (var c in all)
                if (c != null && c.isHumanMotion && !c.name.StartsWith("__preview__")) human.Add(c);
            if (human.Count == 0)
            {
                Summary = all.Length == 0 ? "у Resources/Animations немає кліпів" : $"знайдено {all.Length} кліпів, але жоден не Humanoid";
                return;
            }

            Idle = Pick(human, new[] { "idle" }, NotForward);
            Walk = Pick(human, new[] { "walk" }, NotForward);
            Run = Pick(human, new[] { "jog" }, NotForward) ?? Pick(human, new[] { "run" }, NotForward);
            Sprint = Pick(human, new[] { "sprint" }, NotForward);
            Jump = Pick(human, new[] { "jump" }, new string[0]);
            Roll = Pick(human, new[] { "roll" }, new string[0]);
            Climb = Pick(human, new[] { "climb" }, new string[0]);
            Swim = Pick(human, new[] { "swim" }, new[] { "tread" });
            SwimIdle = Pick(human, new[] { "tread" }, new string[0]);
            CrouchIdle = Pick(human, new[] { "crouch", "idle" }, new string[0]);
            CrouchWalk = Pick(human, new[] { "crouch", "walk" }, new string[0]);
            PickUp = Pick(human, new[] { "pickup" }, new string[0]);
            Heal = Pick(human, new[] { "heal" }, new string[0]);
            foreach (var c in human)
            {
                string n = c.name.ToLowerInvariant();
                bool recovery = n.Contains("_rec") || n.Contains("recover");
                bool locomotion = n.Contains("idle") || n.Contains("walk") || n.Contains("run") || n.Contains("jog") || n.Contains("sprint")
                                  || n.StartsWith("env_") || n.Contains("swim") || n.Contains("crouch");
                if (n.Contains("death") || n.Contains("dying") || n.Contains("_die")) Deaths.Add(c);
                else if (n.Contains("hit") && !n.Contains("hitch")) (n.Contains("head") ? HeadHits : Hits).Add(c);
                else if (locomotion || recovery) continue;
                else if (n.Contains("stab") || n.Contains("thrust") || n.Contains("spear") || n.Contains("pierce")) Stabs.Add(c);
                else if (n.Contains("sword") || n.Contains("attack") || n.Contains("slash") || n.Contains("punch") || n.Contains("melee")
                         || n.Contains("axe") || n.Contains("hook") || n.Contains("swing") || n.Contains("club")) Attacks.Add(c);
                else if (n.Contains("bow") || n.Contains("shoot") || n.Contains("archer")) Shoots.Add(c);
            }
            Summary = $"Humanoid-кліпів {human.Count}: стоїть {Name(Idle)}, іде {Name(Walk)}, біжить {Name(Run)}, спринт {Name(Sprint)}, " +
                      $"удари {Attacks.Count}, уколи {Stabs.Count}, влучання {Hits.Count + HeadHits.Count}, смерть {Deaths.Count}, лук {Shoots.Count}, " +
                      $"середовище {CountEnv()}/9";
            Debug.Log("[Анімації] " + Summary);
        }

        static string Name(AnimationClip c) => c != null ? c.name : "—";

        static int CountEnv()
        {
            int n = 0;
            foreach (var c in new[] { Jump, Roll, Climb, Swim, SwimIdle, CrouchIdle, CrouchWalk, PickUp, Heal }) if (c != null) n++;
            return n;
        }

        /// <summary>Best match: has every keyword, none of the excluded words, shortest name ("Idle_Loop" over "Idle_Talking").</summary>
        static AnimationClip Pick(List<AnimationClip> clips, string[] need, string[] exclude)
        {
            AnimationClip best = null;
            foreach (var c in clips)
            {
                string n = c.name.ToLowerInvariant();
                bool ok = true;
                foreach (var k in need) if (!n.Contains(k)) { ok = false; break; }
                if (!ok) continue;
                foreach (var k in exclude) if (n.Contains(k)) { ok = false; break; }
                if (!ok) continue;
                if (best == null || c.name.Length < best.name.Length) best = c;
            }
            return best;
        }

        public static AnimationClip Random(List<AnimationClip> list) => list.Count == 0 ? null : list[UnityEngine.Random.Range(0, list.Count)];
    }

    /// <summary>
    /// Drives one Neanderthal with animation clips through a small Playables mixer (no AnimatorController asset is
    /// needed, so it fits the "everything is built in code" setup): idle / walk / run / sprint blended by speed, plus
    /// one action slot for attacks, hit reactions and death. Flinches and head-look from <see cref="Creature"/> are
    /// layered on top in LateUpdate.
    /// </summary>
    public class CreatureAnimator
    {
        const int Idle = 0, Walk = 1, Run = 2, Sprint = 3, Action = 4;
        readonly Animator animator;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        readonly AnimationClipPlayable[] loco = new AnimationClipPlayable[4];
        readonly float[] refSpeed = { 0f, 1.4f, 3.6f, 6f };
        AnimationClipPlayable action;
        bool hasAction;
        float actionWeight, actionEnd = -1f;
        bool actionHold;                 // death: freeze on the last frame
        bool dying;
        readonly float[] weights = new float[4];

        public bool Active => graph.IsValid();

        CreatureAnimator(Animator a) { animator = a; }

        /// <summary>
        /// Build a humanoid avatar for the rigged model from its bone names (the FBX rest pose is a T-pose, which is
        /// what Unity expects) and hook up the clips. Must run before the code bends the arms down. Null if anything
        /// is missing - the caller then keeps the procedural animation.
        /// </summary>
        public static CreatureAnimator TryCreate(GameObject model)
        {
            if (!AnimLibrary.Ready) return null;
            try
            {
                var avatar = BuildAvatar(model);
                if (avatar == null || !avatar.isValid || !avatar.isHuman) return null;
                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var ca = new CreatureAnimator(animator);
                ca.BuildGraph();
                return ca;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Анімації] не вдалося підключити кліпи до моделі: " + e.Message);
                return null;
            }
        }

        static readonly (string human, string bone)[] BoneMap =
        {
            ("Hips", "Hips"), ("Spine", "Spine"), ("Chest", "Chest"), ("Neck", "Neck"), ("Head", "Head"),
            ("LeftShoulder", "Shoulder.L"), ("RightShoulder", "Shoulder.R"),
            ("LeftUpperArm", "UpperArm.L"), ("RightUpperArm", "UpperArm.R"),
            ("LeftLowerArm", "LowerArm.L"), ("RightLowerArm", "LowerArm.R"),
            ("LeftHand", "Hand.L"), ("RightHand", "Hand.R"),
            ("LeftUpperLeg", "UpperLeg.L"), ("RightUpperLeg", "UpperLeg.R"),
            ("LeftLowerLeg", "LowerLeg.L"), ("RightLowerLeg", "LowerLeg.R"),
            ("LeftFoot", "Foot.L"), ("RightFoot", "Foot.R"),
        };

        static Avatar BuildAvatar(GameObject model)
        {
            var names = new HashSet<string>();
            var skeleton = new List<SkeletonBone>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                names.Add(t.name);
                skeleton.Add(new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
            }
            var human = new List<HumanBone>();
            foreach (var (h, b) in BoneMap)
            {
                if (!names.Contains(b))
                {
                    // Shoulders and chest are optional for Unity; everything else is required.
                    if (h.Contains("Shoulder") || h == "Chest" || h == "Neck") continue;
                    return null;
                }
                human.Add(new HumanBone { humanName = h, boneName = b, limit = new HumanLimit { useDefaultValues = true } });
            }
            var desc = new HumanDescription
            {
                human = human.ToArray(),
                skeleton = skeleton.ToArray(),
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
            };
            var avatar = AvatarBuilder.BuildHumanAvatar(model, desc);
            avatar.name = "NeanderthalAvatar";
            return avatar;
        }

        void BuildGraph()
        {
            graph = PlayableGraph.Create("Neanderthal");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Body", animator);
            mixer = AnimationMixerPlayable.Create(graph, 5);
            output.SetSourcePlayable(mixer);
            var clips = new[] { AnimLibrary.Idle, AnimLibrary.Walk, AnimLibrary.Run, AnimLibrary.Sprint ?? AnimLibrary.Run };
            for (int i = 0; i < 4; i++)
            {
                loco[i] = AnimationClipPlayable.Create(graph, clips[i]);
                loco[i].SetApplyFootIK(true);
                graph.Connect(loco[i], 0, mixer, i);
                // Clips exported with root motion tell us how fast they walk; use that to keep feet from sliding.
                float avg = clips[i].averageSpeed.magnitude;
                if (i > 0 && avg > 0.3f) refSpeed[i] = avg;
            }
            mixer.SetInputWeight(Idle, 1f);
            graph.Play();
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        public void Reset()
        {
            dying = false;
            actionHold = false;
            actionEnd = -1f;
            actionWeight = 0f;
            if (Active) mixer.SetInputWeight(Action, 0f);
        }

        void PlayAction(AnimationClip clip, float speed, bool hold = false)
        {
            if (clip == null || !Active) return;
            if (hasAction)
            {
                graph.Disconnect(mixer, Action);
                action.Destroy();
            }
            action = AnimationClipPlayable.Create(graph, clip);
            action.SetApplyFootIK(true);
            action.SetSpeed(speed);
            action.SetTime(0);
            graph.Connect(action, 0, mixer, Action);
            hasAction = true;
            actionHold = hold;
            actionEnd = Time.time + clip.length / Mathf.Max(0.05f, speed);
        }

        /// <summary>One-shot environment action (jump, roll, pick up, heal...) in the action slot.</summary>
        public void PlayOnce(AnimationClip clip, float speed = 1f)
        {
            if (!dying) PlayAction(clip, speed);
        }

        /// <summary>Attack timed so the clip's swing (~45% in) lands when the creature deals its damage.</summary>
        public void Attack(float windup, bool stab)
        {
            var clip = AnimLibrary.Random(stab && AnimLibrary.Stabs.Count > 0 ? AnimLibrary.Stabs : AnimLibrary.Attacks);
            if (clip == null) return;
            PlayAction(clip, Mathf.Clamp(clip.length * 0.45f / Mathf.Max(0.1f, windup), 0.6f, 2.5f));
        }

        public void Shoot(float drawTime)
        {
            var clip = AnimLibrary.Random(AnimLibrary.Shoots);
            if (clip != null) PlayAction(clip, Mathf.Clamp(clip.length * 0.7f / Mathf.Max(0.1f, drawTime), 0.5f, 2f));
        }

        public void Hit(HitZone zone)
        {
            if (dying) return;
            var clip = AnimLibrary.Random(zone == HitZone.Head && AnimLibrary.HeadHits.Count > 0 ? AnimLibrary.HeadHits : AnimLibrary.Hits);
            if (clip != null) PlayAction(clip, 1.3f);
        }

        /// <summary>Locomotion blend by speed; returns false when this animator isn't running.</summary>
        public bool Drive(float dt, Creature c)
        {
            if (!Active) return false;
            float s = c.CurrentSpeed;
            var w = weights;
            System.Array.Clear(w, 0, 4);
            float walkS = refSpeed[Walk], runS = refSpeed[Run], sprintS = refSpeed[Sprint];
            if (s < 0.15f) w[Idle] = 1f;
            else if (s < walkS) { float k = s / walkS; w[Idle] = 1f - k; w[Walk] = k; }
            else if (s < runS) { float k = (s - walkS) / (runS - walkS); w[Walk] = 1f - k; w[Run] = k; }
            else if (AnimLibrary.Sprint != null && s < sprintS) { float k = (s - runS) / (sprintS - runS); w[Run] = 1f - k; w[Sprint] = k; }
            else w[AnimLibrary.Sprint != null && s >= sprintS ? Sprint : Run] = 1f;

            for (int i = 0; i < 4; i++)
            {
                // Play the gait at the speed the creature really moves (no moonwalking), and loop it ourselves in
                // case the FBX was imported without "Loop Time".
                double len = loco[i].GetAnimationClip().length;
                if (i > 0) loco[i].SetSpeed(Mathf.Clamp(s / refSpeed[i], 0.5f, 1.8f));
                if (len > 0.01 && loco[i].GetTime() > len) loco[i].SetTime(loco[i].GetTime() % len);
            }

            bool actionOn = hasAction && (actionHold || Time.time < actionEnd - 0.12f);
            actionWeight = Mathf.MoveTowards(actionWeight, actionOn ? 1f : 0f, dt / (actionOn ? 0.08f : 0.18f));
            float rest = 1f - actionWeight;
            for (int i = 0; i < 4; i++) mixer.SetInputWeight(i, w[i] * rest);
            mixer.SetInputWeight(Action, actionWeight);

            // The body pivot (used by the procedural lean and the fall) stays neutral; clips do the work.
            c.Body.Root.localRotation = Quaternion.identity;
            c.Body.Root.localPosition = Vector3.zero;
            return true;
        }

        /// <summary>Death clip, held on its last frame. False if there is no death clip (procedural fall instead).</summary>
        public bool DriveDeath(float dt, Creature c)
        {
            if (!Active) return false;
            if (!dying)
            {
                var clip = AnimLibrary.Random(AnimLibrary.Deaths);
                if (clip == null) { graph.Stop(); return false; }
                dying = true;
                PlayAction(clip, 1f, true);
            }
            if (hasAction && action.GetTime() >= action.GetAnimationClip().length) action.SetSpeed(0);
            actionWeight = Mathf.MoveTowards(actionWeight, 1f, dt / 0.1f);
            for (int i = 0; i < 4; i++) mixer.SetInputWeight(i, 1f - actionWeight);
            mixer.SetInputWeight(Action, actionWeight);
            c.Body.Root.localRotation = Quaternion.identity;
            return true;
        }

        /// <summary>Hit-stop: hold the pose for a few frames.</summary>
        public void SetPaused(bool paused)
        {
            if (Active) mixer.SetSpeed(paused ? 0 : 1);
        }

        public void Revive()
        {
            Reset();
            if (Active && !graph.IsPlaying()) graph.Play();
        }
    }
}
