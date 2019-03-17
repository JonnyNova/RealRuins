﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

using System.Reflection;

using RimWorld;
using RimWorld.Planet;
using Verse.AI;
using Verse.AI.Group;
using Verse;
using RimWorld.BaseGen;

namespace RealRuins
{

    class GenStep_ScatterRealRuins : GenStep {
        public override int SeedPart {
            get {
                return 74293945;
            }
        }

        private ScatterOptions currentOptions = RealRuins_ModSettings.defaultScatterOptions;

        public float CalculateDistanceToNearestSettlement(Map map)
        {
            int rootTile = map.Tile;
            int proximityLimit = 16;
            int minDistance = proximityLimit;

            foreach (WorldObject wo in Find.World.worldObjects.ObjectsAt(map.Tile)) {
                if (wo.Faction != Faction.OfPlayer && (wo is Settlement || wo is Site)) return 1.0f; //some default proximity index for bases and sites. not too much, but not flat area.
            }
            
            Find.WorldFloodFiller.FloodFill(rootTile, (int x) => !Find.World.Impassable(x), delegate(int tile, int traversalDistance)
            {
                if (traversalDistance > proximityLimit)
                {
                    return true;
                }

                //TODO: Check how traversing is done. If it's some kind of BFS, probably I should stop at the first settlement reached.
                if (traversalDistance > 0) {
                    foreach (WorldObject wo in Find.World.worldObjects.ObjectsAt(tile)) {
                        if (wo.Faction != Faction.OfPlayer) {
                            if (wo is Settlement) {
                                if (traversalDistance < minDistance) minDistance = traversalDistance;
                            } else if (wo is Site) {
                                if (traversalDistance * 2 < minDistance) minDistance = traversalDistance * 2; //site has twice less influence
                            }
                        }
                    }
                }

                return false;
            }, 2147483647, null);

            return minDistance;
        }


        public override void Generate(Map map, GenStepParams parms) {
            //skip generation due to low blueprints count
            if (SnapshotStoreManager.Instance.StoredSnapshotsCount() < 10) {
                Debug.Message("Skipping ruins gerenation due to low blueprints count.");
                return;
            }

            bool shouldReturn = false;
            foreach (WorldObject wo in Find.World.worldObjects.ObjectsAt(map.Tile))
            {
                if (!(wo is Site site)) continue;
                shouldReturn = true;
            }
            
            if (shouldReturn) return;
            
            if (!map.TileInfo.WaterCovered) {

                float densityMultiplier = 1.0f;
                float scaleMultiplier = 1.0f;
                float distanceToSettlement = 0.0f;
                float totalDensity = RealRuins_ModSettings.defaultScatterOptions.densityMultiplier;
                currentOptions = RealRuins_ModSettings.defaultScatterOptions.Copy(); //store as instance variable to keep accessible on subsequent ScatterAt calls

                if (RealRuins_ModSettings.defaultScatterOptions.enableProximity) {
                        
                    distanceToSettlement = CalculateDistanceToNearestSettlement(map);
                    if (distanceToSettlement >= 16 && Rand.Chance(0.5f)) {
                        totalDensity = 0;
                    }
                    
                    if (totalDensity > 0) {
                        densityMultiplier = (float)(Math.Exp(1.0 / (distanceToSettlement / 5.0 + 0.3)) - 0.5);
                        scaleMultiplier = (float)(Math.Exp(1 / (distanceToSettlement / 5 + 0.5)) - 0.3);
                    } else {
                        densityMultiplier = 0.0f;
                    }

                    currentOptions.densityMultiplier *= densityMultiplier;
                    currentOptions.minRadius = Math.Min(60, Math.Max(6, (int)(currentOptions.minRadius * scaleMultiplier))); //keep between 6 and 60
                    currentOptions.maxRadius = Math.Min(60, Math.Max(6, (int)(currentOptions.maxRadius * scaleMultiplier))); //keep between 6 and 60
                    currentOptions.scavengingMultiplier *= scaleMultiplier * densityMultiplier;
                    currentOptions.deteriorationMultiplier += Math.Min(0.2f, (1.0f / (scaleMultiplier * densityMultiplier * 3)));


                    if (densityMultiplier > 20.0f) densityMultiplier = 20.0f;
                    while (densityMultiplier * currentOptions.maxRadius > 800) {
                        densityMultiplier *= 0.9f; //WHAT? Why not 800/radius?
                    }

                }

                //number of ruins based on density settings
                var num = (int)((float)map.Area / 10000.0f) * Rand.Range(1 * totalDensity, 2 * totalDensity);

                Debug.Message("dist {0}, dens {1} (x{2}), scale x{3} ({4}-{5}), scav {6}, deter {7}", distanceToSettlement, currentOptions.densityMultiplier, densityMultiplier, scaleMultiplier, currentOptions.minRadius, currentOptions.maxRadius, currentOptions.scavengingMultiplier, currentOptions.deteriorationMultiplier);
                Debug.Message("Spawning {0} ruin chunks", num);
                BaseGen.globalSettings.map = map;

                bool shouldUnpause = false;
                Find.TickManager.Pause();
                if (!Find.TickManager.Paused) {
                    Find.TickManager.TogglePaused();
                    shouldUnpause = true;
                }



                for (int i = 0; i < num; i++) {
                    //We use copy of scatteroptions because each scatteroptions represents separate chunk with separate location, size, maps, etc.
                    //should use struct instead? is it compatible with IExposable?
                    ResolveParams rp = default(ResolveParams);
                    rp.SetCustom<ScatterOptions>(Constants.ScatterOptions, currentOptions.Copy());
                    rp.faction = Find.FactionManager.OfAncientsHostile;
                    var center = CellFinder.RandomNotEdgeCell(10, map);
                    rp.rect = new CellRect(center.x, center.z, 1, 1); //after generation will be extended to a real size
                    Debug.Message("rect before {0}", rp.rect);
                    BaseGen.symbolStack.Push("scatterRuins", rp);
                    //Debug.Message("rect after {0}", rp.rect);
                    //BaseGen.symbolStack.Push("scatterRuinsMobs", rp);
                }
                BaseGen.Generate();

                if (shouldUnpause) {
                    Debug.Message("Finished spawning, unpausing");
                    Find.TickManager.TogglePaused();
                }
            }
        }


    }

