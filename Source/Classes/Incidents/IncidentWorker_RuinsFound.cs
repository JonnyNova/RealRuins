using RimWorld;
using RimWorld.Planet;

using Verse;

using System.Collections.Generic;
using System.Linq;
using System;

namespace RealRuins {
    public class IncidentWorker_RuinsFound : IncidentWorker {

        protected override bool CanFireNowSub(IncidentParms parms) {
            if (!base.CanFireNowSub(parms)) {
                return false;
            }

            if (!SnapshotStoreManager.Instance.CanFireLargeEvent()) {
                return false;
            }

            int tile;
            Faction faction;

            return Find.FactionManager.RandomNonHostileFaction(false, false, false, TechLevel.Undefined) != null && TryFindTile(out tile) && SiteMakerHelper.TryFindRandomFactionFor(DefDatabase<SiteCoreDef>.GetNamed("RuinedBaseSite"), null, out faction, true, null);
        }

        protected override bool TryExecuteWorker(IncidentParms parms) {
            Faction faction = parms.faction;
            if (faction == null) {
                faction = Find.FactionManager.RandomNonHostileFaction(false, false, false, TechLevel.Undefined);
            }
            if (faction == null) {
                return false;
            }
            if (!TryFindTile(out int tile)) {
                return false;
            }

            AbandonedBaseWorldObject site = (AbandonedBaseWorldObject)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("AbandonedBase"));
            site.Tile = tile;
            site.SetFaction(null);
            Find.WorldObjects.Add(site);

            string filename = null;
            Blueprint bp = BlueprintFinder.FindRandomBlueprintWithParameters(out filename, 6400, 0.01f, 30000, maxAttemptsCount: 50);

            RuinedBaseComp comp = site.GetComponent<RuinedBaseComp>();
            if (comp == null) {
                Debug.Warning("Component is null");
            } else {
                Debug.Warning("Starting scavenging...");
                int cost = 10000;
                if (bp != null) {
                    cost = (int)bp.totalCost;
                }
                comp.blueprintFileName = filename;
                comp.StartScavenging(cost);
            }



            var lifetime = (int)(Math.Pow(site.GetComponent<RuinedBaseComp>().currentCapCost / 1000, 0.41) * 1.1);
            string letterText = GetLetterText(faction, lifetime);
            Find.LetterStack.ReceiveLetter(def.letterLabel, letterText, def.letterDef, site, faction, null);
            return true;
        }

        private bool TryFindTile(out int tile) {
            IntRange itemStashQuestSiteDistanceRange = new IntRange(5, 30);
            return TileFinder.TryFindNewSiteTile(out tile, itemStashQuestSiteDistanceRange.min, itemStashQuestSiteDistanceRange.max, false, true, -1);
        }


        private string GetLetterText(Faction alliedFaction, int timeoutDays) {
            string text = string.Format(def.letterText, alliedFaction.leader.LabelShort, alliedFaction.def.leaderTitle, alliedFaction.Name, timeoutDays).CapitalizeFirst();
            return text;
        }
    }
}