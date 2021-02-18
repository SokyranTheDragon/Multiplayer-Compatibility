using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    static class FullRandomPatcher
    {
        // The patching was separate in the old version, but it's still in here just in case it'll ever find use again
        public static bool IsSystemRandomPatched { get; private set; }
        public static bool IsUnityRandomPatched { get; private set; }

        public static bool ShouldReplaceSystemRand => true;
        public static bool ShouldReplaceUnityRand => true;

        public static bool ShouldLogSystemRand => true;
        public static bool ShouldLogUnityRand => true;

        #region Sokyran RNG Nuker 9000001
        /// <summary>Sokyran RNG Nuker 9000001 - finds and logs/patches all occurances of System/Unity RNG calls</summary>
        public static void TranspileAll()
        {
            if (IsSystemRandomPatched || IsUnityRandomPatched) return;
            if (!ShouldReplaceSystemRand && !ShouldReplaceUnityRand && !ShouldLogSystemRand && !ShouldLogUnityRand) return;

            var unsupportedTypes = new[]
            {
                nameof(System),
                nameof(Unity),
                nameof(UnityEditor),
                nameof(UnityEngine),
                nameof(UnityEngineInternal),
                nameof(Multiplayer),
                nameof(Microsoft),
                nameof(HarmonyLib),
                nameof(Microsoft),
                nameof(Mono),
                nameof(MonoMod),
                nameof(Ionic),
                nameof(NVorbis),
                nameof(RuntimeAudioClipLoader),
                nameof(JetBrains),
                nameof(AOT),
                nameof(DynDelegate),
                "I18N",
                "LiteNetLib",
                "RestSharp",
                "JetBrains",
                "YamlDotNet",
                "SemVer",
                "GasNetwork",

                // Used by some mods, don't include
                //nameof(RimWorld),
                //nameof(Verse),
            };

            var types = LoadedModManager.RunningMods.SelectMany(x => x.assemblies.loadedAssemblies).SelectMany(x => x.GetTypes());

            Parallel.ForEach(types, t => PatchType(t, unsupportedTypes));

            IsSystemRandomPatched = IsUnityRandomPatched = true;
        }

        public static void PatchType(Type type, string[] unsupportedTypes, List<string> log = null)
        {
            // Don't mind all the try/catch blocks, I went for maximum safety
            try
            {
                if (unsupportedTypes.Where(t => type.Namespace != null && (type.Namespace == t || type.Namespace.StartsWith($"{t}."))).Any()) return;
            }
            catch (Exception)
            { }

            if (log != null) log.Add(type.FullName);

            try
            {
                // Get all methods, constructors, getters, and setters (everything that should have IL instructions)
                var methods = AccessTools.GetDeclaredMethods(type).Cast<MethodBase>()
                    .Concat(AccessTools.GetDeclaredConstructors(type))
                    .Concat(AccessTools.GetDeclaredProperties(type).SelectMany(p => new[] { p.GetGetMethod(true), p.GetSetMethod(true) }).Where(p => p != null));

                foreach (var method in methods)
                {
                    try
                    {
                        MpCompat.harmony.Patch(method, transpiler: new HarmonyMethod(typeof(PatchingUtilities), nameof(PatchingUtilities.FullRNGPatcher)));
                        if (!method.IsConstructor || !method.IsStatic) 
                            Log.Warning($"Unpatched RNG method: {type.FullName}:{method.Name}", true);
                    }
                    catch (PatchingUtilities.PatchingCancelledException)
                    { }
                    catch (Exception)
                    { }
                }
            }
            catch (Exception)
            { }

            try
            {
                var fields = AccessTools.GetDeclaredFields(type).Where(f => f.IsStatic && f.FieldType == typeof(System.Random));

                foreach (var field in fields)
                {
                    try
                    {
                        if (!(field.GetValue(null) is PatchingUtilities.RandRedirector))
                        {
                            field.SetValue(null, new PatchingUtilities.RandRedirector());
                            Log.Warning($"Potentially unpatched static RNG field: {type.FullName}:{field.Name}", true);
                        }
                    }
                    catch (Exception)
                    { }
                }
            }
            catch (Exception)
            { }
        }
        #endregion

        #region Sokyran RNG Nuker 9000000
        public static bool ShouldLog()
        {
            if (Client.Multiplayer.Client == null) return false;
            if (!Client.Multiplayer.game?.worldComp?.logDesyncTraces ?? false) return false; // only log if debugging enabled in Host Server menu
            if (Client.TickPatch.Skipping || Client.Multiplayer.IsReplay) return false;

            if (!Client.Multiplayer.Ticking && !Client.Multiplayer.ExecutingCmds) return false;

            ShouldReplace();

            return false;
        }

        public static bool ShouldLogStripped()
        {
            if (Client.Multiplayer.Client == null) return false;
            if (!Client.Multiplayer.game?.worldComp?.logDesyncTraces ?? false) return false; // only log if debugging enabled in Host Server menu
            if (Client.TickPatch.Skipping || Client.Multiplayer.IsReplay) return false;

            if (!Client.Multiplayer.Ticking && !Client.Multiplayer.ExecutingCmds) return false;

            return true;
        }

        public static bool ShouldReplace()
        {
            if (!Client.WildAnimalSpawnerTickMarker.ticking &&
                !Client.WildPlantSpawnerTickMarker.ticking &&
                !Client.SteadyEnvironmentEffectsTickMarker.ticking &&
                !Client.FindBestStorageCellMarker.executing &&
                Client.ThingContext.Current?.def != ThingDefOf.SteamGeyser)
            {
                return true;
            }

            return false;
        }

        public static void PatchSystemRandom()
        {
            if (IsSystemRandomPatched || !MP.enabled) return;
            if (!ShouldReplaceSystemRand && !ShouldLogSystemRand) return;

            Log.Warning("System.Random is being patched by Multiplayer Compatibility - this could cause some issues, use at own risk");

            var type = typeof(System.Random);
            var patcherType = typeof(FullRandomPatcher);

            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(System.Random.Next), Array.Empty<Type>()), new HarmonyMethod(patcherType, nameof(Next)));
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(System.Random.Next), new[] { typeof(int) }), new HarmonyMethod(patcherType, nameof(NextMax)));
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(System.Random.Next), new[] { typeof(int), typeof(int) }), new HarmonyMethod(patcherType, nameof(NextMinMax)));
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(System.Random.NextBytes), new[] { typeof(byte[]) }), new HarmonyMethod(patcherType, nameof(NextBytes)));
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(System.Random.NextDouble), Array.Empty<Type>()), new HarmonyMethod(patcherType, nameof(NextDouble)));

            IsSystemRandomPatched = true;
        }

        public static void PatchUnityRandom()
        {
            // Old version, unused
            if (IsUnityRandomPatched || !MP.enabled) return;
            if (!ShouldReplaceUnityRand && !ShouldLogUnityRand) return;

            Log.Warning("UnityEngine.Random is being patched by Multiplayer Compatibility - this could cause some issues, use at own risk");

            var type = typeof(UnityEngine.Random);
            var patcherType = typeof(FullRandomPatcher);

            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(UnityEngine.Random.Range), new[] { typeof(int), typeof(int) }), new HarmonyMethod(patcherType, nameof(RandomRangeInt)));
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(UnityEngine.Random.Range), new[] { typeof(int), typeof(int) }), new HarmonyMethod(patcherType, nameof(RandomRangeInt)));
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(UnityEngine.Random.RandomRange), new[] { typeof(float), typeof(float) }), new HarmonyMethod(patcherType, nameof(RandomRangeFloat)));
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(UnityEngine.Random.RandomRange), new[] { typeof(float), typeof(float) }), new HarmonyMethod(patcherType, nameof(RandomRangeFloat)));
            //try
            //{
            //    MpCompat.harmony.Patch(AccessTools.PropertyGetter(type, "value"), new HarmonyMethod(patcherType, nameof(RandomValue))); // Can't patch, external method, must transpile
            //}
            //catch (Exception)
            //{ }
            MpCompat.harmony.Patch(AccessTools.PropertyGetter(type, nameof(UnityEngine.Random.insideUnitCircle)), new HarmonyMethod(patcherType, nameof(InsideUnitCircle)));

            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(UnityEngine.Random.ColorHSV), Array.Empty<Type>()), new HarmonyMethod(patcherType, nameof(UnityLogOnly)));
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(UnityEngine.Random.ColorHSV), new[] { typeof(float), typeof(float) }), new HarmonyMethod(patcherType, nameof(UnityLogOnly)));
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(UnityEngine.Random.ColorHSV), new[] { typeof(float), typeof(float), typeof(float), typeof(float) }), new HarmonyMethod(patcherType, nameof(UnityLogOnly)));
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(UnityEngine.Random.ColorHSV), new[] { typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float) }), new HarmonyMethod(patcherType, nameof(UnityLogOnly)));
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(UnityEngine.Random.ColorHSV), new[] { typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float) }), new HarmonyMethod(patcherType, nameof(UnityLogOnly)));

            MpCompat.harmony.Patch(AccessTools.PropertyGetter(type, nameof(UnityEngine.Random.rotation)), new HarmonyMethod(patcherType, nameof(UnityLogOnly)));
            MpCompat.harmony.Patch(AccessTools.PropertyGetter(type, nameof(UnityEngine.Random.rotationUniform)), new HarmonyMethod(patcherType, nameof(UnityLogOnly)));
            MpCompat.harmony.Patch(AccessTools.PropertyGetter(type, nameof(UnityEngine.Random.onUnitSphere)), new HarmonyMethod(patcherType, nameof(UnityLogOnly)));
            MpCompat.harmony.Patch(AccessTools.PropertyGetter(type, nameof(UnityEngine.Random.insideUnitSphere)), new HarmonyMethod(patcherType, nameof(UnityLogOnly)));

            IsUnityRandomPatched = true;
        }

        private static bool StackTraceLogAndCheckIfSystem(bool shouldLog)
        {
            var stack = new System.Diagnostics.StackTrace(1, true);

            if (stack.FrameCount <= 2)
            {
                if (shouldLog) Log.Warning($"Unpatched (and broken?) RNG call{Environment.NewLine}{stack}");
                return false;
            }

            var name = stack.GetFrame(2).GetType().Namespace;

            if (name.StartsWith("System.") || name == "System") return true;

            if (shouldLog) Log.Warning($"Unpatched RNG call{Environment.NewLine}{stack}");
            return false;
        }

        #region System.Random
        private static bool Next(ref int __result)
        {
            if (!MP.IsInMultiplayer) return true;

            // TODO: move after ShouldReplace in release builds
            if (StackTraceLogAndCheckIfSystem(ShouldLogSystemRand)) return true;
            if (!ShouldReplace()) return true;

            if (ShouldReplaceSystemRand)
                __result = Rand.Range(0, int.MaxValue);
            return false;
        }

        private static bool NextMax(int maxValue, ref int __result)
        {
            if (!MP.IsInMultiplayer) return true;

            // TODO: move after ShouldReplace in release builds
            if (StackTraceLogAndCheckIfSystem(ShouldLogSystemRand)) return true;
            if (!ShouldReplace()) return true;

            if (ShouldReplaceSystemRand)
                __result = Rand.Range(0, maxValue);
            return false;
        }

        private static bool NextMinMax(int minValue, int maxValue, ref int __result)
        {
            if (!MP.IsInMultiplayer) return true;

            // TODO: move after ShouldReplace in release builds
            if (StackTraceLogAndCheckIfSystem(ShouldLogSystemRand)) return true;
            if (!ShouldReplace()) return true;

            if (ShouldReplaceSystemRand)
                __result = Rand.Range(minValue, maxValue);
            return false;
        }

        private static bool NextBytes(byte[] buffer)
        {
            if (!MP.IsInMultiplayer) return true;

            // TODO: move after ShouldReplace in release builds
            if (StackTraceLogAndCheckIfSystem(ShouldLogSystemRand)) return true;
            if (!ShouldReplace()) return true;

            if (ShouldReplaceSystemRand)
            {
                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = (byte)Rand.RangeInclusive(0, 255);
            }
            return false;
        }

        private static bool NextDouble(ref double __result)
        {
            if (!MP.IsInMultiplayer) return true;

            // TODO: move after ShouldReplace in release builds
            if (StackTraceLogAndCheckIfSystem(ShouldLogSystemRand)) return true;
            if (!ShouldReplace()) return true;

            if (ShouldReplaceSystemRand)
                __result = Rand.Range(0f, 1f);
            return false;
        }
        #endregion

        #region UnityEngine.Random
        private static bool RandomRangeInt(int min, int max, ref int __result)
        {
            if (!MP.IsInMultiplayer) return true;

            // TODO: move after ShouldReplace in release builds
            if (StackTraceLogAndCheckIfSystem(ShouldLogUnityRand)) return true;
            if (!ShouldReplace()) return true;

            if (ShouldReplaceUnityRand)
                __result = Rand.Range(min, max);
            return false;
        }

        private static bool RandomRangeFloat(float min, float max, ref float __result)
        {
            if (!MP.IsInMultiplayer) return true;

            // TODO: move after ShouldReplace in release builds
            if (StackTraceLogAndCheckIfSystem(ShouldLogUnityRand)) return true;
            if (!ShouldReplace()) return true;

            if (ShouldReplaceUnityRand)
                __result = Rand.Range(min, max);
            return false;
        }

        private static bool RandomValue(ref float __result)
        {
            if (!MP.IsInMultiplayer) return true;

            // TODO: move after ShouldReplace in release builds
            if (StackTraceLogAndCheckIfSystem(ShouldLogUnityRand)) return true;
            if (!ShouldReplace()) return true;

            if (ShouldReplaceUnityRand)
                __result = Rand.Value;
            return false;
        }

        private static bool InsideUnitCircle(ref Vector2 __result)
        {
            if (!MP.IsInMultiplayer) return true;

            // TODO: move after ShouldReplace in release builds
            if (StackTraceLogAndCheckIfSystem(ShouldLogUnityRand)) return true;
            if (!ShouldReplace()) return true;

            if (ShouldReplaceUnityRand)
                __result = Rand.InsideUnitCircle;
            return false;
        }

        private static void UnityLogOnly()
        {
            if (MP.IsInMultiplayer) StackTraceLogAndCheckIfSystem(ShouldLogUnityRand);
            //if (MP.IsInMultiplayer && ShouldLogUnityRand && ShouldLog())
            //    Client.Multiplayer.game.sync.TryAddStackTraceForDesyncLog("Unity RNG");
        }
        #endregion
        #endregion
    }
}
