﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace RealRuins {
    public class TransportPodsArrivalAction_VisitRuinsPOI : TransportPodsArrivalAction {
        private MapParent site;

        private PawnsArrivalModeDef arrivalMode;

        public TransportPodsArrivalAction_VisitRuinsPOI() {
        }

        public TransportPodsArrivalAction_VisitRuinsPOI(MapParent site, PawnsArrivalModeDef arrivalMode) {
            this.site = site;
            this.arrivalMode = arrivalMode;
        }

        public override void ExposeData() {
            base.ExposeData();
            Scribe_References.Look(ref site, "site");
            Scribe_Defs.Look(ref arrivalMode, "arrivalMode");
        }

        public override FloatMenuAcceptanceReport StillValid(IEnumerable<IThingHolder> pods, int destinationTile) {
            FloatMenuAcceptanceReport floatMenuAcceptanceReport = base.StillValid(pods, destinationTile);
            if (!(bool)floatMenuAcceptanceReport) {
                return floatMenuAcceptanceReport;
            }
            if (site != null && site.Tile != destinationTile) {
                return false;
            }
            return CanVisit(pods, site);
        }

        public override bool ShouldUseLongEvent(List<ActiveDropPodInfo> pods, int tile) {
            return !site.HasMap;
        }

        public override void Arrived(List<ActiveDropPodInfo> pods, int tile) {
            Debug.Log("Overridden arrive pods - visit POI");
            Thing lookTarget = TransportPodsArrivalActionUtility.GetLookTarget(pods);
            bool flag = !site.HasMap;

            string letterText = null;
            string letterCaption = null;
            LetterDef letterDef = LetterDefOf.NeutralEvent;
            if (flag) {
                if (site.Faction == null) {
                    letterCaption = "LetterLabelCaravanEnteredUnownedPOI".Translate();
                    letterText = "LetterTransportPodsArrivedInUnownedPOI".Translate().CapitalizeFirst();
                    letterDef = LetterDefOf.NeutralEvent;
                } else {
                    letterCaption = "LetterLabelTransportPodsAttackedPOI".Translate();
                    letterText = "LetterTransportPodsAttackedPOI".Translate().CapitalizeFirst();
                    letterDef = LetterDefOf.ThreatBig;
                }
            }

            AffectRelationsIfNeeded(ref letterText);
            Find.LetterStack.ReceiveLetter(letterCaption, letterText, letterDef, lookTarget, null, null);


            Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, RuinedBaseWorker.MapSize, null);
            if (flag) {
                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter_Send(orGenerateMap.mapPawns.AllPawns, "LetterRelatedPawnsInMapWherePlayerLanded".Translate(Faction.OfPlayer.def.pawnsPlural), LetterDefOf.NeutralEvent, informEvenIfSeenBefore: true);
            }
            arrivalMode.Worker.TravelingTransportPodsArrived(pods, orGenerateMap);
        }

        public static FloatMenuAcceptanceReport CanVisit(IEnumerable<IThingHolder> pods, MapParent site) {
            if (site == null || !site.Spawned) {
                return false;
            }
            if (!TransportPodsArrivalActionUtility.AnyNonDownedColonist(pods)) {
                return false;
            }
            if (site.EnterCooldownBlocksEntering()) {
                return FloatMenuAcceptanceReport.WithFailMessage("MessageEnterCooldownBlocksEntering".Translate(site.EnterCooldownDaysLeft().ToString("0.#")));
            }
            return true;
        }

        public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(CompLaunchable representative, IEnumerable<IThingHolder> pods, MapParent site) {
            foreach (FloatMenuOption floatMenuOption in TransportPodsArrivalActionUtility.GetFloatMenuOptions(() => CanVisit(pods, site), () => new TransportPodsArrivalAction_VisitRuinsPOI(site, PawnsArrivalModeDefOf.EdgeDrop), "DropAtEdge".Translate(), representative, site.Tile)) {
                yield return floatMenuOption;
            }
            foreach (FloatMenuOption floatMenuOption2 in TransportPodsArrivalActionUtility.GetFloatMenuOptions(() => CanVisit(pods, site), () => new TransportPodsArrivalAction_VisitRuinsPOI(site, PawnsArrivalModeDefOf.CenterDrop), "DropInCenter".Translate(), representative, site.Tile)) {
                yield return floatMenuOption2;
            }
        }


        private void AffectRelationsIfNeeded(ref string letterText) {
            if (site.Faction == null || site.Faction == Faction.OfPlayer) {
                return;
            }

            FactionRelationKind playerRelationKind = site.Faction.PlayerRelationKind;
            if (!site.Faction.HostileTo(Faction.OfPlayer)) {
                site.Faction.TrySetRelationKind(Faction.OfPlayer, FactionRelationKind.Hostile, canSendLetter: false);
            } else if (site.Faction.TryAffectGoodwillWith(Faction.OfPlayer, -50, canSendMessage: false, canSendHostilityLetter: false)) {
                letterText = letterText + "\r\n" + "RelationsWith".Translate(site.Faction.Name) + ": " + (-50).ToStringWithSign();
            }
            site.Faction.TryAppendRelationKindChangedInfo(ref letterText, playerRelationKind, site.Faction.PlayerRelationKind);
        }
    }
}