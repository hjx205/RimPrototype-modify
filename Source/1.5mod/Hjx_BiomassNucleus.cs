using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using RimWorld.Planet;

namespace Hjx_BiomassNucleus
{
    [DefOf]
    public static class AbilityDefOf
    {
        public static AbilityDef BMN_GroundSpike;

        public static AbilityDef BMN_BladeSprint;

        static AbilityDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(Hjx_BiomassNucleus.AbilityDefOf));
        }
    }

    [StaticConstructorOnStartup]
    public class BiomassNucleusMod : Mod
    {
        public static BiomassNucleusSettings settings;

        public BiomassNucleusMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<BiomassNucleusSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing_Standard = new Listing_Standard();
            listing_Standard.Begin(inRect);
            listing_Standard.Gap(5f);
            listing_Standard.CheckboxLabeled(
                "NoMissingPart".Translate(),
                ref settings.NoMissingPart,
                "BMN_Settingtip1".Translate()
                );
            listing_Standard.Gap(5f);
            settings.clawpowerSetting = (float)Math.Round(
                listing_Standard.SliderLabeled(
                    "Clawform_damage".Translate() + ": " + settings.clawpowerSetting.ToString("F1"),
                    settings.clawpowerSetting,
                    0.5f,
                    5f
                 ),
                1
            );
            settings.clawArmorPenetration = (float)Math.Round(
                listing_Standard.SliderLabeled(
                    "Clawform_ArmorPenetration".Translate() + ": " + settings.clawArmorPenetration.ToString("F1"),
                    settings.clawArmorPenetration,
                    0.5f,
                    3f
                ),
                1
            );
            listing_Standard.Gap(10f);
            settings.bladepowerSetting = (float)Math.Round(
                listing_Standard.SliderLabeled(
                    "Bladeform_damage".Translate() + ": " + settings.bladepowerSetting.ToString("F1"),
                    settings.bladepowerSetting,
                    0.5f,
                    5f
                 ),
                1
            );
            settings.bladeArmorPenetration = (float)Math.Round(
                listing_Standard.SliderLabeled(
                    "Bladeform_ArmorPenetration".Translate() + ": " + settings.bladeArmorPenetration.ToString("F1"),
                    settings.bladeArmorPenetration,
                    0.5f,
                    3f
                ),
                1
            );
            listing_Standard.End();
        }

        public override string SettingsCategory()
        {
            return "BiomassNucleus".Translate();
        }
    }

    public class BiomassNucleusSettings : ModSettings
    {
        public float clawpowerSetting = 1f;

        public float bladepowerSetting = 1f;

        public float clawArmorPenetration = 1f;

        public float bladeArmorPenetration = 1f;

        public bool NoMissingPart = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref clawpowerSetting, "clawpower", 1f);
            Scribe_Values.Look(ref bladepowerSetting, "bladepower", 1f);
            Scribe_Values.Look(ref clawArmorPenetration, "clawArmorPenetration", 1f);
            Scribe_Values.Look(ref bladeArmorPenetration, "bladeArmorPenetration", 1f);
            Scribe_Values.Look(ref NoMissingPart, "NoMissingPart", defaultValue: false, forceSave: true);
        }
    }

    public static class BMNUtility 
    {
        public static bool bigsmallActive;

        public static IntVec3 GetFirstEmptyCellInRange(IntVec3 loc, Map map, int radius = 3)
        {
            int num = GenRadial.NumCellsInRadius(radius);
            if(map == null)
            {
                Log.Message("地图为null");
                return loc;
            }
            if(num == 0)
            {
                return loc;
            }
            for(int i = 0; i < num; i++)
            {
                IntVec3 intVec = loc + GenRadial.RadialPattern[i];
                if(!(intVec == loc) && intVec.InBounds(map) && !intVec.Filled(map))
                {
                    return intVec;
                }
            }
            return loc;
        }

        public static void DrawCells(Map map, IntVec3 center, int range, Color color)
        {
            List<IntVec3> list = new List<IntVec3>();
            for(int i = center.z - range; i <= center.z + range; i++)
            {
                for(int j = center.x - range; j <= center.x + range; j++)
                {
                    IntVec3 intVec = new IntVec3(j, 0, i);
                    if (intVec.InBounds(map))
                    {
                        list.Add(intVec);
                    }
                }
            }
            GenDraw.DrawFieldEdges(list, color);
        }

        public static void ResurrectPawnFromVoid(Map map, IntVec3 positionHeld, Pawn pawn)
        {
            if(pawn.Corpse != null)
            {
                if(pawn.apparel != null)
                {
                    List<Apparel> wornApparel = pawn.apparel.WornApparel;
                    for(int i = 0; i < wornApparel.Count; i++)
                    {
                        wornApparel[i].Notify_PawnResurrected(pawn);
                    }
                }
                Corpse corpse = pawn.Corpse;
                corpse.Map.designationManager.DesignationOn(corpse, DesignationDefOf.Strip)?.Delete();
                // 强制剥离尸体装备(触发掉落物生成)
                ((IStrippable)corpse)?.Strip(notifyFaction: true);
                corpse.Destroy();
            }
            // 全局清楚pawn
            if (pawn.IsWorldPawn())
            {
                Find.WorldPawns.RemovePawn(pawn);
            }
            pawn.ForceSetStateToUnspawned();
            // 重新创建初始组件
            PawnComponentsUtility.CreateInitialComponents(pawn);
            pawn.health.Notify_Resurrected();
            if(pawn.Faction != null && pawn.Faction.IsPlayer && pawn.workSettings != null)
            {
                pawn.workSettings.EnableAndInitialize();
                Find.StoryWatcher.watcherPopAdaptation.Notify_PawnEvent(pawn, PopAdaptationEvent.GainedColonist);
            }
            GenSpawn.Spawn(pawn, positionHeld, map);
            EffecterDefOf.MeatExplosion.Spawn(positionHeld, map).Cleanup();
            Messages.Message("MessagePawnResurrected".Translate(pawn), pawn, MessageTypeDefOf.PositiveEvent);
            // 思绪系统清理
            PawnDiedOrDownedThoughtsUtility.RemoveDiedThoughts(pawn);
        }

        public static bool TryGiveMutation(Pawn pawn, HediffDef mutationDef, string partSide)
        {
            if(mutationDef.defaultInstallPart == null)
            {
                Log.ErrorOnce("Attempted to use mutation hediff which didn't specify a default install part (hediff: " + mutationDef.label, 194783821);
                return false;
            }
            List<BodyPartRecord> list = new List<BodyPartRecord>();
            list = (from part in pawn.RaceProps.body.GetPartsWithDef(mutationDef.defaultInstallPart)
                    where !pawn.health.hediffSet.HasDirectlyAddedPartFor(part)
                    select part).ToList();
            // 精确匹配部位侧面
            BodyPartRecord bodyPartRecord = null;
            if(list.Any((BodyPartRecord part) => part.woundAnchorTag == partSide))
            {
                bodyPartRecord = list.First((BodyPartRecord part) => part.woundAnchorTag == partSide);
            }
            if(bodyPartRecord == null)
            {
                foreach(BodyPartRecord item in list)
                {
                    // 调试日志
                    Log.Message($"PawnName: {pawn.Name},部位名称: {item.def.label}, woundAnchorTag: {item.woundAnchorTag}, index: {item.Index}, body: {item.body}");
                    if(HARraceCheck(item, partSide))
                    {
                        bodyPartRecord = item;
                        break;
                    }
                }
                // 若仍无匹配部位，触发错误提示并返回失败
                if(bodyPartRecord == null)
                {
                    Messages.Message("NoSpareBodyPart".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.RejectInput);
                    return false;
                }
            }
            pawn.health.RestorePart(bodyPartRecord);
            pawn.health.AddHediff(mutationDef, bodyPartRecord);
            return true;
        }

        public static void RemoveFirstHediff(Pawn pawn, HediffDef hediffDefToRemove, string woundAnchorTag = null)
        {
            HediffSet hediffSet = pawn.health.hediffSet;
            if(woundAnchorTag == null)
            {
                Hediff firstHediffOfDef = hediffSet.GetFirstHediffOfDef(hediffDefToRemove);
                if(firstHediffOfDef != null)
                {
                    pawn.health.RemoveHediff(firstHediffOfDef);
                }
                return;
            }
            foreach(Hediff hediff in hediffSet.hediffs)
            {
                if(hediff.def == hediffDefToRemove && hediff.Part != null)
                {
                    if(hediff.Part.woundAnchorTag == null && HARraceCheck(hediff.Part, woundAnchorTag))
                    {
                        pawn.health.RemoveHediff(hediff);
                        break;
                    }
                    if(hediff.Part.woundAnchorTag == woundAnchorTag)
                    {
                        pawn.health.RemoveHediff(hediff);
                        break;
                    }
                }
            }
        }

        public static bool HARraceCheck(BodyPartRecord part, string partSide)
        {
            if(part.body.ToString() == "Ratkin")
            {
                if(partSide == "LeftShoulder" && part.Index == 23)
                {
                    return true;
                }
                if(partSide == "RightShoulder" && part.Index == 34)
                {
                    return true;
                }
            }
            if(part.body.ToString() == "Kiiro_Body")
            {
                if(partSide == "LeftShoulder" && part.Index == 25)
                {
                    return true;
                }
                if(partSide == "RightShoulder" && part.Index == 36)
                {
                    return true;
                }
            }
            if(part.body.ToString() == "Mincho_BodyDef")
            {
                if(partSide == "LeftShoulder" && part.Index == 7)
                {
                    return true;
                }
                if(partSide == "RightShouler" && part.Index == 8)
                {
                    return true;
                }
            }
            if(part.body.ToString() == "Miliro_Body")
            {
                if(partSide == "LeftShoulder" && part.Index == 24)
                {
                    return true;
                }
                if(partSide == "RightShoulder" && part.Index == 35)
                {
                    return true;
                }
            }
            if(part.body.ToString() == "Maru")
            {
                if(partSide == "LeftShoulder" && part.Index == 24)
                {
                    return true;
                }
                if(partSide == "RightShoulder" && part.Index == 35)
                {
                    return true;
                }
            }
            if(part.body.ToString() == "Moosesian")
            {
                if(partSide == "LeftShoulder" && part.Index == 25)
                {
                    return true;
                }
                if(partSide == "RightShoulder" && part.Index == 36)
                {
                    return true;
                }
            }
            if(part.body.ToString() == "Rabbie")
            {
                if(partSide == "LeftShoulder" && part.Index == 23)
                {
                    return true;
                }
                if(partSide == "RightShoulder" && part.Index == 34)
                {
                    return true;
                }
            }
            return false;
        }

        public static void DynamicVerbTool(Pawn pawn, string defName, Action<Tool> modifyToolAction)
        {
            foreach(Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if(!(hediff.def.defName == defName))
                {
                    continue;
                }
                if(!(hediff is HediffWithComps hediffWithComps))
                {
                    break;
                }
                HediffComp_VerbGiver comp = hediffWithComps.GetComp<HediffComp_VerbGiver>();
                if(comp == null || comp.Tools == null)
                {
                    break;
                }
                {
                    foreach(Tool tool in comp.Tools)
                    {
                        modifyToolAction(tool);
                    }
                    break;
                }
            }
        }

        public static void InitializeVerbTool(string defName, Action<Tool> modifyToolAction)
        {
            HediffDef named = DefDatabase<HediffDef>.GetNamed(defName, errorOnFail: false);
            if(named == null)
            {
                Log.Error("未找到名为 " + defName + " 的Hediff定义。");
                return;
            }
            Hediff_BiomassPartBase hediff_BiomassPartBase = (Hediff_BiomassPartBase)Activator.CreateInstance(named.hediffClass);
            hediff_BiomassPartBase.def = named;
            hediff_BiomassPartBase.InitializeComps();
            HediffComp_VerbGiver hediffComp_VerbGiver = hediff_BiomassPartBase.TryGetComp<HediffComp_VerbGiver>();
            if(hediffComp_VerbGiver != null)
            {
                if(hediffComp_VerbGiver.Tools != null)
                {
                    foreach(Tool tool in hediffComp_VerbGiver.Tools)
                    {
                        modifyToolAction(tool);
                    }
                }
                else
                {
                    Log.Message("HediffComp_VerbGiver 组件中的 Tools 列表未空。");
                }
            }
            else
            {
                Log.Message("未找到 HediffComp_VerbGiver 组件。");
            }
            hediff_BiomassPartBase = null;
        }

        public static void DamagePawn(Pawn pawn, float damageAmount, float armorPenetration, Thing instigator, DamageDef damageDef, int num)
        {
            for(int i = 0; i < num; i++)
            {
                if(pawn == null)
                {
                    break;
                }
                if (pawn.Dead)
                {
                    break;
                }
                DamageInfo dinfo = new DamageInfo(damageDef, damageAmount, armorPenetration, -1f, instigator);
                DamageWorker.DamageResult damageResult = pawn.TakeDamage(dinfo);
            }
        }

        public static void RecacheActiveMods()
        {
            bigsmallActive = ModsConfig.IsActive("redmattis.bigsmall.core");
        }
    }

    public class CompAbilityEffect_BiomassCost : CompAbilityEffect
    {
        public new CompProperties_AbilityBiomassCost Props => (CompProperties_AbilityBiomassCost)props;

        private bool HasBiomass
        {
            get
            {
                if(!(parent.pawn.health?.hediffSet?.hediffs?.FirstOrDefault((Hediff h) => h is Hediff_BiomassNucleus) is Hediff_BiomassNucleus hediff_BiomassNucleus) || hediff_BiomassNucleus.GetEnergy() < Props.biomassCost)
                {
                    return false;
                }
                return true;
            }
        }

        public override void PostApplied(List<LocalTargetInfo> targets, Map map)
        {
            base.PostApplied(targets, map);
            if(parent.pawn.health?.hediffSet?.hediffs?.FirstOrDefault((Hediff h) => h is Hediff_BiomassNucleus) is Hediff_BiomassNucleus hediff_BiomassNucleus)
            {
                hediff_BiomassNucleus.SetEnergy(hediff_BiomassNucleus.GetEnergy() - Props.biomassCost);
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            if(!(parent.pawn.health?.hediffSet?.hediffs?.FirstOrDefault((Hediff h) => h is Hediff_BiomassNucleus) is  Hediff_BiomassNucleus hediff_BiomassNucleus))
            {
                reason = "AbilityDisabledNoBiomass".Translate(parent.pawn);
                return true;
            }
            if(hediff_BiomassNucleus.GetEnergy() < Props.biomassCost)
            {
                reason = "AbilityDisabledNoBiomass".Translate(parent.pawn);
                return true;
            }
            reason = null;
            return false;
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return HasBiomass;
        }
    }

    public class CompAbilityEffect_BladeSprint : CompAbilityEffect, ICompAbilityEffectOnJumpCompleted
    {
        public new CompProperties_BladeSprint Props => (CompProperties_BladeSprint)props;

        public void OnJumpCompleted(IntVec3 origin, LocalTargetInfo target)
        {
            Map map = parent.pawn.MapHeld;
            Vector3 normalized = (target.CenterVector3 - origin.ToVector3Shifted()).normalized; //计算从起点到目标的单位方向向量
            Vector3 vect = target.CenterVector3 + normalized; //在目标位置上偏移一个单位向量
            IntVec3 intVec = vect.ToIntVec3();
            Effecter effecter = ((!target.HasThing)
                ? Props.effecterDef.Spawn(target.Cell, parent.pawn.Map, Props.scale)
                : Props.effecterDef.Spawn(target.Thing, parent.pawn.Map, Props.scale)); // 决定效果生成的位置
            if(Props.maintainForTicks > 0)
            {
                parent.AddEffecterToMaintain(effecter, intVec, Props.maintainForTicks, map);
            }
            else
            {
                effecter.Cleanup();
            }
            List<Thing> list = ThingsInRange(intVec, 1);
            Hediff_BiomassNucleus hediff_BiomassNucleus = parent.pawn?.health?.hediffSet?.hediffs?.FirstOrDefault((Hediff h) => h is Hediff_BiomassNucleus) as Hediff_BiomassNucleus;
            float damageAmount = hediff_BiomassNucleus.BladePower * parent.pawn.GetStatValue(StatDefOf.MeleeDamageFactor);
            float bladeArmorPenetration = hediff_BiomassNucleus.BladeArmorPenetration;
            foreach(Thing item in list)
            {
                if(item is Pawn pawn && pawn.Faction != Faction.OfPlayer)
                {
                    BMNUtility.DamagePawn(pawn, damageAmount, bladeArmorPenetration, parent.pawn, DamageDefOf.Cut, 6);
                }
            }
            List<Thing> ThingsInRange(IntVec3 center, int range)
            {
                List<Thing> list2 = new List<Thing>();
                ThingGrid thingGrid = map.thingGrid;
                for(int i = center.z - range; i <= center.z + range; i++)
                {
                    for(int j = center.x - range; j <= center.x + range; j++)
                    {
                        IntVec3 c = new IntVec3(j, 0, i);
                        if (c.InBounds(map))
                        {
                            // 获取当前网格的所有物体并合并到结果列表
                            List<Thing> list3 = thingGrid.ThingsListAt(c);
                            if(list3 != null)
                            {
                                list2.AddRange(list3);
                            }
                        }
                    }
                }
                return list2;
            }
        }

        // AI目标选择逻辑(false,始终不会自动使用此能力)
        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return false;
        }

        // 此能力可应用于任何目标
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return true;
        }
    }

    public class CompAbilityEffect_GroundSpike : CompAbilityEffect
    {
        public new CompProperties_GroundSpike Props => (CompProperties_GroundSpike)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            // 获取目标实体(优先Pawn,然后Building)
            Pawn pawn = target.Pawn;
            Building building = target.Thing as Building;
            Map mapHeld = parent.pawn.MapHeld; // 获取施法者所在的地图
            float statValue = parent.pawn.GetStatValue(StatDefOf.MeleeDamageFactor);

            // 处理敌方生物单位
            if (pawn != null && pawn.Faction != Faction.OfPlayer)
            {
                // 在目标位置生成尖刺特效
                FleckMaker.Static(pawn.Position, mapHeld, Hjx_BiomassNucleus.FleckDefOf.BMN_GroundSpike_Stab);

                // 造成3次刺击伤害
                BMNUtility.DamagePawn(
                    pawn: pawn,
                    damageAmount: Hediff_BiomassNucleus.GroundSpikePower * statValue,
                    armorPenetration: Hediff_BiomassNucleus.GroundSpikeArmor * statValue,
                    instigator: parent.pawn,
                    damageDef: DamageDefOf.Stab,
                    num: 3
                );
            }
            // 处理敌方建筑单位
            else if(building != null && building.Faction != Faction.OfPlayer)
            {
                FleckMaker.Static(building.Position, mapHeld, Hjx_BiomassNucleus.FleckDefOf.BMN_GroundSpike_Stab);
                DamageInfo dinfo = new DamageInfo(
                    def: DamageDefOf.Stab,
                    amount: Hediff_BiomassNucleus.GroundSpikePower * statValue * 5f,
                    armorPenetration: Hediff_BiomassNucleus.GroundSpikeArmor,
                    angle: -1f,
                    instigator: parent.pawn
                );
                building.TakeDamage(dinfo);
            }
        }
    }

    public class CompAbilityEffect_Integrate : CompAbilityEffect
    {
        // 覆盖基类属性，获取配置
        public new CompProperties_Integrate Props => (CompProperties_Integrate)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = target.Pawn;
            if(pawn == null || pawn.Dead)
            {
                return;
            }
            Map mapHeld = parent.pawn.MapHeld;
            IntVec3 positionHeld = pawn.PositionHeld;
            if (positionHeld.IsValid)
            {
                float num = 0f;
                // 计算生物质获取量：取目标肉类数量的5%
                int num2 = Mathf.Max(GenMath.RoundRandom(pawn.GetStatValue(StatDefOf.MeatAmount)), 3);
                pawn.Kill(new DamageInfo(DamageDefOf.Psychic, 99999f, 0f, -1f, parent.pawn));
                if(pawn.Corpse == null)
                {
                    num = (float)((double)num2 * 0.05); // 直接计算生物质
                }
                else
                {
                    // 从尸体获取营养值
                    num = pawn.Corpse.GetStatValue(StatDefOf.Nutrition);
                    pawn.Corpse?.Destroy();
                }
                EffecterDefOf.MeatExplosion.Spawn(positionHeld, mapHeld).Cleanup();
                if(parent.pawn.health?.hediffSet?.hediffs?.FirstOrDefault((Hediff h) => h is Hediff_BiomassNucleus) is Hediff_BiomassNucleus hediff_BiomassNucleus)
                {
                    hediff_BiomassNucleus.UpdateTargetPawnDef(pawn);
                    hediff_BiomassNucleus.SetEnergy(hediff_BiomassNucleus.GetEnergy() + num * 5f);
                }
            }
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return false;
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = target.Pawn;
            if(pawn == null)
            {
                return false;
            }
            // 非血肉生物直接拒绝
            if (!pawn.RaceProps.IsFlesh)
            {
                if (throwMessages)
                {
                    Messages.Message("MessageNoFlesh".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.RejectInput);
                }
                return false;
            }
            // 小型生物或倒地的生物
            if(pawn.Downed || pawn.BodySize <= 0.67f)
            {
                return true;
            }
            // 生命值小于等于20%
            Pawn_HealthTracker health = pawn.health;
            if(health != null && health.summaryHealth?.SummaryHealthPercent <= 0.2f)
            {
                return true;
            }
            if (throwMessages)
            {
                Messages.Message("IntegrateFaildMessage".Translate(), pawn, MessageTypeDefOf.RejectInput);
            }
            return false;
        }

        // 鼠标悬停时显示的额外信息
        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            Pawn pawn = target.Pawn;
            if(pawn != null)
            {
                if (!pawn.RaceProps.IsFlesh)
                {
                    return "MessageNoFlesh".Translate();
                }
                // 未包含该生物类型时，显示可同化提示
                if(parent.pawn.health?.hediffSet?.hediffs?.FirstOrDefault((Hediff h) => h is Hediff_BiomassNucleus) is Hediff_BiomassNucleus hediff_BiomassNucleus && !hediff_BiomassNucleus.RaceDictionary.ContainsKey(pawn.def.defName))
                {
                    return "Integrate_count".Translate() + "+1";
                }
            }
            return null;
        }
    }

    public class CompBiomassNode : ThingComp
    {
        private Map map;

        private float nutrition = 0f;

        private static readonly IntRange ItemConsumCountRange = new IntRange(4, 12);

        private bool ConsumeCorpse = false; //是否消耗尸体

        private bool ConsumeFood = false; //是否消耗食物

        private Dictionary<IntVec3, Mote> rootEffects = new Dictionary<IntVec3, Mote>(); //每次消耗物品数量范围

        private int incubatetick = 0; //孵化倒计时

        private string incubatePawn;

        private string incubatepawnName;

        public float Nutrition
        {
            get
            {
                return nutrition;
            }
            set
            {
                if(value > 400f)
                {
                    nutrition = 400f;
                    ConsumeCorpse = false;
                    ConsumeFood = false;
                }
                else
                {
                    nutrition = value;
                }
            }
        }

        private CompProperties_BiomassNode Props => (CompProperties_BiomassNode)props;

        private int range => Props.ConsumeRange;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            map = parent.Map;
        }

        public override void CompTick()
        {
            base.CompTick();

            if (parent.IsHashIntervalTick(2500))
            {
                // 如果开启消耗模式
                if(ConsumeCorpse || ConsumeFood)
                {
                    // 获取当前位置
                    IntVec3 position = parent.Position;
                    List<Thing> list = ThingsInRange(position, range);
                    foreach(Thing item2 in list)
                    {
                        if(ConsumeCorpse && item2 is Corpse corpse && corpse.InnerPawn.RaceProps.IsFlesh)
                        {
                            Nutrition += GetNutritionFromCorpse(corpse);
                        }
                        else if(ConsumeFood && item2 is ThingWithComps item && !(item2 is Plant) && !(item2 is Corpse))
                        {
                            Nutrition += GetNutritionFromItenStack(item);
                        }
                    }
                }

                // 自修复逻辑
                if(parent.HitPoints != parent.MaxHitPoints)
                {
                    parent.HitPoints += 5;
                    if(parent.HitPoints > parent.MaxHitPoints)
                    {
                        parent.HitPoints = parent.MaxHitPoints;
                    }
                }
            }

            // 每500ticks更新一次根特效
            if(parent.IsHashIntervalTick(500) && (ConsumeCorpse || ConsumeFood))
            {
                UpdateRootEffectsInRange(parent.Position, range);
            }

            if (incubatetick <= 0) return;

            incubatetick--;
            if (incubatetick > 0) return;

            // 孵化完成时复活pawn
            foreach(Pawn item3 in PawnsFinder.All_AliveOrDead)
            {
                if(item3.ThingID == incubatePawn)
                {
                    BMNUtility.ResurrectPawnFromVoid(map, parent.Position, item3);
                    incubatePawn = null;
                    incubatepawnName = null;
                    break;
                }
            }
        }

        public List<Thing> ThingsInRange(IntVec3 center, int range)
        {
            List<Thing> list = new List<Thing>();
            ThingGrid thingGrid = map.thingGrid;
            for(int i = center.z - range; i <= center.z + range; i++)
            {
                for(int j = center.x - range; j <= center.x + range; j++)
                {
                    IntVec3 c = new IntVec3(j, 0, i);
                    if (c.InBounds(map))
                    {
                        List<Thing> list2 = thingGrid.ThingsListAt(c);
                        if(list2 != null)
                        {
                            list.AddRange(list2);
                        }
                    }
                }
            }
            return list;
        }

        public bool RootEffectCheck(IntVec3 c, Map map)
        {
            if (!c.InBounds(map))
            {
                return false;
            }
            List<Thing> list = map.thingGrid.ThingsListAtFast(c);
            foreach(Thing item in list)
            {
                if(ConsumeCorpse && item is Corpse corpse && corpse.InnerPawn.RaceProps.IsFlesh)
                {
                    return true;
                }
                if(ConsumeFood && item is ThingWithComps thing && !(item is Plant) && !(item is Corpse) && thing.GetStatValue(StatDefOf.Nutrition) > 0f)
                {
                    return true;
                }
            }
            return false;
        }

        public void UpdateRootEffectsInRange(IntVec3 center, int range)
        {
            for(int i = center.z -range; i <= center.z + range; i++)
            {
                for(int j = center.x - range; j <= center.x + range; j++)
                {
                    IntVec3 intVec = new IntVec3(j, 0, i);
                    if(RootEffectCheck(intVec, map))
                    {
                        if (!rootEffects.ContainsKey(intVec))
                        {
                            // 创建树根特校
                            Mote value = MoteMaker.MakeStaticMote(intVec.ToVector3Shifted(), map, RimWorld.ThingDefOf.Mote_HarbingerTreeRoots, 0.8f, makeOffscreen: false, Rand.Range(0f, 360f));
                            rootEffects[intVec] = value;
                        }
                        // 移除无效特效
                        else if (rootEffects.ContainsKey(intVec))
                        {
                            rootEffects[intVec]?.Destroy();
                            rootEffects.Remove(intVec);
                        }
                    }
                }
            }
        }

        // 从尸体获取营养
        private float GetNutritionFromCorpse(Corpse corpse)
        {
            // 处理腐烂尸体
            if(corpse.GetRotStage() != 0)
            {
                // 计算肉类营养
                int num = Mathf.Max(GenMath.RoundRandom(corpse.InnerPawn.GetStatValue(StatDefOf.MeatAmount)), 3);
                corpse.Destroy();
                return (float)num * 0.03f;
            }
            (from x in corpse.InnerPawn.health.hediffSet.GetNotMissingParts()
             where x.depth == BodyPartDepth.Outside && !x.def.conceptual && x != corpse.InnerPawn.RaceProps.body.corePart
             select x).TryRandomElement(out var result);
            float bodyPartNutrition;
            if(result == null)
            {
                bodyPartNutrition = FoodUtility.GetBodyPartNutrition(corpse, corpse.InnerPawn.RaceProps.body.corePart);
                corpse.Destroy();
            }
            else
            {
                bodyPartNutrition = FoodUtility.GetBodyPartNutrition(corpse, result);
                Hediff_MissingPart hediff_MissingPart = (Hediff_MissingPart)HediffMaker.MakeHediff(RimWorld.HediffDefOf.MissingBodyPart, corpse.InnerPawn, result);
                hediff_MissingPart.IsFresh = true;
                hediff_MissingPart.lastInjury = RimWorld.HediffDefOf.Digested;
                corpse.InnerPawn.health.AddHediff(hediff_MissingPart);
            }
            return bodyPartNutrition;
        }

        // 从物品堆栈获取营养
        private static float GetNutritionFromItenStack(ThingWithComps item)
        {
            float statValue = item.GetStatValue(StatDefOf.Nutrition);
            if(statValue <= 0f)
            {
                return 0f;
            }
            // 随机消耗部分物品
            int num = Mathf.Min(item.stackCount, ItemConsumCountRange.RandomInRange);
            Thing thing = item.SplitOff(num);
            float result = statValue * (float)num;
            thing.Destroy();
            return result;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nutrition, "Nutrition", 0f);
            Scribe_Values.Look(ref ConsumeCorpse, "ConsumerCorpse", defaultValue: false);
            Scribe_Values.Look(ref ConsumeFood, "ConsumeFood", defaultValue: false);
            Scribe_Values.Look(ref incubatetick, "IncubateTick", 0);
            Scribe_Values.Look(ref incubatePawn, "IncubatePawn");
            Scribe_Values.Look(ref incubatepawnName, "IncubatePawnName");
        }

        public override string CompInspectStringExtra()
        {
            string text = string.Format("{0}: {1:F1} / 1200", "Biomass".Translate(), nutrition * 3f);
            if(incubatetick > 0)
            {
                string text2 = string.Format("{0}: {1} ({2}) ", "Remaing_incubation_time".Translate(), incubatetick.ToStringTicksToPeriod(), incubatepawnName);
                text = text + "\n" + text2;
            }
            return text;
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            if(selPawn.health?.hediffSet?.hediffs?.FirstOrDefault((Hediff h) => h is Hediff_BiomassNucleus) == null)
            {
                yield break;
            }
            if(selPawn.CurJob != null && selPawn.CurJob.def == Hjx_BiomassNucleus.JobDefOf.GetBiomass && selPawn.CurJob.targetA.Thing == parent)
            {
                yield return new FloatMenuOption(" (" + "Getting_biomass".Translate() + ")", null);
                yield break; 
            }
            yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Get_biomass".Translate(),
                delegate
                {
                    Job job = JobMaker.MakeJob(Hjx_BiomassNucleus.JobDefOf.GetBiomass, parent);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }),
                selPawn,
                parent);
        }

        // 添加额外操作按钮
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach(Gizmo item2 in base.CompGetGizmosExtra())
            {
                yield return item2;
            }
            
            yield return new Command_Toggle
            {
                defaultLabel = "Consume_Corpse".Translate(),
                icon = Texture2DUtility.CourseIcon,
                isActive = () => ConsumeCorpse,
                toggleAction = delegate
                {
                    ConsumeCorpse = !ConsumeCorpse;
                    // 关闭特效
                    if (!ConsumeCorpse && !ConsumeFood && rootEffects != null)
                    {
                        foreach (KeyValuePair<IntVec3, Mote> rootEffect in rootEffects)
                            rootEffect.Value.Destroy();
                        rootEffects.Clear();
                    }
                }
            };

            yield return new Command_Toggle
            {
                defaultLabel = "Consume_Food".Translate(),
                icon = Texture2DUtility.FoodIcon,
                isActive = () => ConsumeFood,
                toggleAction = delegate
                {
                    ConsumeFood = !ConsumeFood;
                    if (!ConsumeCorpse && !ConsumeFood && rootEffects != null)
                    {
                        foreach (KeyValuePair<IntVec3, Mote> rootEffect2 in rootEffects)
                            rootEffect2.Value.Destroy();
                        rootEffects.Clear();
                    }
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "Incubate".Translate(),
                defaultDesc = "IncubateButtonDesc".Translate(),
                icon = Texture2DUtility.BornIcon,
                action = delegate
                {
                    List<FloatMenuOption> list = new List<FloatMenuOption>();

                    if (incubatetick <= 0)
                    {
                        foreach (Pawn item in PawnsFinder.All_AliveOrDead)
                        {
                            if (item.Faction == Faction.OfPlayer && item.Dead)
                            {
                                // 检查是否有生物核心
                                Hediff hediff = item.health?.hediffSet?.hediffs?.FirstOrDefault((Hediff h) => h is Hediff_BiomassNucleus);
                                if (hediff != null)
                                {
                                    // 添加复活选项
                                    list.Add(new FloatMenuOption(
                                        string.Concat("Incubate".Translate(), item.Name?.ToString()),
                                        delegate
                                        {
                                            // 检查营养值
                                            if (nutrition >= 300f)
                                            {
                                                incubatePawn = item.ThingID;
                                                incubatetick = 180000;
                                                incubatepawnName = item.Name.ToString();
                                                nutrition -= 300f;
                                            }
                                            else
                                            {
                                                Messages.Message("Need_900_biomass".Translate(),
                                                    MessageTypeDefOf.RejectInput,
                                                    historical: false);
                                            }
                                        }));
                                }
                            }
                        }
                        // 没有可用pawn时
                        if (list.Count == 0)
                            list.Add(new FloatMenuOption("BiomassNodeNoPawns".Translate(), null));
                    }
                    // 取消孵化
                    else
                    {
                        list.Add(new FloatMenuOption("cancerIncubate".Translate(),
                            delegate
                            {
                                incubatePawn = null;
                                incubatepawnName = null;
                                incubatetick = 0;
                                nutrition += 150f;
                            }));
                    }

                    // 显示菜单
                    Find.WindowStack.Add(new FloatMenu(list));
                }
            };
        }

        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            if (rootEffects == null) return;

            foreach (KeyValuePair<IntVec3, Mote> rootEffect in rootEffects)
                rootEffect.Value.Destroy();

            rootEffects.Clear();
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            BMNUtility.DrawCells(map, parent.Position, range, Color.white);
        }
    }

    public class CompProperties_AbilityBiomassCost : CompProperties_AbilityEffect
    {
        public float biomassCost;

        public CompProperties_AbilityBiomassCost()
        {
            compClass = typeof(CompAbilityEffect_BiomassCost);
        }

        public override IEnumerable<string> ExtraStatSummary()
        {
            yield return string.Concat("BiomassCost".Translate() + ": ", Mathf.RoundToInt(biomassCost).ToString());
        }
    }

    public class CompProperties_BiomassNode : CompProperties
    {
        public int ConsumeRange = 3;

        public CompProperties_BiomassNode()
        {
            compClass = typeof(CompBiomassNode);
        }
    }

    public class CompProperties_BladeSprint : CompProperties_AbilityEffect
    {
        public EffecterDef effecterDef;

        public int maintainForTicks = -1;

        public float scale;

        public CompProperties_BladeSprint()
        {
            compClass = typeof(CompAbilityEffect_BladeSprint);
        }
    }

    public class CompProperties_GroundSpike : CompProperties_AbilityEffect
    {
        public CompProperties_GroundSpike()
        {
            compClass = typeof(CompAbilityEffect_GroundSpike);
        }
    }

    public class CompProperties_Integrate : CompProperties_AbilityEffect
    {
        public CompProperties_Integrate()
        {
            compClass = typeof(CompAbilityEffect_Integrate);
        }
    }

    public class CompProperties_SpawnAndLetter : CompProperties
    {
        public int radius = 4;

        public LetterDef letterDef;

        public CompProperties_SpawnAndLetter()
        {
            compClass = typeof(CompSpawnAndLetter);
        }
    }

    public class CompProperties_UseBiomassNucleus : CompProperties_UseEffect
    {
        public HediffDef hediffDef;

        public LetterDef letterDef;

        public CompProperties_UseBiomassNucleus()
        {
            compClass = typeof(CompUseEffect_BiomassNucleus);
        }
    }

    // 生物质核心的生成与通知系统
    public class CompSpawnAndLetter : ThingComp
    {
        public bool spawnBiomassNucleus = false;

        public CompProperties_SpawnAndLetter Props => (CompProperties_SpawnAndLetter)props;

        public void SendLetter(Pawn triggerer)
        {
            Find.LetterStack.ReceiveLetter(
                "Anomaly_heart".Translate().Formatted(triggerer.Named("PAWN")),
                "Anomaly_heart_text".Translate().Formatted(triggerer.Named("PAWN")),
                Props.letterDef,
                parent
                );
            spawnBiomassNucleus = true;
        }

        public override void CompTick()
        {
            base.CompTick();
            // 生成过核心 或 未到120tick间隔
            if(spawnBiomassNucleus || !parent.IsHashIntervalTick(120))
            {
                return;
            }
            Map map = parent.Map;
            int num = GenRadial.NumCellsInRadius(Props.radius);
            for(int i = 0; i < num; i++)
            {
                // 获取辐射状扩散的第i个子坐标
                IntVec3 c = parent.Position + GenRadial.RadialPattern[i];
                if (!c.InBounds(map)) continue;
                List<Thing> thingList = c.GetThingList(map);
                for(int j = 0; j < thingList.Count; j++)
                {
                    // 筛选符合条件的殖民者
                    if(thingList[j] is Pawn pawn 
                       && pawn.IsColonistPlayerControlled
                       && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Sight)
                       && pawn.Awake()
                       && GenSight.LineOfSightToThing(pawn.Position, parent, parent.Map))
                    {
                        SendLetter(pawn);
                        Thing thing = ThingMaker.MakeThing(Hjx_BiomassNucleus.ThingDefOf.BMN_BiomassNucleus);
                        CompUseEffect_BiomassNucleus comp = thing.TryGetComp<CompUseEffect_BiomassNucleus>();
                        if(comp != null)
                        {
                            comp.copypawnID = "Alex Mercer";
                        }
                        IntVec3 spawnPos = BMNUtility.GetFirstEmptyCellInRange(parent.Position, map, 6);
                        // 生成物体
                        GenSpawn.Spawn(thing, spawnPos, map, WipeMode.VanishOrMoveAside);
                        return;
                    }
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref spawnBiomassNucleus, "spawnBiomassNucleus", defaultValue: false);
        }
    }

    public class CompUseEffect_BiomassNucleus : CompUseEffect
    {
        public Dictionary<string, string> copyRaceDictionary = new Dictionary<string, string>();

        public string copypawnID = null;

        public CompProperties_UseBiomassNucleus Props => (CompProperties_UseBiomassNucleus)props;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref copypawnID, "copypawnIDs");
            Scribe_Collections.Look(ref copyRaceDictionary, "copyRaceDictionary", LookMode.Value, LookMode.Value);
        }

        public override void DoEffect(Pawn user)
        {
            user.health.AddHediff(Props.hediffDef);
            if (user.health?.hediffSet?.hediffs?.FirstOrDefault((Hediff h) => h is Hediff_BiomassNucleus) is Hediff_BiomassNucleus hediff_BiomassNucleus)
            {
                hediff_BiomassNucleus.nucleusID = user.ThingID;
                Hediff_BiomassNucleus.onlyOne = user.ThingID;
                hediff_BiomassNucleus.pawnMap = user.Map;
                Log.Message("已绑定" + Hediff_BiomassNucleus.onlyOne);
                hediff_BiomassNucleus.Severity = 0f;
                hediff_BiomassNucleus.placeheart = false;
                if (copyRaceDictionary != null)
                {
                    hediff_BiomassNucleus.RaceDictionary = copyRaceDictionary;
                    hediff_BiomassNucleus.RaceCount = copyRaceDictionary.Count;
                    hediff_BiomassNucleus.PrintRacrList = string.Join(", ", copyRaceDictionary.Values);
                    hediff_BiomassNucleus.Severity = (float)hediff_BiomassNucleus.RaceCount * 0.01f;
                    hediff_BiomassNucleus.SetEnergyMax(200f + (float)hediff_BiomassNucleus.RaceCount * 10f);
                }
                if (copypawnID == "Alex Mercer")
                {
                    Find.LetterStack.ReceiveLetter("Use_BiomassNucleus".Translate().Formatted(user.Named("PAWN")),
                        "Use_BiomassNucleus_text".Translate().Formatted(user.Named("PAWN")),
                        Props.letterDef, parent);
                }
                copypawnID = null;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (copypawnID == null)
            {
                Messages.Message("lost_Anomaly_heart".Translate(), parent, MessageTypeDefOf.NeutralEvent);
                parent.Destroy();
            }
        }

        public override AcceptanceReport CanBeUsedBy(Pawn pawn)
        {
            if (pawn.health.hediffSet.HasHediff(Props.hediffDef))
            {
                return "AlreadyHasHediff".Translate(Props.hediffDef.label);
            }
            return true;
        }

        // 同化的种族数量
        public override string CompInspectStringExtra()
        {
            return string.Format("{0}: {1}", "Integrate_Count".Translate(), copyRaceDictionary.Count);
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            if(selPawn.CurJob != null && selPawn.CurJob.def == Hjx_BiomassNucleus.JobDefOf.ResurrectGiveBlood && selPawn.CurJob.targetA.Thing == parent)
            {
                yield return new FloatMenuOption(" (" + "Give_blood".Translate() + ")", null);
            }
            else
            {
                if(!(selPawn.def.defName != "Mincho_ThingDef"))
                {
                    yield break;
                }
                foreach(Pawn pawn in PawnsFinder.All_AliveOrDead)
                {
                    if(pawn.ThingID == copypawnID && pawn.Dead)
                    {
                        yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Give_blood".Translate() + ExtraLabelGiveBlood(selPawn, 0.3f), delegate
                        {
                            Job job = JobMaker.MakeJob(Hjx_BiomassNucleus.JobDefOf.ResurrectGiveBlood, parent, pawn);
                            selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                        }), selPawn, parent);
                        break;
                    }
                }
            }
        }

        public static void GiveBlood(Pawn selpawn, float blood)
        {
            Hediff hediff = HediffMaker.MakeHediff(RimWorld.HediffDefOf.BloodLoss, selpawn);
            hediff.Severity = 0.3f;
            selpawn.health.AddHediff(hediff);
        }

        private static float Bloodloss(Pawn selpawn, float blood)
        {
            if(selpawn.Dead || !selpawn.RaceProps.IsFlesh)
            {
                // 确认pawn的死亡情况和种族，有没有计算的必要
                return 0f;
            }
            Hediff firstHediffOfDef = selpawn.health.hediffSet.GetFirstHediffOfDef(RimWorld.HediffDefOf.BloodLoss);
            if(firstHediffOfDef != null)
            {
                blood += firstHediffOfDef.Severity;
            }
            return blood;
        }

        public string ExtraLabelGiveBlood(LocalTargetInfo selpawn, float blood)
        {
            Pawn pawn = selpawn.Pawn;
            string result = null;
            float num = Bloodloss(pawn, blood);
            if(num >= RimWorld.HediffDefOf.BloodLoss.lethalSeverity)
            {
                result = " (" + "WillKill".Translate() + ") ";
            }
            else if (RimWorld.HediffDefOf.BloodLoss.stages[RimWorld.HediffDefOf.BloodLoss.StageAtSeverity(num)].lifeThreatening)
            {
                result = " (" + "WillCauseSeriousBloodloss".Translate() + ") ";
            }
            return result;
        }
    }

    [DefOf]
    public static class FleckDefOf
    {
        public static FleckDef BMN_GroundSpike_Stab;

        public static FleckDef BMN_IntegrateSplash;
    }

    [StaticConstructorOnStartup]
    public class Gizmo_BiomassNucleus : Gizmo
    {
        private Hediff_BiomassNucleus nucleus;

        private Texture2D shieldIcon;

        private Texture2D bladeIcon;

        private Texture2D clawIcon;

        private Texture2D WorkBoxNull;

        private static readonly Texture2D FullBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.5f, 0.1f, 0.1f));

        private static readonly Texture2D EmptyBarTex = SolidColorMaterials.NewSolidColorTexture(Color.clear);

        public Gizmo_BiomassNucleus(Hediff_BiomassNucleus nucleus)
        {
            Order = -100f;
            this.nucleus = nucleus;
            shieldIcon = ContentFinder<Texture2D>.Get("Hjx_BiomassNucleus/shieldIcon");
            bladeIcon = ContentFinder<Texture2D>.Get("Hjx_BiomassNucleus/bladeIcon");
            clawIcon = ContentFinder<Texture2D>.Get("Hjx_BiomassNucleus/clawIcon");
            WorkBoxNull = ContentFinder<Texture2D>.Get("Hjx_BiomassNucleus/WorkBoxNull");
        }

        public override float GetWidth(float maxWidth)
        {
            return 160f;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            //1.绘制基础窗口框架
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Rect rect2 = rect.ContractedBy(7f);
            Widgets.DrawWindowBackground(rect);

            //2.标题与文本设置
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft; //左对齐
            Rect rect3 = new Rect(rect2.x, rect2.y, 70f, Text.LineHeight);
            Widgets.Label(new Rect(rect2.x, rect2.y, 70f, Text.LineHeight), "Biomass".Translate());

            //3.能量进度条系统
            float energy = nucleus.GetEnergy();
            float energyMax = nucleus.GetEnergyMax();
            float num = energy / Mathf.Max(1f, energyMax);
            Rect position = new Rect(rect2.x, rect2.y + 31f, rect2.width, 29f);
            Rect rect4 = position.ContractedBy(3f, 5f); //进度条内边距
            GUI.DrawTexture(position, SolidColorMaterials.NewSolidColorTexture(Color.black));
            Widgets.FillableBar(rect4, num, FullBarTex, EmptyBarTex, doBorder: false);

            //4.能量刻度标记
            float[] array = new float[3] { 25f, 50f, 75f };
            float[] array2 = array;
            foreach(float num2 in array2)
            {
                float num3 = num2 / 100f;
                Rect position2 = new Rect(rect4.x + rect4.width * num3 - 1f, rect4.y + rect4.height - 6f, 2f, 6f);
                GUI.DrawTexture(position2, (num < num3) ? BaseContent.GreyTex : BaseContent.BlackTex);
            }

            //5.数值显示与工具提示
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect4, $"{energy:F0} / {energyMax:F0}");
            TooltipHandler.TipRegion(rect3, string.Format(
                "{0}: {1} \n{2}",
                "Integrate_count".Translate(),
                nucleus.RaceCount,
                nucleus.PrintRacrList));
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            if (Mouse.IsOver(rect4))
            {
                Widgets.DrawHighlight(rect4);
            }

            //6.形态切换按钮组
            float num4 = 25f;
            float x = rect2.xMax - (num4 * 3f + 2f); //右对齐布局
            float yMin = rect2.yMin;
            float num5 = 1f;
            float num6 = 12f;
            Rect butRect = new Rect(x, yMin, num4, num4);
            Rect position3 = new Rect(butRect.xMax - num6, butRect.y, num6, num6);
            Rect butRect2 = new Rect(butRect.xMax + num5, yMin, num4, num4);
            Rect position4 = new Rect(butRect2.xMax - num6, butRect2.y, num6, num6);
            Rect butRect3 = new Rect(butRect2.xMax + num5, yMin, num4, num4);
            Rect position5 = new Rect(butRect3.xMax - num6, butRect3.y, num6, num6);

            // 护盾按钮逻辑
            if(Widgets.ButtonImage(butRect, shieldIcon, doMouseoverSound: true, "shieldIcon_desc".Translate()))
            {
                nucleus.shieldOn = !nucleus.shieldOn;
                if (nucleus.shieldOn)
                {
                    if (nucleus.clawL)
                    {
                        nucleus.clawL = false;
                        BMNUtility.RemoveFirstHediff(nucleus.pawn, HediffDefOf.Hediff_clawformL, "LeftShoulder");
                    }
                    nucleus.CheckAndApplyShield();
                }
                else
                {
                    BMNUtility.RemoveFirstHediff(nucleus.pawn, HediffDefOf.Hediff_shieldform);
                }
            }
            GUI.DrawTexture(position3, nucleus.shieldOn ? Widgets.CheckboxOnTex : WorkBoxNull);

            // 刀刃按钮逻辑
            if(Widgets.ButtonImage(butRect2, bladeIcon, doMouseoverSound: true, "bladeIcon_desc".Translate()))
            {
                nucleus.bladeOn = !nucleus.bladeOn;
                if (nucleus.bladeOn)
                {
                    if (nucleus.clawR)
                    {
                        nucleus.clawR = false;
                        BMNUtility.RemoveFirstHediff(nucleus.pawn, HediffDefOf.Hediff_clawformR, "RightShoulder");
                    }
                    nucleus.CheckAndApplyBlade();
                }
                else
                {
                    BMNUtility.RemoveFirstHediff(nucleus.pawn, HediffDefOf.Hediff_bladeform);
                }
            }
            GUI.DrawTexture(position4, nucleus.bladeOn ? Widgets.CheckboxOnTex : WorkBoxNull);

            // 爪子按钮逻辑
            if(Widgets.ButtonImage(butRect3, clawIcon, doMouseoverSound: true, "clawIcon_desc".Translate()))
            {
                if(nucleus.clawR || nucleus.clawL)
                {
                    nucleus.clawR = false;
                    nucleus.clawL = false;
                    BMNUtility.RemoveFirstHediff(nucleus.pawn, HediffDefOf.Hediff_clawformL, "LeftShoulder");
                    BMNUtility.RemoveFirstHediff(nucleus.pawn, HediffDefOf.Hediff_clawformR, "RightShoulder");
                }
                else
                {
                    if (!nucleus.bladeOn)
                    {
                        nucleus.clawR = true;
                        nucleus.CheckAndApplyClaw_R();
                    }
                    if (!nucleus.shieldOn)
                    {
                        nucleus.clawL = true;
                        nucleus.CheckAndApplyClaw_L();
                    }
                }
            }
            GUI.DrawTexture(position5, (nucleus.clawR || nucleus.clawL) ? Widgets.CheckboxOnTex : WorkBoxNull);
            return new GizmoResult(GizmoState.Clear); // 切除残留UI
        }
    }

    public class Hediff_BiomassNucleus : HediffWithComps
    {
        private float energy = 200f;

        private float energyMax = 200f;

        // 核心唯一标识
        public string nucleusID = null;

        // 全局唯一核心持有者ID
        public static string onlyOne;

        public bool clawR = false;

        public bool clawL = false;

        public bool bladeOn = false;

        public bool shieldOn = false;

        public Dictionary<string, string> RaceDictionary = new Dictionary<string, string>();

        // 集成种族数量
        public int RaceCount = 0;

        // 种族列表可视化字符串
        public string PrintRacrList;

        // 是否已放置核心物品
        public bool placeheart = false;

        public Map pawnMap;

        public static float GroundSpikeArmor = 0.8f;

        public static float GroundSpikePower = 24f;

        public float ClawPower => 12f * BiomassNucleusMod.settings.clawpowerSetting;

        public float ClawArmorPenetration => (0.38f + (float)Math.Min(RaceCount, 50) * 0.015f) * BiomassNucleusMod.settings.clawArmorPenetration;

        public float ClawCooldownTime
        {
            get
            {
                float num = 12f / (7.5f + (float)Math.Min(RaceCount, 50) * 0.65f);
                return (float)Math.Floor(num * 100f) / 100f;
            }
        }

        public float BladePower => (27f + (float)Math.Min(RaceCount, 50)) * BiomassNucleusMod.settings.bladepowerSetting;

        public float BladeArmorPenetration => (0.76f + (float)RaceCount * 0.02f) * BiomassNucleusMod.settings.bladeArmorPenetration;

        public float BladeCooldownTime => 2f - (float)Math.Min(RaceCount, 40) * 0.01f;

        public override bool ShouldRemove
        {
            get
            {
                if(pawn.ThingID != onlyOne && !pawn.Dead)
                {
                    Messages.Message(pawn.Name.ToString() + "lost_BiomassNucleus".Translate(), pawn, MessageTypeDefOf.NeutralEvent);
                    return true;
                }
                return false;
            }
        }

        public float GetEnergy()
        {
            return energy;
        }

        public void SetEnergy(float amount)
        {
            energy = Mathf.Clamp(amount, 0f, energyMax);
        }

        public float GetEnergyMax()
        {
            return energyMax;
        }

        public void SetEnergyMax(float amount)
        {
            energyMax = amount;
        }

        // 序列化与存档逻辑
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref energy, "Energy", 0f);
            Scribe_Values.Look(ref energyMax, "EnergyMax", 0f);
            Scribe_Values.Look(ref clawR, "ClawR", defaultValue: false);
            Scribe_Values.Look(ref clawL, "ClawL", defaultValue: false);
            Scribe_Values.Look(ref bladeOn, "BladeOn", defaultValue: false);
            Scribe_Values.Look(ref shieldOn, "ShieldOn", defaultValue: false);
            Scribe_Collections.Look(ref RaceDictionary, "RaceDictionary", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref RaceCount, "RaceCount", 0);
            Scribe_Values.Look(ref PrintRacrList, "PrintRacrList");
            Scribe_Values.Look(ref nucleusID, "nucleusID");
            Scribe_Values.Look(ref placeheart, "PlaceHeart", defaultValue: false);
            if(Scribe.mode != LoadSaveMode.PostLoadInit)
            {
                return;
            }
            if(nucleusID != null && nucleusID == pawn.ThingID && !pawn.Dead)
            {
                onlyOne = nucleusID;
                BMNUtility.InitializeVerbTool("Hediff_clawformR", delegate (Tool tool)
                {
                    tool.power = ClawPower;
                    tool.armorPenetration = ClawArmorPenetration;
                    tool.cooldownTime = ClawCooldownTime;

                });
                BMNUtility.InitializeVerbTool("Hediff_clawformL", delegate (Tool tool)
                {
                    tool.power = ClawPower;
                    tool.armorPenetration = ClawArmorPenetration;
                    tool.cooldownTime = ClawCooldownTime;
                });
                BMNUtility.InitializeVerbTool("Hediff_bladeform", delegate (Tool tool)
                {
                    tool.power = BladePower;
                    tool.armorPenetration = BladeArmorPenetration;
                    tool.cooldownTime = BladeCooldownTime;
                });
                GroundSpikeArmor = ClawArmorPenetration * 1.4f + 0.2f;
                GroundSpikePower = (24f + (float)Math.Min(RaceCount, 50) * 0.5f) * BiomassNucleusMod.settings.clawpowerSetting;
                Log.Message("Set name:" + pawn.Name?.ToString() + ",Nucleus:" + nucleusID + ",Pawn:" + pawn.ThingID);
            }
            if(RaceDictionary == null)
            {
                RaceDictionary = new Dictionary<string, string>();
            }
            // 刷新活跃模组
            BMNUtility.RecacheActiveMods();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if(pawn != null && pawn.Spawned && !pawn.Dead && (pawn.Faction == Faction.OfPlayer || DebugSettings.ShowDevGizmos))
            {
                yield return new Gizmo_BiomassNucleus(this);
            }
        }

        public void CheckAndApplyClaw_R()
        {
            if(energy >= 15f)
            {
                if(BMNUtility.TryGiveMutation(pawn, Hjx_BiomassNucleus.HediffDefOf.Hediff_clawformR, "RightShoulder"))
                {
                    energy -= 15f;
                    UpdateClawR();
                }
            }
            else
            {
                Messages.Message("Insufficient_biomass".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.RejectInput);
                clawR = false;
            }
        }

        public void CheckAndApplyClaw_L()
        {
            if(energy >= 15f)
            {
                if(BMNUtility.TryGiveMutation(pawn, Hjx_BiomassNucleus.HediffDefOf.Hediff_clawformL, "LeftShoulder"))
                {
                    energy -= 15f;
                    UpdateClawL();
                }
            }
            else
            {
                Messages.Message("Insufficient_biomass".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.RejectInput);
                clawL = false;
            }
        }

        public void CheckAndApplyBlade()
        {
            if (energy >= 30f)
            {
                if (BMNUtility.TryGiveMutation(pawn, Hjx_BiomassNucleus.HediffDefOf.Hediff_bladeform, "RightShoulder"))
                {
                    energy -= 30f;
                    UpdateBlade();
                }
            }
            else
            {
                Messages.Message("Insufficient_biomass".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.RejectInput);
                bladeOn = false;
            }
        }

        public void CheckAndApplyShield()
        {
            if(energy >= 30f)
            {
                if(BMNUtility.TryGiveMutation(pawn, Hjx_BiomassNucleus.HediffDefOf.Hediff_shieldform, "LeftShoulder"))
                {
                    energy -= 30f;
                }
            }
            else
            {
                Messages.Message("Insufficient_biomass".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.RejectInput);
                shieldOn = false;
            }
        }

        public void UpdateTargetPawnDef(Pawn target)
        {
            if(target != null)
            {
                string text = target?.def?.defName;
                string text2 = target?.def?.label;
                if(text == null)
                {
                    text = "Unknow";
                }
                if(text2 == null)
                {
                    text2 = "ERR" + (RaceCount + 1);
                }
                if (!RaceDictionary.ContainsKey(text))
                {
                    RaceDictionary[text] = text2;
                    Messages.Message("Integrate_count".Translate() + "+1", pawn, MessageTypeDefOf.NeutralEvent);
                }
                RaceCount = RaceDictionary.Count;
                PrintRacrList = string.Join(", ", RaceDictionary.Values);
                UpdateAll();
            }
        }

        public void UpdateClawL()
        {
            BMNUtility.DynamicVerbTool(pawn, "Hediff_clawformL", delegate (Tool tool)
            {
                tool.power = ClawPower;
                tool.armorPenetration = ClawArmorPenetration;
                tool.cooldownTime = ClawCooldownTime;
            });
            GroundSpikeArmor = ClawArmorPenetration * 1.4f + 0.2f;
            GroundSpikePower = (24f + (float)Math.Min(RaceCount, 50) * 0.5f) * BiomassNucleusMod.settings.clawpowerSetting;
        }

        public void UpdateClawR()
        {
            BMNUtility.DynamicVerbTool(pawn, "Hediff_clawformR", delegate (Tool tool)
            {
                tool.power = ClawPower;
                tool.armorPenetration = ClawArmorPenetration;
                tool.cooldownTime = ClawCooldownTime;
            });
            GroundSpikeArmor = ClawArmorPenetration * 1.4f + 0.2f;
            GroundSpikePower = (24f + (float)Math.Min(RaceCount, 50) * 0.5f) * BiomassNucleusMod.settings.clawpowerSetting;
        }

        public void UpdateBlade()
        {
            BMNUtility.DynamicVerbTool(pawn, "Hediff_bladeform", delegate (Tool tool)
            {
                tool.power = BladePower;
                tool.armorPenetration = BladeArmorPenetration;
                tool.cooldownTime = BladeCooldownTime;
            });
        }

        public void UpdateAll()
        {
            Severity = (float)RaceCount * 0.02f;
            energyMax = 200f + (float)RaceCount * 10f;
            UpdateClawL();
            UpdateClawR();
            UpdateBlade();
        }

        public override void Notify_Spawned()
        {
            base.Notify_Spawned();
            if(nucleusID != null && nucleusID == pawn.ThingID)
            {
                onlyOne = nucleusID;
                pawnMap = pawn.Map;
            }
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            if(!placeheart && nucleusID != null && nucleusID == pawn.ThingID)
            {
                Thing thing = ThingMaker.MakeThing(Hjx_BiomassNucleus.ThingDefOf.BMN_BiomassNucleus);
                CompUseEffect_BiomassNucleus compUseEffect_BiomassNucleus = thing.TryGetComp<CompUseEffect_BiomassNucleus>();
                if(compUseEffect_BiomassNucleus != null)
                {
                    compUseEffect_BiomassNucleus.copyRaceDictionary = RaceDictionary;
                    compUseEffect_BiomassNucleus.copypawnID = nucleusID;
                }
                if(pawnMap == null)
                {
                    pawnMap = pawn.Corpse?.Map;
                }
                IntVec3 firstEmptyCellInRange = BMNUtility.GetFirstEmptyCellInRange(pawn.Position, pawnMap, 6);
                // 生成生物质核心实体
                GenSpawn.Spawn(thing, firstEmptyCellInRange, pawnMap, WipeMode.VanishOrMoveAside);
                nucleusID = null;
                onlyOne = null;
                placeheart = true;
            }
        }

        // 移除生物质核心状态
        public override void PostRemoved()
        {
            base.PostRemoved();
            BMNUtility.RemoveFirstHediff(pawn, Hjx_BiomassNucleus.HediffDefOf.Hediff_clawformR);
            BMNUtility.RemoveFirstHediff(pawn, Hjx_BiomassNucleus.HediffDefOf.Hediff_clawformL);
            BMNUtility.RemoveFirstHediff(pawn, Hjx_BiomassNucleus.HediffDefOf.Hediff_bladeform);
            BMNUtility.RemoveFirstHediff(pawn, Hjx_BiomassNucleus.HediffDefOf.Hediff_shieldform);
            if(!placeheart && nucleusID != null)
            {
                Thing thing = ThingMaker.MakeThing(Hjx_BiomassNucleus.ThingDefOf.BMN_BiomassNucleus);
                CompUseEffect_BiomassNucleus compUseEffect_BiomassNucleus = thing.TryGetComp<CompUseEffect_BiomassNucleus>();
                if(compUseEffect_BiomassNucleus != null)
                {
                    compUseEffect_BiomassNucleus.copyRaceDictionary = RaceDictionary;
                    compUseEffect_BiomassNucleus.copypawnID = nucleusID;
                }
                if(pawnMap == null)
                {
                    pawnMap = pawn.Map;
                }
                IntVec3 firstEmptyCellInRange = BMNUtility.GetFirstEmptyCellInRange(pawn.Position, pawnMap, 6);
                GenSpawn.Spawn(thing, firstEmptyCellInRange, pawnMap, WipeMode.VanishOrMoveAside);
                nucleusID = null;
                onlyOne = null;
                placeheart = true;
            }
        }
    }

    public class Hediff_BiomassPartBase : Hediff_AddedPart
    {
        public Hediff_BiomassNucleus baseNucleus;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            baseNucleus = pawn?.health?.hediffSet?.hediffs?.FirstOrDefault((Hediff h) => h is Hediff_BiomassNucleus) as Hediff_BiomassNucleus;
            if (!BiomassNucleusMod.settings.NoMissingPart)
            {
                return;
            }
            pawn.health.RestorePart(base.Part, this, checkStateChange: false);
            for(int i = 0; i < base.Part.parts.Count; i++)
            {
                BodyPartRecord bodyPartRecord = base.Part.parts[i];
                for(int num = pawn.health.hediffSet.hediffs.Count - 1; num >= 0; num--)
                {
                    Hediff hediff = pawn.health.hediffSet.hediffs[num];
                    if(hediff.Part == bodyPartRecord && hediff.def == RimWorld.HediffDefOf.MissingBodyPart)
                    {
                        pawn.health.RemoveHediff(hediff);
                    }
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if(Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                baseNucleus = pawn?.health?.hediffSet?.hediffs?.FirstOrDefault((Hediff h) => h is Hediff_BiomassNucleus) as Hediff_BiomassNucleus; 
            }
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            if (BiomassNucleusMod.settings.NoMissingPart)
            {
                return;
            }
            pawn.health.RestorePart(base.Part, this, checkStateChange: false);
            for(int i = 0; i < base.Part.parts.Count; i++)
            {
                BodyPartRecord bodyPartRecord = base.Part.parts[i];
                for(int num = pawn.health.hediffSet.hediffs.Count - 1; num >= 0; num--)
                {
                    Hediff hediff = pawn.health.hediffSet.hediffs[num];
                    if(hediff.Part == bodyPartRecord && hediff.def == RimWorld.HediffDefOf.MissingBodyPart)
                    {
                        pawn.health.RemoveHediff(hediff);
                    }
                }
            }
        }

        public void InitializeComps()
        {
            if(def.comps == null)
            {
                return;
            }
            comps = new List<HediffComp>();
            for(int i = 0; i < def.comps.Count; i++)
            {
                HediffComp hediffComp = null;
                try
                {
                    hediffComp = (HediffComp)Activator.CreateInstance(def.comps[i].compClass);
                    hediffComp.props = def.comps[i];
                    hediffComp.parent = this;
                    comps.Add(hediffComp);
                }
                catch(Exception ex)
                {
                    Log.Error("Could not instantiate or initialize a HediffComp: " + ex);
                    comps.Remove(hediffComp);
                }
            }
        }
    }

    public class Hediff_BiomassPartBlade : Hediff_BiomassPartBase
    {
        public override string TipStringExtra
        {
            get
            {
                if(baseNucleus == null)
                {
                    return base.TipStringExtra;
                }
                // 构建包含详细参数的提示信息
                string tipStringExtra = base.TipStringExtra;
                StringBuilder stringBuilder = new StringBuilder(tipStringExtra);
                stringBuilder.AppendLine();
                stringBuilder.Append($"  Lv {Math.Min(baseNucleus.RaceCount, 50)}  /  50 \n" +
                    string.Format("  - {0}:  {1}  /  {2:F1}\n",
                        "Damage".Translate(),
                        baseNucleus.BladePower,
                        77f * BiomassNucleusMod.settings.bladepowerSetting) +
                    string.Format("  - {0}:  {1:F0}%  /  ∞\n",
                        "ArmorPenetration".Translate(),
                        baseNucleus.BladeArmorPenetration * 100f) +
                    string.Format("  - {0}:  {1:F1}  /  1.6",
                        "CooldownTime".Translate(),
                        baseNucleus.BladeCooldownTime));

                return stringBuilder.ToString();
            }
        }

        public override void PreRemoved()
        {
            base.PreRemoved();

            if(pawn?.health?.hediffSet?.hediffs?.FirstOrDefault((Hediff h) => h is Hediff_BiomassNucleus) is Hediff_BiomassNucleus hediff_BiomassNucleus)
            {
                // 计算当前部位健康比例
                float partHealth = pawn.health.hediffSet.GetPartHealth(base.Part);
                if(partHealth > 0f)
                {
                    float maxHealth = base.Part.def.GetMaxHealth(pawn);
                    // 按比例回收能量
                    hediff_BiomassNucleus.SetEnergy(hediff_BiomassNucleus.GetEnergy() + partHealth / maxHealth * 30f);
                }
            }
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            if(pawn?.health?.hediffSet?.hediffs?.FirstOrDefault((Hediff h) => h is Hediff_BiomassNucleus) is Hediff_BiomassNucleus hediff_BiomassNucleus)
            {
                hediff_BiomassNucleus.bladeOn = false; // 关闭刀刃激活状态
            }
            // 移除冲刺能力
            pawn.abilities.RemoveAbility(AbilityDefOf.BMN_BladeSprint);
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            if(baseNucleus != null)
            {
                pawn.abilities.GainAbility(AbilityDefOf.BMN_BladeSprint);
            }
        }
    }

    public class Hediff_BiomassPartClaw : Hediff_BiomassPartBase
    {
        public override string TipStringExtra
        {
            get
            {
                if(baseNucleus == null)
                {
                    return base.TipStringExtra;
                }
                // 构建多行属性面板
                string tipStringExtra = base.TipStringExtra;
                StringBuilder stringBuilder = new StringBuilder(tipStringExtra);
                stringBuilder.AppendLine();
                // 动态显示爪属性
                stringBuilder.Append($"  Lv {Math.Min(baseNucleus.RaceCount, 50)}  /  50 \n" +
                    string.Format("  - {0}:  {1:F1}  ( Max )\n", 
                        "Damage".Translate(), 
                        baseNucleus.ClawPower) + 
                    string.Format("  - {0}:  {1:F0}%  /  {2:F0}%\n", 
                        "ArmorPenetration".Translate(),
                        baseNucleus.ClawArmorPenetration * 100f,
                        113f * BiomassNucleusMod.settings.clawArmorPenetration) + 
                    string.Format("  - {0}:  {1:F1}  /  0.3", 
                        "CooldownTime".Translate(), 
                        baseNucleus.ClawCooldownTime));
                return stringBuilder.ToString();

            }
        }

        public override void PreRemoved()
        {
            base.PreRemoved();
            if(pawn?.health?.hediffSet?.hediffs?.FirstOrDefault((Hediff h) => h is Hediff_BiomassNucleus) is Hediff_BiomassNucleus hediff_BiomassNucleus)
            {
                float partHealth = pawn.health.hediffSet.GetPartHealth(base.Part);
                if(partHealth > 0f)
                {
                    float maxHealth = base.Part.def.GetMaxHealth(pawn);
                    hediff_BiomassNucleus.SetEnergy(hediff_BiomassNucleus.GetEnergy() + partHealth / maxHealth * 15f);
                }
            }
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            Hediff_BiomassNucleus hediff_BiomassNucleus = pawn?.health?.hediffSet?.hediffs?.FirstOrDefault((Hediff h) => h is Hediff_BiomassNucleus) as Hediff_BiomassNucleus;
            if(hediff_BiomassNucleus != null)
            {
                if((base.Part.woundAnchorTag == null && BMNUtility.HARraceCheck(base.Part, "RightShoulder")) ||
                    base.Part.woundAnchorTag == "RightShoulder")
                {
                    hediff_BiomassNucleus.clawR = false;
                }
                else if ((base.Part.woundAnchorTag == null && BMNUtility.HARraceCheck(base.Part, "LeftShoulder")) ||
                    base.Part.woundAnchorTag == "LeftShoulder")
                {
                    hediff_BiomassNucleus.clawL = false;
                }
            }

            if(hediff_BiomassNucleus == null || (!hediff_BiomassNucleus.clawR && !hediff_BiomassNucleus.clawL))
            {
                pawn.abilities.RemoveAbility(AbilityDefOf.BMN_GroundSpike);
            }
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            if(baseNucleus != null)
            {
                pawn.abilities.GainAbility(AbilityDefOf.BMN_GroundSpike);
            }
        }
    }

    public class Hediff_BiomassPartShield : Hediff_BiomassPartBase
    {
        private float energy = 114.514f;

        public override bool ShouldRemove
        {
            get
            {
                if(energy <= 0f)
                {
                    return true;
                }
                return false;
            }
        }

        public override void Notify_Regenerated(float hp)
        {
            if(baseNucleus != null)
            {
                // 从核心获取当前总能量
                energy = baseNucleus.GetEnergy();
                // 消耗对应能量
                baseNucleus.SetEnergy(energy - hp);
            }
            else
            {
                energy = 0f;
            }
        }

        public override void PreRemoved()
        {
            base.PreRemoved();
            if(baseNucleus != null)
            {
                float partHealth = pawn.health.hediffSet.GetPartHealth(base.Part);
                if(partHealth > 0f)
                {
                    float maxHealth = base.Part.def.GetMaxHealth(pawn);
                    baseNucleus.SetEnergy(baseNucleus.GetEnergy() + partHealth / maxHealth * 30f);
                }
                baseNucleus.shieldOn = false;
            }
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            if(baseNucleus != null)
            {
                energy = baseNucleus.GetEnergy();
            }
            else
            {
                energy = 0f;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if(Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if(baseNucleus != null)
                {
                    energy = baseNucleus.GetEnergy();
                }
                else
                {
                    energy = 0f;
                }
            }
        }
    }

    [DefOf]
    public static class HediffDefOf
    {
        public static HediffDef Hediff_clawformR;

        public static HediffDef Hediff_clawformL;

        public static HediffDef Hediff_bladeform;

        public static HediffDef Hediff_shieldform;

        public static HediffDef Hediff_BiomassNucleus;

        static HediffDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(Hjx_BiomassNucleus.HediffDefOf));
        }
    }

    [DefOf]
    public static class JobDefOf
    {
        public static JobDef GetBiomass;

        public static JobDef ResurrectGiveBlood;

        static JobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(Hjx_BiomassNucleus.JobDefOf));
        }
    }

    public class JobDriver_GetBiomass : JobDriver
    {
        private const TargetIndex ChargeInd = TargetIndex.A;

        // 为作业目标加锁
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A).Thing, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 基础检验
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            // 步骤1：移动到生物质节点位置
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            // 步骤2：等待0.55秒
            yield return Toils_General.Wait(0.55f.SecondsToTicks());
            // 步骤3：核心吸收阶段等待1.5秒
            yield return Toils_General.Wait(1.5f.SecondsToTicks());
            // 步骤4：执行能量吸收的核心逻辑
            yield return Toils_General.Do(delegate
            {
                float num = ((job.GetTarget(TargetIndex.A).Thing?.TryGetComp<CompBiomassNode>())?.Nutrition).Value;
                Hediff_BiomassNucleus hediff_BiomassNucleus = pawn.health.hediffSet?.hediffs?.FirstOrDefault((Hediff h) => h is Hediff_BiomassNucleus) as Hediff_BiomassNucleus;
                // 获取当前能量和最大能量值
                float energy = hediff_BiomassNucleus.GetEnergy();
                float energyMax = hediff_BiomassNucleus.GetEnergyMax();
                // 计算可吸收量
                float num2 = Math.Min(energyMax - energy, num * 3f);
                hediff_BiomassNucleus.SetEnergy(energy + num2);
                CompBiomassNode compBiomassNode = job.GetTarget(TargetIndex.A).Thing?.TryGetComp<CompBiomassNode>();
                if (compBiomassNode != null)
                {
                    compBiomassNode.Nutrition -= num2 / 3f;
                }
            });
            // 步骤5：操作完成后等待0.35秒
            yield return Toils_General.Wait(0.35f.SecondsToTicks());
        }
    }

    public class JobDriver_ResurrectGiveBlood : JobDriver
    {
        // 此为心脏
        private const TargetIndex ChargeInd = TargetIndex.A;

        private string pawnID = "";

        protected Thing Heart => job.targetA.Thing;

        // 复活目标
        protected Pawn TargetP => job.targetB.Pawn;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pawnID, "pawnID");
        }

        // 预保留阶段
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A).Thing, job, 1, -1, null, errorOnFailed);
        }

        // 创建任务流程步骤序列
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            // 记录目标角色ID
            if(TargetP != null)
            {
                pawnID = job.targetB.Pawn.ThingID;
            }
            // 步骤1：移动到心脏位置
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            // 步骤2：短等待
            yield return Toils_General.Wait(0.25f.SecondsToTicks());
            // 步骤3：带特效的长时间等待
            Toil toil = Toils_General.Wait(1.5f.SecondsToTicks());
            toil.WithEffect(() => EffecterDefOf.MeatExplosionTiny, job.GetTarget(TargetIndex.A).Thing);
            yield return toil;
            // 步骤4：执行复活核心逻辑
            yield return Toils_General.Do(delegate
            {
                Pawn pawn = null;
                // 优先使用直接指定的目标
                if (TargetP != null)
                {
                    pawn = TargetP;
                }
                else
                {
                    foreach (Pawn item in PawnsFinder.All_AliveOrDead)
                    {
                        if (item.ThingID == pawnID && item.Dead)
                        {
                            pawn = item;
                            break;
                        }
                    }
                    // 找不到有效目标则任务失败
                    if (pawn == null)
                    {
                        toil.actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                    }
                }
                // 执行复活仪式
                BMNUtility.ResurrectPawnFromVoid(base.Map, Heart.PositionHeld, pawn);

                // 处理生物质核心状态
                if (pawn.health?.hediffSet?.hediffs?.FirstOrDefault((Hediff h) => h is Hediff_BiomassNucleus) is Hediff_BiomassNucleus hediff_BiomassNucleus)
                {
                    hediff_BiomassNucleus.placeheart = false;
                    hediff_BiomassNucleus.nucleusID = pawn.ThingID;
                    Hediff_BiomassNucleus.onlyOne = pawn.ThingID;
                    Heart.Destroy(); // 销毁使用的心脏物品
                }
                CompUseEffect_BiomassNucleus.GiveBlood(base.pawn, 0.3f);
            });
            // 步骤5：收尾等待
            yield return Toils_General.Wait(0.1f.SecondsToTicks());
        }
    }

    public class PawnRenderNode_Spastic : PawnRenderNode
    {
        public class SpasmData
        {
            public float rotationStart; // 起始旋转角度
            public float rotationTarget; // 目标旋转角度
            public float scaleStart; // 起始缩放比例
            public float scaleTarget; // 目标缩放比例
            public Vector3 offsetStart; // 起始位移偏移量
            public Vector3 offsetTarget; // 目标位移偏移量
            public int tickStart; // 动画开始时的游戏Tick数
            public int nextSpasm; // 下一次痉挛发生的Tick数
            public float duration; // 动画持续随机

            public SpasmData()
            {
                duration = 1f;
                scaleStart = (scaleStart = 1f);
            }
        }

        protected SpasmData spasmData;

        public PawnRenderNode_Spastic(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
        {
        }

        // 生成网格集合：创建平面网格用于渲染
        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            return new GraphicMeshSet(MeshPool.GridPlane(props.overrideMeshSize ?? props.drawSize));
        }

        // 检查并执行痉挛逻辑动画
        public bool CheckAndDoSpasm(PawnDrawParms parms, out SpasmData dat, out float progress)
        {
            if(parms.pawn.Dead || 
                !(props is Hjx_BiomassNucleus.PawnRenderNodeProperties_Spastic pawnRenderNodeProperties_Spastic) || 
                parms.Portrait ||
                parms.Cache)
            {
                progress = 0f;
                dat = null;
                return false;
            }
            // 初始化痉挛数据
            if(spasmData == null)
            {
                spasmData = new SpasmData();
            }
            // 到达痉挛触发时间
            if(Find.TickManager.TicksGame >= spasmData.nextSpasm)
            {
                spasmData.tickStart = Find.TickManager.TicksGame;
                spasmData.duration = GetNextSpasmDurationTicks();
                spasmData.nextSpasm = GetNexSpasmTick();
                // 动画参数随机化
                spasmData.rotationStart = spasmData.rotationTarget;
                spasmData.rotationTarget = pawnRenderNodeProperties_Spastic.rotationRange.RandomInRange;

                spasmData.scaleStart = spasmData.scaleTarget;
                spasmData.scaleTarget = pawnRenderNodeProperties_Spastic.scaleRange.RandomInRange;

                spasmData.offsetStart = spasmData.offsetTarget;
                spasmData.offsetTarget = new Vector3(
                    pawnRenderNodeProperties_Spastic.offsetRangeX.RandomInRange,
                    0f,
                    pawnRenderNodeProperties_Spastic.offsetRangeZ.RandomInRange);
            }
            // 计算动画进度
            progress = (float)(Find.TickManager.TicksGame - spasmData.tickStart) / Mathf.Max(spasmData.duration, 0.0001f);
            dat = spasmData;
            return true;
        }

        protected virtual int GetNexSpasmTick()
        {
            if(props is Hjx_BiomassNucleus.PawnRenderNodeProperties_Spastic pawnRenderNodeProperties_Spastic)
            {
                return spasmData.tickStart +
                    (int)spasmData.duration +
                    pawnRenderNodeProperties_Spastic.nextSpasmTicksRange.RandomInRange;
            }
            return 0;
        }

        protected virtual int GetNextSpasmDurationTicks()
        {
            if(props is Hjx_BiomassNucleus.PawnRenderNodeProperties_Spastic pawnRenderNodeProperties_Spastic)
            {
                return pawnRenderNodeProperties_Spastic.durationTicksRange.RandomInRange;
            }
            return 0;
        }

        // 获取渲染用网格(带翻转逻辑)
        public override Mesh GetMesh(PawnDrawParms parms)
        {
            if (meshSet == null) return null;
            Mesh mesh = meshSet.MeshAt(parms.facing);
            bool flag = FlipGraphic;
            // 根据绘制数据决定是否翻转
            if(base.Props.drawData != null &&
                base.Props.drawData.FlipForRot(parms.facing))
            {
                flag = !flag;
            }
            // 执行网格翻转
            if (flag)
            {
                mesh = MeshPool.GridPlaneFlip(MeshPool.SizeOf(mesh));
            }
            return mesh;
        }
    }

    public class PawnRenderNodeProperties_Spastic : PawnRenderNodeProperties
    {
        // 是否根据角色朝向进行旋转
        public bool rotateFacing = true;

        // 缩放比例变化范围配置
        public FloatRange scaleRange = FloatRange.One;

        // 旋转角度变化范围配置(0,无旋转变化)
        public FloatRange rotationRange = FloatRange.Zero;

        // x轴水平偏移量变化范围配置(0,无水平偏移)
        public FloatRange offsetRangeX = FloatRange.Zero;

        // z轴深度偏移量变化范围配置(0,无深度偏移)
        public FloatRange offsetRangeZ = FloatRange.Zero;

        // 痉挛动画持续时间范围配置
        public IntRange durationTicksRange = new IntRange(60, 60);

        // 下一次痉挛触发间隔范围配置
        public IntRange nextSpasmTicksRange = new IntRange(60, 60);

        public PawnRenderNodeProperties_Spastic()
        {
            nodeClass = typeof(Hjx_BiomassNucleus.PawnRenderNode_Spastic);
            workerClass = typeof(Hjx_BiomassNucleus.PawnRenderNodeWorker_Spastic);
        }
    }

    public class PawnRenderNodeWorker_Spastic : PawnRenderNodeWorker
    {
        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 anchorOffset = Vector3.zero; // 锚点偏移量初始化
            pivot = PivotFor(node, parms); // 获取枢轴点
            if(node.Props.drawData != null)
            {
                // 如果是伤痕节点且使用伤痕锚点
                if(node.hediff != null && node.Props.drawData.useHediffAnchor)
                {
                    foreach(BodyTypeDef.WoundAnchor item in PawnDrawUtility.FindAnchors(parms.pawn, node.hediff.Part))
                    {
                        // 检查锚点是否可用
                        if(PawnDrawUtility.AnchorUsable(parms.pawn, item, parms.facing))
                        {
                            PawnDrawUtility.CalcAnchorData(parms.pawn, item, parms.facing, out anchorOffset, out var _);
                        }
                    }
                }
                // 应用绘制数据的旋转偏移
                Vector3 vector = node.Props.drawData.OffsetForRot(parms.facing);
                // 按角色体型缩放偏移量
                if(node.Props.drawData.scaleOffsetByBodySize && parms.pawn.story != null)
                {
                    Vector2 bodyGraphicScale = parms.pawn.story.bodyType.bodyGraphicScale;
                    float num = (bodyGraphicScale.x + bodyGraphicScale.y) / 2f;
                    vector *= num;
                }
                anchorOffset += vector;
            }
            // 添加调试偏移
            anchorOffset += node.DebugOffset;
            // 添加动画工作类的偏移
            if(node.AnimationWorker != null && node.AnimationWorker.Enabled() && !parms.flags.FlagSet(PawnRenderFlags.Portrait))
            {
                anchorOffset += node.AnimationWorker.OffsetAtTick(node.tree.AnimationTick, parms);
            }
            // 添加痉挛特效偏移
            if(node is Hjx_BiomassNucleus.PawnRenderNode_Spastic pawnRenderNode_Spastic && 
                pawnRenderNode_Spastic.CheckAndDoSpasm(parms, out var dat, out var progress))
            {
                // 根据痉挛进度进行线性插值
                anchorOffset += Vector3.Lerp(dat.offsetStart, dat.offsetTarget, progress);
            }
            return anchorOffset;
        }

        // 计算旋转角度
        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            float num = node.DebugAngleOffset; // 调试角度偏移
            // 添加绘制数据的旋转偏移
            if(node.Props.drawData != null)
            {
                num += node.Props.drawData.RotationOffsetForRot(parms.facing);
            }
            // 添加动画工作类旋转
            if(node.AnimationWorker != null && node.AnimationWorker.Enabled() && !parms.flags.FlagSet(PawnRenderFlags.Portrait))
            {
                num += node.AnimationWorker.AngleAtTick(node.tree.AnimationTick, parms);
            }
            Quaternion quaternion = Quaternion.AngleAxis(num, Vector3.up); // 基础旋转
            // 非痉挛节点直接返回基础旋转
            if(!(node is Hjx_BiomassNucleus.PawnRenderNode_Spastic pawnRenderNode_Spastic))
            {
                return quaternion;
            }
            float num2 = 0f; // 额外旋转角度初始化
            if(node.Props is Hjx_BiomassNucleus.PawnRenderNodeProperties_Spastic pawnRenderNodeProperties_Spastic &&
                pawnRenderNodeProperties_Spastic.rotateFacing)
            {
                num2 += parms.facing.AsAngle; // 角色面向角度
            }
            // 添加痉挛旋转
            if(pawnRenderNode_Spastic.CheckAndDoSpasm(parms, out var dat, out var progress))
            {
                num2 += Mathf.Lerp(dat.rotationStart, dat.rotationTarget, progress);
            }
            // 合并基础旋转和额外旋转
            return quaternion * num2.ToQuat();
        }

        // 计算缩放比例：包含基础缩放、痉挛缩放、体型缩放
        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 vector = base.ScaleFor(node, parms);

            if(node is Hjx_BiomassNucleus.PawnRenderNode_Spastic pawnRenderNode_Spastic && 
                pawnRenderNode_Spastic.CheckAndDoSpasm(parms, out var dat, out var progress))
            {
                vector *= Mathf.Lerp(dat.scaleStart, dat.scaleTarget, progress);
                vector.y = 1f;
            }
            // 按角色体型调整缩放
            float num = ((!BMNUtility.bigsmallActive) ?
                        (float)Math.Sqrt(node.hediff.pawn.BodySize) :
                        (float)Math.Sqrt(node.hediff.pawn.RaceProps.baseBodySize));
            // 最小缩放限制
            if(num < 0.9f)
            {
                num = 0.9f;
            }
            return vector * num;
        }

        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            // 检查旋转模式是否匹配
            if (!node.Props.rotDrawMode.HasFlag(parms.rotDrawMode))
            {
                return false;
            }
            // 检查是否在可见面向方向
            if(node.Props.visibleFacing != null && !node.Props.visibleFacing.Contains(parms.facing))
            {
                return false;
            }
            // 检查是否被跳过标志标记
            if(node.Props.skipFlag != RenderSkipFlagDefOf.None && parms.skipFlags.HasFlag(node.Props.skipFlag))
            {
                return false;
            }
            Rot4 rot = Rot4.Invalid;
            // 处理图形翻转逻辑
            if(rot != Rot4.Invalid && node.Props.flipGraphic && rot.IsHorizontal)
            {
                rot = rot.Opposite;
            }
            // 检查是否与当前面向方向冲突
            if(parms.facing == rot)
            {
                return false;
            }
            // 检查链接的身体部位是否存在
            if (node.Props.linkedBodyPartsGroup != null && 
                !parms.pawn.health.hediffSet.GetNotMissingParts().Any((BodyPartRecord x) =>
                x.groups.NotNullAndContains(node.Props.linkedBodyPartsGroup)))
            {
                return false;
            }
            return node.DebugEnabled;
        }
    }

    [StaticConstructorOnStartup]
    public static class Texture2DUtility
    {
        // 只读字段确保初始化后，不会被修改
        public static readonly Texture2D CourseIcon = ContentFinder<Texture2D>.Get("Hjx_BiomassNucleus/CourseIcon");

        public static readonly Texture2D FoodIcon = ContentFinder<Texture2D>.Get("Hjx_BiomassNucleus/FoodIcon");

        public static readonly Texture2D BornIcon = ContentFinder<Texture2D>.Get("Hjx_BiomassNucleus/BornIcon");
    }

    [DefOf]
    public static class ThingDefOf
    {
        public static ThingDef BMN_Building_BiomassNode;

        public static ThingDef BMN_BiomassNucleus;

        public static ThingDef PawnFlyer_BladeSprint;

        public static ThingDef PawnFlyer_BMN_Longjump;

        static ThingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(Hjx_BiomassNucleus.ThingDefOf));
        }
    }

    public class Verb_CastAbilityBladeSprint : Verb_CastAbilityJump
    {
        public override ThingDef JumpFlyerDef => Hjx_BiomassNucleus.ThingDefOf.PawnFlyer_BladeSprint;

        // 重写高亮绘制方法
        public override void DrawHighlight(LocalTargetInfo target)
        {
            if(verbProps.range > 0f)
            {
                // 调用新临时范围环绘制方法
                verbProps.DrawRadiusRing_NewTemp(caster.Position, this);
            }
            // 当目标可被击中、技能适用且目标有效时
            if(CanHitTarget(target) && IsApplicableTo(target) && target.IsValid)
            {
                GenDraw.DrawTargetHighlightWithLayer(target.CenterVector3, AltitudeLayer.MetaOverlays);
                // 计算从施法者到目标的标准化方向向量
                Vector3 normalized = (target.CenterVector3 - ability.pawn.PositionHeld.ToVector3Shifted()).normalized;
                // 计算冲刺终点位置
                Vector3 vect = target.CenterVector3 + normalized;
                // 将世界坐标转换为网格坐标
                IntVec3 center = vect.ToIntVec3();
                // 绘制半径为1的冲刺影响范围
                BMNUtility.DrawCells(ability.pawn.MapHeld, center, 1, Verb_CastAbility.RadiusHighlightColor);
            }
            // 如果目标有效，绘制技能效果预览
            if (target.IsValid)
            {
                ability.DrawEffectPreviews(target);
            }
        }
    }

    public class Verb_CastAbilityLongjump : Verb_CastAbilityJump
    {
        public override ThingDef JumpFlyerDef => Hjx_BiomassNucleus.ThingDefOf.PawnFlyer_BMN_Longjump;
    }
}