    class GenStep_ScatterLargeRealRuins : GenStep {

        private ScatterOptions currentOptions;
        
        public override int SeedPart {
            get {
                return 74293947;
            }
        }


        public override void Generate(Map map, GenStepParams parms) {
            Find.TickManager.Pause();
            Debug.Message("Overridden LARGE generate");
            if (!map.TileInfo.WaterCovered) {

                string filename = map.Parent.GetComponent<RuinedBaseComp>()?.blueprintFileName;
                Debug.Message("Preselected file name is {0}", filename);

                currentOptions = RealRuins_ModSettings.defaultScatterOptions.Copy(); //store as instance variable to keep accessible on subsequent ScatterAt calls

                currentOptions.minRadius = 200;
                currentOptions.maxRadius = 200;
                currentOptions.scavengingMultiplier = 0.1f;
                currentOptions.deteriorationMultiplier = 0.0f;
                currentOptions.hostileChance = 1.0f;

                currentOptions.blueprintFileName = filename;
                currentOptions.minimumCostRequired = 5000;
                currentOptions.minimumDensityRequired = 0.15f;
                currentOptions.minimumAreaRequired = 6400;
                currentOptions.deleteLowQuality = false; //do not delete since we have much higher requirements for base ruins
                currentOptions.shouldKeepDefencesAndPower = true;
                currentOptions.shouldLoadPartOnly = false;
                currentOptions.shouldAddRaidTriggers = true;
                currentOptions.claimableBlocks = false;


                ResolveParams resolveParams = default(ResolveParams);
                BaseGen.globalSettings.map = map;
                resolveParams.SetCustom<ScatterOptions>(Constants.ScatterOptions, currentOptions);
                resolveParams.faction = Find.FactionManager.OfAncientsHostile;
                resolveParams.rect = new CellRect(0, 0, map.Size.x, map.Size.z);
                BaseGen.symbolStack.Push("scatterRuins", resolveParams);
                BaseGen.symbolStack.Push("scatterRaidTriggers", resolveParams);

                BaseGen.globalSettings.mainRect = resolveParams.rect;

                float uncoveredCost = currentOptions.uncoveredCost;
                if (uncoveredCost < 0) {
                    if (Rand.Chance(0.5f)) {
                        uncoveredCost = -uncoveredCost; //adding really small party
                    }
                }

                
                //adding starting party
                //don't doing it via basegen because of uh oh i don't remember, something with pawn location control

                if (uncoveredCost > 0) {
                    float pointsCost = uncoveredCost / 10.0f;
                    FloatRange defaultPoints = new FloatRange(pointsCost * 0.2f,
                        Math.Min(10000.0f, pointsCost * 2.0f));
                    Debug.Message("Adding starting party. Remaining points: {0}. Used points range: {1}",
                        currentOptions.uncoveredCost, defaultPoints);

                    if (currentOptions.allowFriendlyRaids) {
                        if (Rand.Chance(0.1f)) {
                            resolveParams.faction = Find.FactionManager.RandomNonHostileFaction();
                        } else {
                            resolveParams.faction = Find.FactionManager.RandomEnemyFaction();
                        }
                    } else {
                        resolveParams.faction = Find.FactionManager.RandomEnemyFaction();
                    }


                    PawnGroupMakerParms pawnGroupMakerParms = new PawnGroupMakerParms();
                    pawnGroupMakerParms.groupKind = PawnGroupKindDefOf.Combat;
                    pawnGroupMakerParms.tile = map.Tile;
                    pawnGroupMakerParms.points = pointsCost;
                    pawnGroupMakerParms.faction = resolveParams.faction;
                    pawnGroupMakerParms.generateFightersOnly = true;
                    pawnGroupMakerParms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
                    pawnGroupMakerParms.forceOneIncap = false;
                    pawnGroupMakerParms.seed = Rand.Int;

                    List<Pawn> pawns = PawnGroupMakerUtility.GeneratePawns(pawnGroupMakerParms).ToList();
                    CellRect rect = resolveParams.rect;

                    Debug.Message("Rect: {0}, {1} - {2}, {3}", rect.BottomLeft.x, rect.BottomLeft.z, rect.TopRight.x, rect.TopRight.z);
                    Debug.Message("corner: {0}, {1} size: {2}, {3}", currentOptions.bottomLeft.x, currentOptions.bottomLeft.z, currentOptions.roomMap.GetLength(0), currentOptions.roomMap.GetLength(1));

                    CellRect spawnRect = new CellRect(10, 10, map.Size.x - 20, map.Size.y - 20);
                    //CellRect mapRect = new CellRect(currentOptions.topLeft.x, currentOptions.topLeft.z, currentOptions.)

                    foreach (Pawn p in pawns) {

                        IntVec3 location = CellFinder.RandomNotEdgeCell(10, map);
                        bool result = CellFinder.TryFindRandomSpawnCellForPawnNear(location, map, out location);
                        
                        if ( result ) { 
                            GenSpawn.Spawn(p, location, map, Rot4.Random);
                            Debug.Message("Spawned at {0}, {1}", p.Position.x, p.Position.z);
                        } else {
                            Debug.Message("Failed to find a new position");
                        }
                    }

                    LordJob lordJob = new LordJob_AssaultColony(resolveParams.faction, canKidnap: false, canTimeoutOrFlee: Rand.Chance(0.5f));
                    if (lordJob != null) {
                        LordMaker.MakeNewLord(resolveParams.faction, lordJob, map, pawns);
                    }

                }

                BaseGen.symbolStack.Push("chargeBatteries", resolveParams);
                BaseGen.symbolStack.Push("refuel", resolveParams);

                BaseGen.Generate();

            }
        }

    }
    
