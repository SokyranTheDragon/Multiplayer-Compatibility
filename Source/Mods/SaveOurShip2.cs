using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat.Mods
{
    /// <summary>Save Our Ship 2 by Kentington and Thain</summary>
    /// <see href="https://steamcommunity.com/workshop/filedetails/?id=1909914131"/>
    [MpCompatFor("kentington.saveourship2")]
    class SaveOurShip2
    {
        // Ship bridge
        private static Type worldObjectOrbitingShipType;
        private static MethodInfo worldObjectOrbitingShipCanLaunchNowMethod;
        private static FieldInfo shieldsListField;
        private static FieldInfo anyShieldsOnField;
        // Rename ship dialog
        private static FieldInfo buildingShipBridgeField;

        private static Type compBecomeBuildingType;
        private static Type compBecomePawnType;

        public SaveOurShip2(ModContentPack mod)
        {
            worldObjectOrbitingShipType = AccessTools.TypeByName("RimWorld.WorldObjectOrbitingShip");

            // Gizmos
            {
                // Rename ship
                var type = AccessTools.TypeByName("SaveOurShip2.Dialog_NameShip");
                buildingShipBridgeField = type.GetField("ship");
                MP.RegisterSyncMethod(type, "SetName");
                MP.RegisterSyncWorker<Dialog_Rename>(SyncShipRename, type);
            }

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            // Gizmos
            {
                // Ship bridge
                // Fly to new/previous world - skipped for now
                var type = AccessTools.TypeByName("RimWorld.Building_ShipBridge");
                worldObjectOrbitingShipCanLaunchNowMethod = AccessTools.PropertyGetter(type, "CanLaunchNow");

                // Sync Building_ShipBridge as a Thing
                MP.RegisterSyncWorker<Thing>(SyncThing, type);

                // Launching the ship
                // We add a prefix to the actual launch and sync our own method, as the real method has a bunch of stuff that shouldn't be synced
                type = AccessTools.TypeByName("RimWorld.Building_ShipBridge");
                MpCompat.harmony.Patch(AccessTools.Method(type, "TryLaunch"), prefix: new HarmonyMethod(typeof(SaveOurShip2), nameof(TryLaunchPrefix)));
                MP.RegisterSyncMethod(typeof(SaveOurShip2), nameof(InitiateCountdown));

                // Move/land ship
                // It deselects everything the user has selected, as it minifies the ship and does some magic with it
                // The deselect interaction should only affect the map with a ship, but we sync it anyway as it's much easier to do :p
                // I'm assuming that moving the ship isn't going to happen very frequently, so occasional inconvenience should be bearable
                MP.RegisterSyncMethod(type, "<GetGizmos>b__7_9");
                MP.RegisterSyncMethod(type, "<GetGizmos>b__7_22");

                // Battle related stuff
                var inner = AccessTools.Inner(type, "<>c");
                MP.RegisterSyncWorker<object>(SyncNothing, inner, shouldConstruct: true);
                MP.RegisterSyncMethod(inner, "<GetGizmos>b__7_12"); // Salvage enemy ship
                MP.RegisterSyncMethod(inner, "<GetGizmos>b__7_13"); // Cancel salvage enemy ship
                MP.RegisterSyncMethod(inner, "<GetGizmos>b__7_15"); // Retreat
                MP.RegisterSyncMethod(inner, "<GetGizmos>b__7_16"); // Stop
                MP.RegisterSyncMethod(inner, "<GetGizmos>b__7_17"); // Advance
                MP.RegisterSyncMethod(inner, "<GetGizmos>b__7_18"); // Escape
                MP.RegisterSyncMethod(type, "<GetGizmos>b__7_22"); // Capture enemy ship
                // Dev mode battle stuff
                MP.RegisterSyncMethod(type, "<GetGizmos>b__7_14"); // Dev start battle
                MP.RegisterSyncMethod(inner, "<GetGizmos>b__7_19"); // Dev enemy retreat
                MP.RegisterSyncMethod(inner, "<GetGizmos>b__7_20"); // Dev enemy stop
                MP.RegisterSyncMethod(inner, "<GetGizmos>b__7_21"); // Dev enemy advance
                // Start battle
                //inner = AccessTools.Inner(type, "<>c__DisplayClass7_3");
                //MP.RegisterSyncMethod(inner, "<GetGizmos>b__23"); // Attempt an act of space piracy by attacking trade ship
                //MP.RegisterSyncMethod(inner, "<GetGizmos>b__24"); // Attempt to engage a ship

                // Toggle all shields
                inner = AccessTools.Inner(type, "<>c__DisplayClass7_1");
                shieldsListField = inner.GetField("shields");
                anyShieldsOnField = inner.GetField("anyShieldOn");
                MP.RegisterSyncWorker<object>(SyncShipBridgeShields, inner, shouldConstruct: true);
                MP.RegisterSyncMethod(inner, "<GetGizmos>b__7");

                // Turning shuttles from pawns to building and vice versa
                compBecomeBuildingType = AccessTools.TypeByName("RimWorld.CompBecomeBuilding");
                MP.RegisterSyncMethod(compBecomeBuildingType, "<CompGetGizmosExtra>b__4_0");
                MP.RegisterSyncWorker<ThingComp>(SyncTransform, compBecomeBuildingType);
                compBecomePawnType = AccessTools.TypeByName("RimWorld.CompBecomePawn");
                MP.RegisterSyncMethod(compBecomePawnType, "<CompGetGizmosExtra>b__5_0");
                MP.RegisterSyncWorker<ThingComp>(SyncTransform, compBecomePawnType);

                //type = AccessTools.TypeByName("SaveOurShip2.SaveShip");
                //MP.RegisterSyncMethod(type, "SaveShipAndRemoveItemStacks");
            }
        }

        private static void SyncNothing(SyncWorker sync, ref object obj)
        { }

        private static void SyncThing(SyncWorker sync, ref Thing thing)
        {
            if (sync.isWriting)
                sync.Write(thing);
            else
                thing = sync.Read<Thing>();
        }

        private static void SyncTransform(SyncWorker sync, ref ThingComp thingComp)
        {
            if (sync.isWriting)
            {
                sync.Write<Thing>(thingComp.parent);
            }
            else
            {
                Thing thing = sync.Read<Thing>();
                if (thing is ThingWithComps thingWithComps)
                    thingComp = thingWithComps.AllComps.Where(x => x.GetType() == compBecomeBuildingType || x.GetType() == compBecomePawnType).FirstOrDefault();
            }
        }

        private static bool TryLaunchPrefix(Building __instance)
        {
            if ((bool)worldObjectOrbitingShipCanLaunchNowMethod.Invoke(__instance, null))
            {
                if (Find.WorldObjects.AllWorldObjects.Any(ob => ob.GetType() == worldObjectOrbitingShipType))
                    return true;

                InitiateCountdown(__instance);
                return false;
            }

            return true;
        }

        private static void InitiateCountdown(Building building)
        {
            ShipCountdown.InitiateCountdown(building);
        }

        private static void SyncShipRename(SyncWorker sync, ref Dialog_Rename dialog)
        {
            if (sync.isWriting)
                sync.Write(buildingShipBridgeField.GetValue(dialog) as Thing);
            else
            {
                Thing thing = sync.Read<Thing>();
                buildingShipBridgeField.SetValue(dialog, thing);
            }
        }

        private static void SyncShipBridgeShields(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
            {
                var anyShieldsOn = (bool)anyShieldsOnField.GetValue(obj);
                var shieldsList = shieldsListField.GetValue(obj) as List<Building>;

                sync.Write(anyShieldsOn);
                sync.Write(shieldsList.Count);
                foreach (var shield in shieldsList)
                    sync.Write<Thing>(shield);
            }
            else
            {
                anyShieldsOnField.SetValue(obj, sync.Read<bool>());

                var items = sync.Read<int>();
                var shieldsList = new List<Building>(items);
                for (int i = 0; i < items; i++)
                    shieldsList.Add(sync.Read<Thing>() as Building);

                shieldsListField.SetValue(obj, shieldsList);
            }
        }
    }
}
