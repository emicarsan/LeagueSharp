﻿using System;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Staberina.Properties;
using TreeLib.Core;
using TreeLib.Extensions;
using TreeLib.Objects;
using Color = System.Drawing.Color;

namespace Staberina
{
    internal class Katarina : Champion
    {
        private const int RRange = 550;
        private static bool CastWAfterE;
        private static readonly ColorBGRA ScriptColor = new ColorBGRA(215, 40, 242, 255);
        private static int LastWardPlacement;
        private static bool WardJumping;
        private static readonly Random Random = new Random(Utils.TickCount);

        public Katarina()
        {
            Q = SpellManager.Q;
            W = SpellManager.W;
            E = SpellManager.E;
            R = SpellManager.R;

            Menu = new Menu("Staberina", "Staberina", true);
            Menu.SetFontStyle(FontStyle.Regular, ScriptColor);

            Orbwalker = Menu.AddOrbwalker();

            var spells = Menu.AddMenu("Spells", "Spells");

            var qMenu = spells.AddMenu("Q", "Q");
            qMenu.AddBool("QCombo", "Use in Combo");
            qMenu.AddBool("QHarass", "Use in Harass");

            var wMenu = spells.AddMenu("W", "W");
            wMenu.AddBool("WCombo", "Use in Combo");
            wMenu.AddBool("WHarass", "Use in Harass");
            wMenu.AddBool("WAuto", "Auto W", false);

            var eMenu = spells.AddMenu("E", "E");
            eMenu.AddBool("ECombo", "Use in Combo");
            eMenu.AddBool("EHarass", "Use in Harass");
            eMenu.AddSlider("EEnemies", "Max Enemies", 5, 1, 5);
            eMenu.Item("EEnemies").SetTooltip("Maximum enemies to E into in Combo.");

            var rMenu = spells.AddMenu("R", "R");

            rMenu.AddBool("RCombo", "Smart R");
            rMenu.Item("RCombo").SetTooltip("Use R in Combo when killable enemy is around");
            rMenu.AddSlider("RUltTicks", "Smart R Ticks", 7, 1, 10);
            rMenu.Item("RUltTicks").SetTooltip("For damage calculation. One tick is 250 ms of channeling.");

            rMenu.AddSlider("RRangeDecrease", "Decrease Range", 30);
            rMenu.Item("RRangeDecrease").ValueChanged += (sender, args) =>
            {
                R.Range = RRange - args.GetNewValue<Slider>().Value;
                var rDraw = Menu.Item("3Draw");
                if (rDraw == null)
                {
                    return;
                }
                var rCircle = rDraw.GetValue<Circle>();
                rDraw.SetValue(new Circle(rCircle.Active, rCircle.Color, R.Range));
            };
            R.Range = RRange - rMenu.Item("RRangeDecrease").GetValue<Slider>().Value;

            rMenu.AddBool("RInCombo", "Always R in Combo", false);
            rMenu.AddBool("RMovement", "Disable Movement while casting R");
            rMenu.AddBool("REvade", "Disable Evade while casting R");
            rMenu.AddBool("RCancelNoEnemies", "Cancel R if no enemies", false);
            rMenu.AddKeyBind("RCancelUlt", "Cancel R Key", 'J');

            var ks = Menu.AddMenu("Killsteal", "Killsteal");
            ks.AddBool("KSEnabled", "Use Smart KS");

            ks.AddInfo("KSInfo", "-- Spells --", ScriptColor);
            ks.AddBool("KSQ", "Use Q");
            ks.AddBool("KSW", "Use W");
            ks.AddBool("KSE", "Use E");
            ks.AddBool("KSR", "Use Smart R");
            ks.Item("KSR").SetTooltip("Uses ultimate tick amount from Spells > R.");

            ks.AddInfo("KSInfo2", "-- Misc --", ScriptColor);
            ks.AddBool("KSRCancel", "Cancel R to KS");
            ks.Item("KSRCancel").SetTooltip("Cancel ultimate channel to KS with other spells.");
            ks.AddSlider("KSEnemies", "Max Enemies", 5, 1, 5);
            ks.Item("KSEnemies").SetTooltip("Maximum enemies to E in to KS.");
            ks.AddSlider("KSHealth", "Min Health", 10);
            ks.Item("KSHealth").SetTooltip("Minimum health to E in to KS.");
            ks.AddBool("KSGapclose", "Gapclose with E", false);
            ks.Item("KSGapclose").SetTooltip("Cast E to units in range of killable target.");
            ks.AddBool("KSWardJump", "Ward Jump", false);
            ks.Item("KSWardJump").SetTooltip("Ward jump with e to killable target.");
            ks.AddBool("KSTurret", "Block E Under Turret");
            ks.Item("KSTurret").SetTooltip("Don't attempt to KS units (with E) under turret.");

            var farm = Menu.AddMenu("Farm", "Farm");

            var qFarm = farm.AddMenu("FarmQ", "Q");
            qFarm.AddBool("QFarm", "Use in Farm");
            qFarm.AddBool("QLastHit", "Only Last Hit (Only Killable)");

            var wFarm = farm.AddMenu("FarmW", "W");
            wFarm.AddBool("WFarm", "Use in Farm");
            wFarm.AddSlider("WMinionsHit", "Min Minions Killed", 2, 1, 4);

            var eFarm = farm.AddMenu("FarmE", "E");
            eFarm.AddBool("EFarm", "Use E->W in Farm", false);
            eFarm.AddSlider("EMinionsHit", "Min Minions Killed", 3, 1, 4);

            farm.AddKeyBind("FarmEnabled", "Farm Enabled", 'J', KeyBindType.Toggle, true);
            farm.Item("FarmEnabled").SetTooltip("Enabled in LastHit and LaneClear mode.", ScriptColor);

            var flee = Menu.AddMenu("Flee", "Flee");
            flee.AddKeyBind("FleeEnabled", "Flee Enabled", 'T');
            flee.AddBool("FleeE", "Use E");
            flee.AddBool("FleeWard", "Use Wards to Flee");

            var draw = Menu.AddMenu("Drawing", "Drawing");
            draw.AddCircle("0Draw", "Draw Q", Color.Purple, Q.Range);
            draw.AddCircle("1Draw", "Draw W", Color.DeepPink, W.Range);
            draw.AddCircle("2Draw", "Draw E", Color.DeepPink, E.Range);
            draw.AddCircle("3Draw", "Draw R", Color.White, R.Range);
            draw.AddBool("ComboKillablePermashow", "Permashow Combo Killable");
            draw.AddBool("FarmPermashow", "Permashow Farm Enabled");

            draw.Item("FarmPermashow").ValueChanged +=
                (sender, eventArgs) =>
                {
                    farm.Item("FarmEnabled").Permashow(eventArgs.GetNewValue<bool>(), null, ScriptColor);
                };

            if (draw.Item("FarmPermashow").IsActive())
            {
                farm.Item("FarmEnabled").Permashow(true, null, ScriptColor);
            }

            var dmg = draw.AddMenu("DamageIndicator", "Damage Indicator");
            dmg.AddBool("DmgEnabled", "Draw Damage Indicator");
            dmg.AddCircle("HPColor", "Predicted Health Color", Color.White);
            dmg.AddCircle("FillColor", "Damage Color", Color.HotPink);
            dmg.AddBool("Killable", "Killable Text");
            DamageIndicator.Initialize(dmg, Utility.GetComboDamage);

            Menu.AddInfo("MenuInfo", "-- Misc --", ScriptColor);
            Menu.AddList("ComboMode", "Combo Mode", new[] { "E->Q->W", "Q->E->W" });

            Menu.AddKeyBind("ComboKillable", "Only Combo Killable", 'K', KeyBindType.Toggle);

            draw.Item("ComboKillablePermashow").ValueChanged +=
                (sender, eventArgs) =>
                {
                    Menu.Item("ComboKillable").Permashow(eventArgs.GetNewValue<bool>(), null, ScriptColor);
                };

            if (draw.Item("ComboKillablePermashow").IsActive())
            {
                Menu.Item("ComboKillable").Permashow(true, null, ScriptColor);
            }

            Menu.AddBool("Sounds", "Sounds");
            if (Menu.Item("Sounds").IsActive())
            {
                new SoundObject(Resources.Load).Play();
            }

            Menu.AddInfo("Info", "By Trees and Lilith", ScriptColor);

            SpellManager.Initialize(Menu, Orbwalker);

            Obj_AI_Base.OnIssueOrder += Obj_AI_Base_OnIssueOrder;
            Menu.AddToMainMenu();
        }

