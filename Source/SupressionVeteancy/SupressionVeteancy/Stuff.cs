using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using HarmonyLib;
using HarmonyMod;
using CombatExtended;
using UnityEngine;

namespace SupressionVeteancy
{
    public static class DebugAction
    {
        [DebugAction("Supression xp", "Set supression xp", false, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SetXP()
        {
            
        }
    }

    public class DaComp : ThingComp
    {
        public int shotsEncountered;

        public float cachedTeamMod = -5f;

        public override void PostExposeData()
        {
            Scribe_Values.Look<int>(ref shotsEncountered, "shots");
            base.PostExposeData();
        }

        public override void PostPostMake()
        {
            switch(this.parent.Faction?.def.techLevel ?? TechLevel.Industrial)
            {
                case TechLevel.Animal:
                    break;
                case TechLevel.Neolithic:
                    shotsEncountered = (int)Rand.Range(ModSettinges.modsets.treshold / 1.25f, ModSettinges.modsets.treshold * 10f);
                    break;
                case TechLevel.Medieval:
                    shotsEncountered = (int)Rand.Range(ModSettinges.modsets.treshold / 1.25f, ModSettinges.modsets.treshold * 6f);
                    break;
                case TechLevel.Industrial:
                    shotsEncountered = (int)Rand.Range(ModSettinges.modsets.treshold / 1.25f, ModSettinges.modsets.treshold * 3);
                    break;
                case TechLevel.Spacer:
                    shotsEncountered = (int)Rand.Range(ModSettinges.modsets.treshold / 1.25f, ModSettinges.modsets.treshold * 4.5f);
                    break;
                case TechLevel.Archotech:
                    shotsEncountered = (int)Rand.Range(ModSettinges.modsets.treshold / 1.25f, ModSettinges.modsets.treshold * 5f);
                    break;
                default:
                    shotsEncountered = (int)Rand.Range(ModSettinges.modsets.treshold / 1.25f, ModSettinges.modsets.treshold * 10f);
                    break;
            }
            base.PostPostMake();
        }
    }

    [StaticConstructorOnStartup]
    public class ExperiencePatcher
    {
        static ExperiencePatcher()
        {
            var pawns = DefDatabase<ThingDef>.AllDefs.Where(x => x.comps?.Any(y => y is CompProperties_Suppressable) ?? false);

            foreach(ThingDef def in pawns)
            {
                def.comps.Add(new CompProperties { compClass = typeof( DaComp )});
            }

            var Harmony = new Harmony("Caula.CECE");

            Harmony.PatchAll();

            if (CE_StatDefOf.Suppressability.parts == null)
            {
                CE_StatDefOf.Suppressability.parts = new List<StatPart>();
            }

            CE_StatDefOf.Suppressability.parts.Add(new ExperienceStatPart());
        }
    }

    public class ExperienceStatPart : StatPart
    {
        public override string ExplanationPart(StatRequest req)
        {
            if (req.Thing != null)
            {
                var dacomp = req.Thing.TryGetComp<DaComp>();
                if (dacomp != null)
                {

                    float mult = (float)Math.Round((1f - ((dacomp.shotsEncountered * 1f / ModSettinges.modsets.treshold * 1f) * 0.1f)), 2);

                    if (mult <= ModSettinges.modsets.max)
                    {
                        mult = ModSettinges.modsets.max;
                    }

                    var result = "Experience effect x" + mult + "\n Experienced shots: " + dacomp.shotsEncountered;

                    if (ModSettinges.modsets.teaming)
                    {
                        var p = req.Thing as Pawn;
                        if (
                            !CombatExtended.Utilities.
                            GenClosest.
                            PawnsInRange(p.Position, p.Map, 8 * p.health.capacities.GetLevel(PawnCapacityDefOf.Hearing)).
                            Where(x => x.Faction == p.Faction && !p.Downed)
                            .EnumerableNullOrEmpty()
                            )
                        {
                            if (dacomp.cachedTeamMod != -5f)
                            {
                                result += "\n Friendlies around x" + Math.Round( (1f / dacomp.cachedTeamMod), 2);
                            }
                        }
                    }

                    return result;
                }
            }
            return null;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing != null)
            {
                var dacomp = req.Thing.TryGetComp<DaComp>();
                if (dacomp != null)
                {
                    float mult = (float)Math.Round((1f - ((dacomp.shotsEncountered * 1f / ModSettinges.modsets.treshold * 1f) * 0.1f)), 2);
                    
                    if (mult <= ModSettinges.modsets.max)
                    {
                        mult = ModSettinges.modsets.max;
                    }

                    if (ModSettinges.modsets.teaming)
                    {
                        var p = req.Thing as Pawn;
                        if (
                            !CombatExtended.Utilities.
                            GenClosest.
                            PawnsInRange(p.Position, p.Map, 8 * p.health.capacities.GetLevel(PawnCapacityDefOf.Hearing)).
                            Where(x => x.Faction == p.Faction && !p.Downed)
                            .EnumerableNullOrEmpty()
                            )
                        {
                            if (dacomp.cachedTeamMod == -5f)
                            {
                                var allPawns = CombatExtended.Utilities.GenClosest.PawnsInRange(p.Position, p.Map, 8 * p.health.capacities.GetLevel(PawnCapacityDefOf.Hearing));

                                allPawns = allPawns.Where(x => x != p);

                                float finalMult = (1f + ( (allPawns.Count() -1) / 8f ));

                                dacomp.cachedTeamMod = Math.Min((float)Math.Round(finalMult, 2), 3);
                            }

                            val /= dacomp.cachedTeamMod;


                        }
                        else
                        {
                            dacomp.cachedTeamMod = -5f;
                        }
                    }

                    val *= mult;
                }
            }
        }
    }

    [HarmonyPatch(typeof(CompSuppressable), "AddSuppression")]

    static class PostFixNotify
    {
        public static void Postfix(CompSuppressable __instance)
        {
            var dad = __instance.parent as Pawn;

            dad.TryGetComp<DaComp>().shotsEncountered++;
        }
    }

    public class ModSettinges : Mod
    {
        public static SettingsMod modsets;

        public ModSettinges(ModContentPack content) : base(content)
        {
            modsets = GetSettings<SettingsMod>();
        }

        public override string SettingsCategory()
        {
            return "Combat experience";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var list1 = new Listing_Standard();

            list1.Begin(inRect);

            list1.Label("Supression treshold for 10% reduction: " + modsets.treshold, -1, "At this number of shots supressability will be decreased by 10% and will scale by multiplications of this value");

            modsets.treshold = (int)list1.Slider(modsets.treshold, 1200, 20000);

            list1.Label("Max supressability decrease " + modsets.max.ToString());

            modsets.max = (float)Math.Round(list1.Slider(modsets.max, 0f, 0.99f), 2);

            list1.CheckboxLabeled("Enable pawns being close to each other making them less supressable", ref modsets.teaming);

            list1.End();
        }
    }

    public class SettingsMod : ModSettings
    {
        public int treshold = 1000;

        public float max = 0.5f;

        public bool teaming = true;

        public override void ExposeData()
        {
            Scribe_Values.Look<int>(ref treshold, "treshold");
            Scribe_Values.Look<float>(ref max, "maximo");
            Scribe_Values.Look<bool>(ref teaming, "teaming");
            base.ExposeData();
        }


    }
}
