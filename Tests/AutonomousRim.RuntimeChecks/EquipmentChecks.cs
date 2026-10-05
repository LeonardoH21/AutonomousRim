using System;
using System.Collections.Generic;
using System.Linq;
using AutonomousRim.Core;
using AutonomousRim.Execution;
using AutonomousRim.Perception;
using AutonomousRim.Planning;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutonomousRim.RuntimeChecks
{
    public sealed class EquipmentChecks : MapComponent
    {
        private int stage;
        private int started;
        private Pawn pawn;
        private Thing mace;
        private Apparel armor;
        private Apparel forcedParka;
        private List<EquipmentOrder> orders = new List<EquipmentOrder>();
        private List<ManagedApparel> owned = new List<ManagedApparel>();
        private Dictionary<Pawn, bool> drafts = new Dictionary<Pawn, bool>();

        public EquipmentChecks(Map map) : base(map) { }

        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private IntVec3 Cell()
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, 8f, true))
                if (cell.InBounds(map) && cell.Standable(map) && !cell.Fogged(map) && cell.GetEdifice(map) == null && !cell.GetThingList(map).Any()) return cell;
            throw new InvalidOperationException("No empty equipment test cell.");
        }

        public override void MapComponentTick()
        {
            int ticks = Find.TickManager.TicksGame;
            if (stage == 99 || !GenCommandLine.CommandLineArgPassed("autonomousrimtest") || ticks < 200 || ticks % 20 != 0) return;
            try
            {
                if (stage == 0) { Setup(); started = ticks; stage = 1; }
                else if (stage == 1 && pawn.equipment.Primary == mace)
                {
                    EquipmentManager.TrackOrders(orders, owned);
                    Check(orders.Count == 1 && !orders[0].Pending, "Weapon order did not complete through the native job system.");
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
                    ColonyState state = ArmorPlan();
                    EquipmentManager.Apply(map, state, orders, owned, new List<Pawn>());
                    Check(!orders.Any(o => o.Pending), "Cooldown failed to suppress an immediate second equipment order.");
                    orders[0].IssuedTick = ticks - 1800; // Test fixture advances only the manager's cooldown record.
                    EquipmentManager.Apply(map, state, orders, owned, new List<Pawn>());
                    Check(orders.Any(o => o.Pending && o.Item == armor), "No armor job issued after cooldown.");
                    stage = 2;
                }
                else if (stage == 2 && pawn.apparel.Wearing(armor))
                {
                    EquipmentManager.TrackOrders(orders, owned);
                    Check(owned.Any(o => o.Apparel == armor) && pawn.outfits.forcedHandler.IsForced(armor), "AI-owned clothing was not stabilized after wearing.");
                    EquipmentManager.Stop(orders, owned);
                    Check(!pawn.outfits.forcedHandler.IsForced(armor), "Disabling equipment management retained an AI clothing force.");
                    Check(pawn.outfits.forcedHandler.IsForced(forcedParka), "Player-forced clothing was incorrectly released.");
                    Check(pawn.equipment.Primary == mace && pawn.apparel.Wearing(armor), "Disabling autoequip removed the current equipment.");
                    foreach (var entry in drafts) entry.Key.drafter.Drafted = entry.Value;
                    stage = 99;
                    Log.Message("[AutonomousRim.EquipmentTests] PASS: active trait effects, Brawler preference, forbidden/biocoded/policy/forced/shield guards, native weapon and armor jobs, cooldown and clothing-force cleanup.");
                }
                if (stage != 99 && ticks - started > 2400) throw new InvalidOperationException($"Equipment job timeout at stage {stage}; current job {pawn.CurJob?.def.defName}.");
            }
            catch (Exception error)
            {
                stage = 99;
                Log.Error("[AutonomousRim.EquipmentTests] FAIL: " + error);
            }
        }

        private void Setup()
        {
            pawn = map.mapPawns.FreeColonistsSpawned.First(p => PawnAnalyzer.IsCombatReady(p) && p.RaceProps.Humanlike);
            foreach (Pawn other in map.mapPawns.FreeColonistsSpawned.Where(p => p != pawn && p.drafter != null))
            {
                drafts[other] = other.Drafted;
                other.drafter.Drafted = true;
            }
            pawn.drafter.Drafted = false;
            pawn.needs.food.CurLevel = pawn.needs.food.MaxLevel;
            pawn.needs.rest.CurLevel = 1f;
            pawn.jobs.ClearQueuedJobs(true);
            pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
            foreach (Trait trait in pawn.story.traits.allTraits.Where(t => t.def.defName == "Brawler" || t.def.defName == "ShootingAccuracy" || t.def.defName == "Nudist" || t.def.defName == "Wimp").ToList())
                pawn.story.traits.RemoveTrait(trait);
            float baselineAim = pawn.GetStatValue(StatDefOf.AimingDelayFactor, true, 0);
            var shootingTrait = new Trait(DefDatabase<TraitDef>.GetNamed("ShootingAccuracy"), 1);
            pawn.story.traits.GainTrait(shootingTrait, true);
            Check(TraitAnalyzer.HasActiveTrait(pawn, "ShootingAccuracy"), "Active shooting trait not identified.");
            Check(pawn.GetStatValue(StatDefOf.AimingDelayFactor, true, 0) > baselineAim, "Careful-shooter aiming modifier not read from the game.");
            Check(TraitAnalyzer.Describe(pawn).Contains(StatDefOf.AimingDelayFactor.LabelCap.ToString()), "Trait audit omitted a numeric modifier.");
            pawn.story.traits.RemoveTrait(shootingTrait);
            pawn.story.traits.GainTrait(new Trait(TraitDefOf.Brawler), true);
            pawn.skills.GetSkill(SkillDefOf.Shooting).Level = 18;
            pawn.skills.GetSkill(SkillDefOf.Melee).Level = 18;
            Check(TraitAnalyzer.PreferMelee(pawn), "Brawler did not receive a melee preference.");
            if (pawn.equipment.Primary != null) pawn.equipment.Remove(pawn.equipment.Primary);
            mace = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Mace"), ThingDefOf.Plasteel);
            GenSpawn.Spawn(mace, Cell(), map);
            Thing gun = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_BoltActionRifle"));
            GenSpawn.Spawn(gun, Cell(), map);
            gun.SetForbidden(true, false);
            Check(!EquipmentPlanner.AllowedWeapon(pawn, gun), "Forbidden gun accepted for autoequip.");
            gun.SetForbidden(false, false);
            Check(EquipmentAnalyzer.Suitability(pawn, EquipmentAnalyzer.Analyze(mace)) > EquipmentAnalyzer.Suitability(pawn, EquipmentAnalyzer.Analyze(gun)), "Brawler ranking preferred the rifle over the melee weapon.");
            CompBiocodable biocode = gun.TryGetComp<CompBiocodable>();
            Check(biocode != null, "Test rifle has no biocoding component.");
            biocode.CodeFor(map.mapPawns.FreeColonistsSpawned.First(p => p != pawn));
            Check(!EquipmentPlanner.AllowedWeapon(pawn, gun), "Foreign biocoded weapon accepted.");

            foreach (Apparel apparel in pawn.apparel.WornApparel.ToList()) pawn.apparel.Remove(apparel);
            forcedParka = (Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_Parka"), ThingDefOf.Cloth);
            pawn.apparel.Wear(forcedParka);
            pawn.outfits.forcedHandler.SetForced(forcedParka, true);
            var competingParka = (Apparel)ThingMaker.MakeThing(forcedParka.def, ThingDefOf.Cloth);
            GenSpawn.Spawn(competingParka, Cell(), map);
            Check(!EquipmentPlanner.AllowedApparel(pawn, competingParka, owned), "Player-forced apparel could be replaced.");
            var shield = (Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_ShieldBelt"));
            pawn.apparel.Wear(shield);
            Thing freeGun = ThingMaker.MakeThing(gun.def); GenSpawn.Spawn(freeGun, Cell(), map);
            Check(!EquipmentPlanner.AllowedWeapon(pawn, freeGun), "Ranged gun accepted while wearing a blocking shield.");
            pawn.apparel.Remove(shield); shield.Destroy(); freeGun.Destroy(); competingParka.Destroy(); gun.Destroy();
            armor = (Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_FlakVest"));
            GenSpawn.Spawn(armor, Cell(), map);
            bool policyAllowed = pawn.outfits.CurrentApparelPolicy.filter.Allows(armor);
            pawn.outfits.CurrentApparelPolicy.filter.SetAllow(armor.def, false);
            Check(!EquipmentPlanner.AllowedApparel(pawn, armor, owned), "Outfit policy ignored.");
            pawn.outfits.CurrentApparelPolicy.filter.SetAllow(armor.def, policyAllowed);

            Job manual = JobMaker.MakeJob(JobDefOf.Wait); manual.expiryInterval = 200; manual.playerForced = true;
            pawn.jobs.TryTakeOrderedJob(manual, JobTag.Misc, false);
            Check(!EquipmentManager.CanAct(pawn), "A player-forced job could be interrupted.");
            pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
            ColonyState state = ColonyStateScanner.Scan(map);
            state.EquipmentDecisions = state.EquipmentDecisions.Where(d => d.Pawn == pawn && d.Item == mace).ToList();
            Check(state.EquipmentDecisions.Count > 0, "No legitimate melee upgrade proposed.");
            EquipmentManager.Apply(map, state, orders, owned, new List<Pawn>());
            Check(orders.Count == 1 && orders[0].Pending && pawn.CurJob.def == JobDefOf.Equip, "Native weapon equip job not issued.");
        }

        private ColonyState ArmorPlan()
        {
            ColonyState state = ColonyStateScanner.Scan(map);
            state.EquipmentDecisions = state.EquipmentDecisions.Where(d => d.Pawn == pawn && d.Item == armor).ToList();
            Check(state.EquipmentDecisions.Count > 0, "No legitimate armor upgrade proposed.");
            return state;
        }
    }
}