        public static int UltTicks
        {
            get { return Menu.Item("RUltTicks").GetValue<Slider>().Value; }
        }

        private static void Obj_AI_Base_OnIssueOrder(Obj_AI_Base sender, GameObjectIssueOrderEventArgs args)
        {
            if (Menu.Item("RMovement").IsActive() && sender.IsMe && Player.IsChannelingImportantSpell())
            {
                args.Process = false;
            }
        }

        public override void OnUpdate()
        {
            if (Player.IsDead)
            {
                return;
            }

            var c = Player.IsChannelingImportantSpell();

            if (c)
            {
                if (Menu.Item("RCancelUlt").IsActive() && Utility.MoveRandomly())
                {
                    return;
                }

                if (Menu.Item("RCancelNoEnemies").IsActive() && Player.CountEnemiesInRange(RRange) == 0 &&
                    Utility.MoveRandomly())
                {
                    return;
                }
            }

            if (WardJumping)
            {
                if (!E.IsReady())
                {
                    WardJumping = false;
                    return;
                }

                var ward =
                    ObjectManager.Get<Obj_AI_Minion>()
                        .Where(o => o.Distance(Game.CursorPos) < 200 && E.IsInRange(o) && MinionManager.IsWard(o))
                        .OrderBy(o => o.Distance(Game.CursorPos))
                        .ThenByDescending(o => o.DistanceToPlayer())
                        .FirstOrDefault();

                if (ward == null)
                {
                    WardJumping = false;
                    return;
                }

                if (E.CastOnUnit(ward))
                {
                    Console.WriteLine("WARD JUMP");
                    return;
                }
            }

            if (Flee())
            {
                return;
            }

            if (AutoKill())
            {
                return;
            }

            if (c)
            {
                return;
            }

            if (Menu.Item("REvade").IsActive() && EvadeDisabler.EvadeDisabled)
            {
                EvadeDisabler.EnableEvade();
            }

            if (Menu.Item("WAuto").IsActive() && W.IsReady() && Enemies.Any(h => h.IsValidTarget(W.Range)) && W.Cast())
            {
                Console.WriteLine("AUTO W");
            }
        }