    class GenStep_ScatterMediumRealRuins : GenStep {

        private ScatterOptions currentOptions;
        
        public override int SeedPart {
            get {
                return 74293948;
            }
        }


        public override void Generate(Map map, GenStepParams parms) {
            if (!map.TileInfo.WaterCovered) {
                Find.TickManager.Pause();

                currentOptions = RealRuins_ModSettings.defaultScatterOptions.Copy(); //store as instance variable to keep accessible on subsequent ScatterAt calls

                currentOptions.minRadius = 24;
                currentOptions.maxRadius = 50;   
                currentOptions.scavengingMultiplier = 0.5f;
                currentOptions.deteriorationMultiplier = 0.1f;
                currentOptions.hostileChance = 0.8f;
                currentOptions.itemCostLimit = 800;

                currentOptions.minimumCostRequired = 5000;
                currentOptions.minimumDensityRequired = 0.2f;
                currentOptions.minimumAreaRequired = 1000;
                currentOptions.deleteLowQuality = false; //do not delete since we have much higher requirements for base ruins
                currentOptions.shouldKeepDefencesAndPower = true;

                ResolveParams rp = default(ResolveParams);
                BaseGen.globalSettings.map = map;
                rp.SetCustom<ScatterOptions>(Constants.ScatterOptions, currentOptions);
                rp.faction = Find.FactionManager.OfAncientsHostile;
                BaseGen.symbolStack.Push("scatterRuins", rp);


                ResolveParams resolveParams = default(ResolveParams);
                resolveParams.rect = CellRect.CenteredOn(map.Center, currentOptions.minRadius + (currentOptions.maxRadius - currentOptions.maxRadius) / 2);
                BaseGen.globalSettings.map = map;
                BaseGen.globalSettings.mainRect = resolveParams.rect;

                if (Rand.Chance(0.2f)) {
                    float pointsCost = Math.Abs(Rand.Gaussian()) * 500; 

                    resolveParams.faction = Find.FactionManager.RandomEnemyFaction();
                    resolveParams.singlePawnLord = LordMaker.MakeNewLord(resolveParams.faction,
                        new LordJob_AssaultColony(resolveParams.faction, false, false, true, true), map, null);

                    resolveParams.pawnGroupKindDef = (resolveParams.pawnGroupKindDef ?? PawnGroupKindDefOf.Settlement);

                    if (resolveParams.pawnGroupMakerParams == null) {
                        resolveParams.pawnGroupMakerParams = new PawnGroupMakerParms();
                        resolveParams.pawnGroupMakerParams.tile = map.Tile;
                        resolveParams.pawnGroupMakerParams.faction = resolveParams.faction;
                        PawnGroupMakerParms pawnGroupMakerParams = resolveParams.pawnGroupMakerParams;
                        pawnGroupMakerParams.points = pointsCost;
                    }

                    BaseGen.symbolStack.Push("pawnGroup", resolveParams);

                }

                BaseGen.symbolStack.Push("chargeBatteries", resolveParams);
                BaseGen.symbolStack.Push("refuel", resolveParams);
                    
                BaseGen.Generate();
            }
        }

    }
    
}