        public override void OnCombo(Orbwalking.OrbwalkingMode mode)
        {
            var comboMode = Menu.Item("ComboMode").GetValue<StringList>().SelectedIndex;
            var d = comboMode == 0 ? E.Range : Q.Range;
            var target = TargetSelector.GetTarget(d, TargetSelector.DamageType.Magical);

            if (!target.IsValidTarget())
            {
                return;
            }

            var q = Q.CanCast(target) && Q.IsActive();
            var w = W.CanCast(target) && W.IsActive();
            var e = E.CanCast(target) && E.IsActive() &&
                    target.CountEnemiesInRange(200) <= Menu.Item("EEnemies").GetValue<Slider>().Value;
            var r = Utility.IsRReady() && target.IsValidTarget(R.Range);

            if (mode == Orbwalking.OrbwalkingMode.Combo && Menu.Item("ComboKillable").IsActive())
            {
                var damage = target.GetComboDamage(q, w, e, r, true);
                if (target.Health > damage)
                {
                    return;
                }
            }

            var delay = (int) (200 + Game.Ping / 2f + Random.Next(100, 300));
            if (Q.LastCastedDelay(delay) || E.LastCastedDelay(delay))
            {
                return;
            }

            if (comboMode == 0 && q && e && E.CastOnUnit(target))
            {
                return;
            }

            if (q && Q.CastOnUnit(target))
            {
                return;
            }

            if (e && E.CastOnUnit(target))
            {
                return;
            }

            if (w && W.Cast())
            {
                return;
            }

            if (r && mode == Orbwalking.OrbwalkingMode.Combo)
            {
                if (Menu.Item("RInCombo").IsActive() && R.Cast())
                {
                    return;
                }

                if (!Menu.Item("RCombo").IsActive())
                {
                    return;
                }

                var enemy =
                    Enemies.FirstOrDefault(h => h.IsValidTarget(R.Range) && h.GetCalculatedRDamage(UltTicks) > h.Health);
                if (enemy != null && R.Cast()) {}
            }
        }

        private static bool AutoKill()
        {
            if (!Menu.Item("KSEnabled").IsActive())
            {
                return false;
            }

            var channeling = Player.IsChannelingImportantSpell();

            if (channeling && !Menu.Item("KSRCancel").IsActive())
            {
                return false;
            }

            var q = Menu.Item("KSQ").IsActive() && Q.IsReady();
            var w = Menu.Item("KSW").IsActive() && W.IsReady();
            var e = Menu.Item("KSE").IsActive() && E.IsReady();
            var r = (Menu.Item("KSR").IsActive() ||
                     Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && Menu.Item("RCombo").IsActive()) &&
                    Utility.IsRReady();

            var target =
                Enemies.Where(
                    h =>
                        h.IsValidTarget(Q.Range) &&
                        h.GetComboDamage(q, w && h.IsValidTarget(W.Range), false, r && h.IsValidTarget(R.Range), true) >
                        h.Health).MinOrDefault(h => h.Health);

            // killable enemies in q range
            if (target.IsValidTarget())
            {
                if (channeling && Utility.MoveRandomly())
                {
                    return true;
                }

                if (q && Q.CanCast(target) && Q.CastOnUnit(target))
                {
                    Console.WriteLine("Q KS");
                    return true;
                }

                if (w && W.CanCast(target) && W.Cast())
                {
                    Console.WriteLine("W KS");
                    return true;
                }

                if (r && target.IsValidTarget(R.Range) && R.Cast())
                {
                    Console.WriteLine("R KS");
                    return true;
                }
            }

            if (!e || Player.HealthPercent < Menu.Item("KSHealth").GetValue<Slider>().Value)
            {
                return false;
            }

            var maxEnemies = Menu.Item("KSEnemies").GetValue<Slider>().Value;

            // killable enemies in e range
            if (
                Enemies.Any(
                    enemy =>
                        enemy.IsValidTarget(E.Range) && enemy.CountEnemiesInRange(200) <= maxEnemies &&
                        enemy.GetComboDamage(q, w, true, r, true) > enemy.Health &&
                        (!Menu.Item("KSTurret").IsActive() || !enemy.UnderTurret(true)) && E.CastOnUnit(enemy)))
            {
                return true;
            }

            if (Menu.Item("KSGapclose").IsActive())
            {
                // killable enemies in range of valid e targets
                foreach (var unit in Utility.GetETargets())
                {
                    foreach (var enemy in
                        Enemies.Where(
                            h =>
                                h.NetworkId != unit.NetworkId && h.IsValidTarget(Q.Range, true, unit.ServerPosition) &&
                                h.CountEnemiesInRange(200) <= maxEnemies &&
                                (!Menu.Item("KSTurret").IsActive() || !h.UnderTurret(true))))
                    {
                        var d = enemy.GetComboDamage(
                            q, w && enemy.IsValidTarget(W.Range, true, unit.ServerPosition), false,
                            r && enemy.IsValidTarget(R.Range, true, unit.ServerPosition), true);
                        if (d > enemy.Health && E.CastOnUnit(unit))
                        {
                            return true;
                        }
                    }
                }
            }

            if (!Menu.Item("KSWardJump").IsActive())
            {
                return false;
            }

            var wardSlot = Utility.GetReadyWard();

            if (wardSlot.Equals(SpellSlot.Unknown) || !LastWardPlacement.HasTimePassed(2000))
            {
                return false;
            }

            var range = Player.Spellbook.GetSpell(wardSlot).SData.CastRange;

            // killable enemies that we can ward hop to
            foreach (var enemy in
                Enemies.Where(
                    h => h.IsValidTarget(range + Q.Range) && (!Menu.Item("KSTurret").IsActive() || !h.UnderTurret(true)))
                )
            {
                var pos = Player.ServerPosition.Extend(enemy.ServerPosition, range);
                var d = enemy.GetComboDamage(
                    q, w && enemy.IsValidTarget(W.Range, true, pos), false, r && enemy.IsValidTarget(R.Range, true, pos),
                    true);
                if (d > enemy.Health && Player.Spellbook.CastSpell(wardSlot, pos))
                {
                    LastWardPlacement = Utils.TickCount;
                    WardJumping = true;
                    return true;
                }
            }
            return false;
        }

        private static bool Flee()
        {
            if (!Menu.Item("FleeEnabled").IsActive())
            {
                return false;
            }

            Orbwalker.ActiveMode = Orbwalking.OrbwalkingMode.None;

            if (Menu.Item("FleeE").IsActive() && E.IsReady())
            {
                var closestTarget = Utility.GetClosestETarget(Game.CursorPos);

                if (closestTarget != null && closestTarget.Distance(Game.CursorPos) < 200)
                {
                    return E.CastOnUnit(closestTarget);
                }

                var ward = Utility.GetReadyWard();
                if (Menu.Item("FleeWard").IsActive() && ward != SpellSlot.Unknown &&
                    LastWardPlacement.HasTimePassed(2000))
                {
                    var range = Player.Spellbook.GetSpell(ward).SData.CastRange;
                    if (Player.Spellbook.CastSpell(ward, Player.ServerPosition.Extend(Game.CursorPos, range)))
                    {
                        Console.WriteLine("PLACE WARD");
                        LastWardPlacement = Utils.TickCount;
                        WardJumping = true;
                        return true;
                    }
                }
            }

            if (!Player.IsDashing() && Player.GetWaypoints().Last().Distance(Game.CursorPos) > 100)
            {
                Player.IssueOrder(GameObjectOrder.MoveTo, Player.ServerPosition.Extend(Game.CursorPos, 250), false);
            }

            return false;
        }


        public override void Drawing_OnDraw(EventArgs args)
        {
            if (Player.IsDead)
            {
                return;
            }

            foreach (var spell in new[] { "0", "1", "2", "3" })
            {
                var circle = Menu.Item(spell + "Draw").GetValue<Circle>();
                if (circle.Active)
                {
                    Render.Circle.DrawCircle(Player.Position, circle.Radius, circle.Color);
                }
            }
        }

        public override void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender == null || !sender.IsValid || !sender.IsMe)
            {
                return;
            }

            if (args.Slot.Equals(SpellSlot.R) && Menu.Item("REvade").IsActive())
            {
                EvadeDisabler.DisableEvade(2500);
            }

            if (args.Slot.Equals(SpellSlot.E) && CastWAfterE && W.IsReady())
            {
                CastWAfterE = false;
                W.Cast();
            }
        }

        public override void GameObject_OnCreate(GameObject sender, EventArgs args)
        {
            if (!Menu.Item("FleeEnabled").IsActive() || !Menu.Item("FleeE").IsActive() || !E.IsReady())
            {
                return;
            }

            var unit = sender as Obj_AI_Minion;
            if (unit == null || !unit.IsValid || unit.DistanceToPlayer() > E.Range)
            {
                return;
            }

            Console.WriteLine(unit.Name);
            if (unit.Name.ToLower().Contains("ward") && !unit.Name.ToLower().Equals("wardcorpse"))
            {
                LeagueSharp.Common.Utility.DelayAction.Add(
                    50, () =>
                    {
                        if (unit.Buffs.Any(b => b.SourceName.Equals(Player.ChampionName)))
                        {
                            E.CastOnUnit(unit);
                        }
                    });
                return;
            }

            if (unit.Distance(Game.CursorPos) > 200) // random ass object
            {
                return;
            }

            E.CastOnUnit(unit);
        }

        public override void OnFarm(Orbwalking.OrbwalkingMode mode)
        {
            if (!Menu.Item("FarmEnabled").IsActive())
            {
                return;
            }

            var minions = MinionManager.GetMinions(E.Range);

            if (Menu.Item("WFarm").IsActive() && W.IsReady())
            {
                var wKillableMinions = minions.Count(m => m.IsValidTarget(W.Range) && W.IsKillable(m));
                if (wKillableMinions < Menu.Item("WMinionsHit").GetValue<Slider>().Value)
                {
                    if (Menu.Item("EFarm").IsActive() && E.IsReady()) // e->w
                    {
                        foreach (var target in from target in Utility.GetETargets()
                            let killableMinions =
                                MinionManager.GetMinions(target.ServerPosition, W.Range + E.Range)
                                    .Count(m => W.IsKillable(m))
                            where killableMinions >= Menu.Item("EMinionsHit").GetValue<Slider>().Value
                            select target)
                        {
                            CastWAfterE = true;
                            if (E.CastOnUnit(target))
                            {
                                return;
                            }
                        }
                    }
                }
                else if (W.Cast())
                {
                    return;
                }
            }

            if (!Menu.Item("QFarm").IsActive() || !Q.IsReady())
            {
                return;
            }

            var qKillableMinion = minions.FirstOrDefault(m => m.IsValidTarget(Q.Range) && Q.IsKillable(m));
            var qMinion = minions.Where(m => m.IsValidTarget(Q.Range)).MinOrDefault(m => m.Health);

            if (qKillableMinion == null)
            {
                if (Menu.Item("QLastHit").IsActive() || qMinion == null)
                {
                    return;
                }

                Q.CastOnUnit(qMinion);
                return;
            }

            Q.CastOnUnit(qKillableMinion);
        }
    }
}