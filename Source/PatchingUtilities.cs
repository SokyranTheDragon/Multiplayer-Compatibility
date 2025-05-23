﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    public static class PatchingUtilities
    {
        #region RNG Patching

        static void FixRNGPre() => Rand.PushState();
        static void FixRNGPos() => Rand.PopState();

        /// <summary>Patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction},MethodBase)"/>, and optionally surrounds the method with <see cref="Rand.PushState()"/> and <see cref="Rand.PopState()"/>.</summary>
        /// <param name="methods">Methods that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        public static void PatchSystemRand(IEnumerable<string> methods, bool patchPushPop = true)
        {
            foreach (var method in methods)
                PatchSystemRand(method, patchPushPop);
        }

        /// <summary>Patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction},MethodBase)"/>, and optionally surrounds the method with <see cref="Rand.PushState()"/> and <see cref="Rand.PopState()"/>.</summary>
        /// <param name="methods">Methods that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        public static void PatchSystemRand(IEnumerable<MethodBase> methods, bool patchPushPop = true)
        {
            foreach (var method in methods)
                PatchSystemRand(method, patchPushPop);
        }

        /// <summary>Patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction},MethodBase)"/>, and optionally surrounds the method with <see cref="Rand.PushState()"/> and <see cref="Rand.PopState()"/>.</summary>
        /// <param name="method">Methods that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        public static void PatchSystemRand(string method, bool patchPushPop = true)
        {
            var m = AccessTools.DeclaredMethod(method) ?? AccessTools.Method(method);
            if (m == null)
                Log.Error($"Trying to patch System.Random for method {method}, but the method does not exist.");
            else
                PatchSystemRand(m, patchPushPop);
        }

        /// <summary>Patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction},MethodBase)"/>, and optionally surrounds the method with <see cref="Rand.PushState()"/> and <see cref="Rand.PopState()"/>.</summary>
        /// <param name="method">Method that needs patching</param>
        /// <param name="patchPushPop">Determines if the method should be surrounded with push/pop calls</param>
        public static void PatchSystemRand(MethodBase method, bool patchPushPop = true)
        {
            if (method == null)
            {
                Log.Error("Trying to patch System.Random for null method.");
                return;
            }

            var transpiler = new HarmonyMethod(typeof(PatchingUtilities), nameof(FixRNG));

            if (patchPushPop)
                PatchPushPopRand(method, transpiler);
            else
                MpCompat.harmony.Patch(method, transpiler: transpiler);
        }

        /// <summary>Patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction},MethodBase)"/>, and optionally surrounds the method with <see cref="Rand.PushState()"/> and <see cref="Rand.PopState()"/>.</summary>
        /// <param name="typeName">Type with a parameterless constructor that needs patching</param>
        /// <param name="patchPushPop">Determines if the method should be surrounded with push/pop calls</param>
        public static void PatchSystemRandCtor(string typeName, bool patchPushPop = true)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                Log.Error($"Trying to patch System.Random in constructor of type {typeName}, but the type does not exist.");
                return;
            }

            var ctor = AccessTools.DeclaredConstructor(type) ?? AccessTools.Constructor(type);
            if (ctor == null)
                Log.Error($"Trying to patch System.Random in constructor of type {typeName}, but the constructor could not be found.");
            else
                PatchSystemRand(ctor, patchPushPop);
        }

        /// <summary>Patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction},MethodBase)"/>, and optionally surrounds the method with <see cref="Rand.PushState()"/> and <see cref="Rand.PopState()"/>.</summary>
        /// <param name="typeNames">Type with a parameterless constructors that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        public static void PatchSystemRandCtor(IEnumerable<string> typeNames, bool patchPushPop = true)
        {
            foreach (var typeName in typeNames)
            {
                var type = AccessTools.TypeByName(typeName);
                PatchSystemRand(AccessTools.DeclaredConstructor(type) ?? AccessTools.Constructor(type), patchPushPop);
            }
        }

        /// <summary>Surrounds method with <see cref="Rand.PushState()"/> and <see cref="Rand.PopState()"/>, as well as applies the transpiler (if provided).</summary>
        /// <param name="methods">Methods that needs patching (as string)</param>
        /// <param name="transpiler">Transpiler that will be applied to the method</param>
        public static void PatchPushPopRand(IEnumerable<string> methods, HarmonyMethod transpiler = null)
        {
            foreach (var method in methods)
                PatchPushPopRand(method, transpiler);
        }

        /// <summary>Surrounds method with <see cref="Rand.PushState()"/> and <see cref="Rand.PopState()"/>, as well as applies the transpiler (if provided).</summary>
        /// <param name="methods">Method that needs patching</param>
        /// <param name="transpiler">Transpiler that will be applied to the method</param>
        public static void PatchPushPopRand(IEnumerable<MethodBase> methods, HarmonyMethod transpiler = null)
        {
            foreach (var method in methods)
                PatchPushPopRand(method, transpiler);
        }

        /// <summary>Surrounds method with <see cref="Rand.PushState()"/> and <see cref="Rand.PopState()"/>, as well as applies the transpiler (if provided).</summary>
        /// <param name="method">Method that needs patching</param>
        /// <param name="transpiler">Transpiler that will be applied to the method</param>
        public static void PatchPushPopRand(string method, HarmonyMethod transpiler = null)
        {
            var m = AccessTools.DeclaredMethod(method) ?? AccessTools.Method(method);
            if (m == null)
                Log.Error($"Trying to patch Verse.Rand.Push/Pop for method {method}, but the method does not exist..");
            else
                PatchPushPopRand(m, transpiler);
        }

        /// <summary>Surrounds method with <see cref="Rand.PushState()"/> and <see cref="Rand.PopState()"/>, as well as applies the transpiler (if provided).</summary>
        /// <param name="method">Method that needs patching</param>
        /// <param name="transpiler">Transpiler that will be applied to the method</param>
        public static void PatchPushPopRand(MethodBase method, HarmonyMethod transpiler = null)
        {
            if (method == null)
                Log.Error("Trying to patch Verse.Rand.Push/Pop for null method.");
            else
                MpCompat.harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(PatchingUtilities), nameof(FixRNGPre)),
                    postfix: new HarmonyMethod(typeof(PatchingUtilities), nameof(FixRNGPos)),
                    transpiler: transpiler
                );
        }

        /// <summary>Patches out <see cref="UnityEngine.Random"/> calls using <see cref="FixUnityRNG(IEnumerable{CodeInstruction},MethodBase)"/>, and optionally surrounds the method with <see cref="Rand.PushState()"/> and <see cref="Rand.PopState()"/>.</summary>
        /// <param name="methods">Methods that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        public static void PatchUnityRand(IEnumerable<string> methods, bool patchPushPop = true)
        {
            foreach (var method in methods)
                PatchUnityRand(method, patchPushPop);
        }

        /// <summary>Patches out <see cref="UnityEngine.Random"/> calls using <see cref="FixUnityRNG(IEnumerable{CodeInstruction},MethodBase)"/>, and optionally surrounds the method with <see cref="Rand.PushState()"/> and <see cref="Rand.PopState()"/>.</summary>
        /// <param name="methods">Methods that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        public static void PatchUnityRand(IEnumerable<MethodBase> methods, bool patchPushPop = true)
        {
            foreach (var method in methods)
                PatchUnityRand(method, patchPushPop);
        }

        /// <summary>Patches out <see cref="UnityEngine.Random"/> calls using <see cref="FixUnityRNG(IEnumerable{CodeInstruction},MethodBase)"/>, and optionally surrounds the method with <see cref="Rand.PushState()"/> and <see cref="Rand.PopState()"/>.</summary>
        /// <param name="method">Methods that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        public static void PatchUnityRand(string method, bool patchPushPop = true)
        {
            var m = AccessTools.DeclaredMethod(method) ?? AccessTools.Method(method);
            if (m == null)
                Log.Error($"Trying to patch UnityEngine.Random for method {method}, but the method does not exist.");
            else
                PatchUnityRand(m, patchPushPop);
        }

        /// <summary>Patches out <see cref="UnityEngine.Random"/> calls using <see cref="FixUnityRNG(IEnumerable{CodeInstruction},MethodBase)"/>, and optionally surrounds the method with <see cref="Rand.PushState()"/> and <see cref="Rand.PopState()"/>.</summary>
        /// <param name="method">Method that needs patching</param>
        /// <param name="patchPushPop">Determines if the method should be surrounded with push/pop calls</param>
        public static void PatchUnityRand(MethodBase method, bool patchPushPop = true)
        {
            if (method == null)
            {
                Log.Error("Trying to patch UnityEngine.Random for null method.");
                return;
            }

            var transpiler = new HarmonyMethod(typeof(PatchingUtilities), nameof(FixUnityRNG));

            if (patchPushPop)
                PatchPushPopRand(method, transpiler);
            else
                MpCompat.harmony.Patch(method, transpiler: transpiler);
        }

        #endregion

        #region System RNG transpiler

        private static readonly ConstructorInfo SystemRandConstructor = typeof(Random).GetConstructor(Type.EmptyTypes);
        private static readonly ConstructorInfo SystemRandSeededConstructor = typeof(Random).GetConstructor([typeof(int)]);
        private static readonly ConstructorInfo RandRedirectorConstructor = typeof(RandRedirector).GetConstructor(Type.EmptyTypes);
        private static readonly ConstructorInfo RandRedirectorSeededConstructor = typeof(RandRedirector).GetConstructor([typeof(int)]);

        /// <summary>Transpiler that replaces all calls to <see cref="System.Random"/> constructor with calls to <see cref="RandRedirector"/> constructor</summary>
        internal static IEnumerable<CodeInstruction> FixRNG(IEnumerable<CodeInstruction> instr, MethodBase original)
        {
            var anythingPatched = false;
            
            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Newobj && ci.operand is ConstructorInfo constructorInfo)
                {
                    if (constructorInfo == SystemRandConstructor)
                    {
                        ci.operand = RandRedirectorConstructor;
                        anythingPatched = true;
                    }
                    else if (constructorInfo == SystemRandSeededConstructor)
                    {
                        ci.operand = RandRedirectorSeededConstructor;
                        anythingPatched = true;
                    }
                }

                yield return ci;
            }

            if (!anythingPatched) Log.Warning($"No System RNG was patched for method: {original?.FullDescription() ?? "(unknown method)"}");
        }

        /// <summary>This class allows replacing any <see cref="System.Random"/> calls with <see cref="Verse.Rand"/> calls</summary>
        public class RandRedirector : Random
        {
            private static RandRedirector instance;
            public static RandRedirector Instance => instance ??= new RandRedirector();
            
            public RandRedirector()
            { }

            public RandRedirector(int seed) : base(seed)
            { }

            public override int Next() => Rand.Range(0, int.MaxValue);

            public override int Next(int maxValue) => Rand.Range(0, maxValue);

            public override int Next(int minValue, int maxValue) => Rand.Range(minValue, maxValue);

            public override void NextBytes(byte[] buffer)
            {
                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = (byte)Rand.RangeInclusive(0, 255);
            }

            public override double NextDouble() => Rand.Range(0f, 1f);
        }

        #endregion

        #region Unity RNG transpiler

        private static readonly MethodInfo UnityRandomRangeInt = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
        private static readonly MethodInfo UnityRandomRangeIntObsolete = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.RandomRange), [typeof(int), typeof(int)]);
        private static readonly MethodInfo UnityRandomRangeFloat = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(float), typeof(float)]);
        private static readonly MethodInfo UnityRandomRangeFloatObsolete = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.RandomRange), [typeof(float), typeof(float)]);
        private static readonly MethodInfo UnityRandomValue = AccessTools.PropertyGetter(typeof(UnityEngine.Random), nameof(UnityEngine.Random.value));
        private static readonly MethodInfo UnityInsideUnitCircle = AccessTools.PropertyGetter(typeof(UnityEngine.Random), nameof(UnityEngine.Random.insideUnitCircle));

        private static readonly MethodInfo VerseRandomRangeInt = AccessTools.Method(typeof(Rand), nameof(Rand.Range), [typeof(int), typeof(int)]);
        private static readonly MethodInfo VerseRandomRangeFloat = AccessTools.Method(typeof(Rand), nameof(Rand.Range), [typeof(float), typeof(float)]);
        private static readonly MethodInfo VerseRandomValue = AccessTools.PropertyGetter(typeof(Rand), nameof(Rand.Value));
        private static readonly MethodInfo VerseInsideUnitCircle = AccessTools.PropertyGetter(typeof(Rand), nameof(Rand.InsideUnitCircle));

        internal static IEnumerable<CodeInstruction> FixUnityRNG(IEnumerable<CodeInstruction> instr, MethodBase original)
        {
            var anythingPatched = false;
            
            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Call && ci.operand is MethodInfo method)
                {
                    if (method == UnityRandomRangeInt || method == UnityRandomRangeIntObsolete)
                    {
                        ci.operand = VerseRandomRangeInt;
                        anythingPatched = true;
                    }
                    else if (method == UnityRandomRangeFloat || method == UnityRandomRangeFloatObsolete)
                    {
                        ci.operand = VerseRandomRangeFloat;
                        anythingPatched = true;
                    }
                    else if (method == UnityRandomValue)
                    {
                        ci.operand = VerseRandomValue;
                        anythingPatched = true;
                    }
                    else if (method == UnityInsideUnitCircle)
                    {
                        ci.operand = VerseInsideUnitCircle;
                        anythingPatched = true;
                    }
                }

                yield return ci;
            }

            if (!anythingPatched) Log.Warning($"No Unity RNG was patched for method: {original?.FullDescription() ?? "(unknown method)"}");
        }
        #endregion

        #region Cancel in interface

        /// <summary>Patches the method to cancel the call if it ends up being called from the UI</summary>
        /// <param name="methodNames">Names (type colon name) of the methods to patch</param>
        public static void PatchCancelInInterface(params string[] methodNames) => PatchCancelInInterface(methodNames as IEnumerable<string>);

        /// <summary>Patches the method to cancel the call if it ends up being called from the UI</summary>
        /// <param name="methodNames">Names (type colon name) of the methods to patch</param>
        public static void PatchCancelInInterface(IEnumerable<string> methodNames)
            => PatchCancelInInterface(methodNames
                .Select(m =>
                {
                    var method = AccessTools.DeclaredMethod(m) ?? AccessTools.Method(m);
                    if (method == null)
                        Log.Error($"({nameof(PatchingUtilities)}) Could not find method {m}");
                    return method;
                })
                .Where(m => m != null));

        /// <summary>Patches the method to cancel the call if it ends up being called from the UI</summary>
        /// <param name="methods">Methods to patch</param>
        public static void PatchCancelInInterface(params MethodBase[] methods) => PatchCancelInInterface(methods as IEnumerable<MethodBase>);

        /// <summary>Patches the method to cancel the call if it ends up being called from the UI</summary>
        /// <param name="methods">Methods to patch</param>
        public static void PatchCancelInInterface(IEnumerable<MethodBase> methods)
        {
            var patch = new HarmonyMethod(typeof(PatchingUtilities), nameof(CancelInInterface));
            foreach (var method in methods)
                PatchCancelInInterfaceInternal(method, patch);
        }

        /// <summary>Patches the method to cancel the call if it ends up being called from the UI and set __result to true (for patching prefixes to let the original run)</summary>
        /// <param name="methodNames">Names (type colon name) of the methods to patch</param>
        public static void PatchCancelInInterfaceSetResultToTrue(params string[] methodNames) => PatchCancelInInterfaceSetResultToTrue(methodNames as IEnumerable<string>);

        /// <summary>Patches the method to cancel the call if it ends up being called from the UI and set __result to true (for patching prefixes to let the original run)</summary>
        /// <param name="methodNames">Names (type colon name) of the methods to patch</param>
        public static void PatchCancelInInterfaceSetResultToTrue(IEnumerable<string> methodNames)
            => PatchCancelInInterfaceSetResultToTrue(methodNames
                .Select(m =>
                {
                    var method = AccessTools.DeclaredMethod(m) ?? AccessTools.Method(m);
                    if (method == null)
                        Log.Error($"({nameof(PatchingUtilities)}) Could not find method {m}");
                    return method;
                })
                .Where(m => m != null));

        /// <summary>Patches the method to cancel the call if it ends up being called from the UI and set __result to true (for patching prefixes to let the original run)</summary>
        /// <param name="methods">Methods to patch</param>
        public static void PatchCancelInInterfaceSetResultToTrue(params MethodBase[] methods) => PatchCancelInInterfaceSetResultToTrue(methods as IEnumerable<MethodBase>);

        /// <summary>Patches the method to cancel the call if it ends up being called from the UI and set __result to true (for patching prefixes to let the original run)</summary>
        /// <param name="methods">Methods to patch</param>
        public static void PatchCancelInInterfaceSetResultToTrue(IEnumerable<MethodBase> methods)
        {
            var patch = new HarmonyMethod(typeof(PatchingUtilities), nameof(CancelInInterfaceSetResultToTrue));
            foreach (var method in methods)
                PatchCancelInInterfaceInternal(method, patch);
        }

        private static void PatchCancelInInterfaceInternal(MethodBase method, HarmonyMethod patch)
        {
            if (method == null)
                Log.Error("Trying to patch cancel in interface on a null method.");
            else
                MpCompat.harmony.Patch(method, prefix: patch);
        }

        private static bool CancelInInterface() => !MP.InInterface;

        private static bool CancelInInterfaceSetResultToTrue(ref bool __result)
        {
            if (!MP.InInterface)
                return true;

            __result = true;
            return false;
        }

        #endregion

        #region Find.CurrentMap replacer

        public static void ReplaceCurrentMapUsage(Type type, string methodName)
        {
            if (type == null)
            {
                Log.Error(methodName == null 
                    ? "Trying to patch current map usage for null type and null or empty method name."
                    : $"Trying to patch current map usage for null type ({methodName}).");
                return;
            }

            if (methodName.NullOrEmpty())
            {
                Log.Error($"Trying to patch current map usage for null or empty method name ({type.FullName}).");
                return;
            }

            var method = AccessTools.DeclaredMethod(type, methodName) ?? AccessTools.Method(type, methodName);
            if (method != null)
                ReplaceCurrentMapUsage(method);
            else
            {
                var name = type.Namespace.NullOrEmpty() ? methodName : $"{type.Name}:{methodName}";
                Log.Warning($"Trying to patch current map usage for null method ({name}). Was the method removed or renamed?");
            }
        }

        public static void ReplaceCurrentMapUsage(string typeColonName, bool logIfNothingPatched = true, bool logIfMissingMethod = true)
        {
            if (typeColonName.NullOrEmpty())
            {
                Log.Error("Trying to patch current map usage for null or empty method name.");
                return;
            }

            var method = AccessTools.DeclaredMethod(typeColonName) ?? AccessTools.Method(typeColonName);
            if (method != null)
                ReplaceCurrentMapUsage(method, logIfNothingPatched, logIfMissingMethod);
            else if (logIfMissingMethod)
                Log.Warning($"Trying to patch current map usage for null method ({typeColonName}). Was the method removed or renamed?");
        }

        public static void ReplaceCurrentMapUsage(MethodBase method, bool logIfNothingPatched = true, bool logIfMissingMethod = true)
        {
            if (method != null)
            {
                var transpiler = new HarmonyMethod(logIfNothingPatched
                    ? MpMethodUtil.MethodOf(ReplaceCurrentMapUsageTranspiler)
                    : MpMethodUtil.MethodOf(ReplaceCurrentMapUsageNoLogTranspiler));

                MpCompat.harmony.Patch(method, transpiler: transpiler);
            }
            else if (logIfMissingMethod)
                Log.Warning("Trying to patch current map usage for null method. Was the method removed or renamed?");
        }

        private static IEnumerable<CodeInstruction> ReplaceCurrentMapUsageTranspiler(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
            => ReplaceCurrentMapUsageTranspilerInternal(instr, baseMethod, true);

        private static IEnumerable<CodeInstruction> ReplaceCurrentMapUsageNoLogTranspiler(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
            => ReplaceCurrentMapUsageTranspilerInternal(instr, baseMethod, false);

        private static IEnumerable<CodeInstruction> ReplaceCurrentMapUsageTranspilerInternal(IEnumerable<CodeInstruction> instr, MethodBase baseMethod, bool logNoCurrentMapPatches)
        {
            var helper = new CurrentMapPatchHelper(baseMethod);

            foreach (var ci in instr)
            {
                yield return ci;

                // Process current instruction and (if we got new instructions to insert) - yield return them as well.
                foreach (var newInstr in helper.ProcessCurrentInstruction(ci))
                    yield return newInstr;
            }

            var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";

            if (!helper.IsSupported)
                Log.Warning($"Unsupported type, can't patch current map usage for {name}");
            else if (logNoCurrentMapPatches && !helper.IsPatched)
                Log.Warning($"Failed patching current map usage for {name}");
#if DEBUG
            else if (!helper.IsPatched)
                Log.Warning($"Failed patching current map usage for {name}, but logging was disabled");
            else
                Log.Warning($"Successfully patched the current map usage for {name}");
#endif
        }

        private class CurrentMapPatchHelper
        {
            private enum CurrentMapUserType
            {
                Thing,
                ThingComp,
                Hediff,
                HediffComp,
                GameCondition,
                MapComponent,
                IncidentParms,

                // Unsupported
                UnsupportedType,
            }

            private static readonly MethodInfo TargetMethodFind = AccessTools.PropertyGetter(typeof(Find), nameof(Find.CurrentMap));
            private static readonly MethodInfo TargetMethodGame = AccessTools.PropertyGetter(typeof(Game), nameof(Game.CurrentMap));

            private readonly CurrentMapUserType currentType;
            private readonly MethodBase baseMethod;
            private readonly IReadOnlyList<CodeInstruction> instructions;

            public bool IsSupported => currentType != CurrentMapUserType.UnsupportedType;
            public bool IsPatched { get; private set; } = false;

            public CurrentMapPatchHelper(MethodBase baseMethod)
            {
                this.baseMethod = baseMethod ?? throw new ArgumentNullException(nameof(baseMethod));
                currentType = GetMapUserForMethod(this.baseMethod, out var earlyInstr);
                instructions = PrepareInstructions(earlyInstr, currentType);

#if DEBUG
                Log.Warning($"Current map type: {currentType}. Early instruction {earlyInstr?.opcode.ToStringSafe()} with operand {earlyInstr?.operand.ToStringSafe()}. Created a total of {instructions?.Count.ToStringSafe()} replacement instructions.");
#endif
            }

            public IEnumerable<CodeInstruction> ProcessCurrentInstruction(CodeInstruction ci)
            {
                if (currentType is >= CurrentMapUserType.UnsupportedType or < 0)
                    yield break;

                if (ci.opcode != OpCodes.Call || ci.operand is not MethodInfo method)
                    yield break;

                var first = true;

                if (method == TargetMethodGame)
                {
                    IsPatched = true;
                    first = false;

                    // Pop the `Game` local
                    ci.opcode = OpCodes.Pop;
                    ci.operand = null;
                }
                // Unsupported method
                else if (method != TargetMethodFind)
                    yield break;

                foreach (var output in instructions)
                {
                    // Replace the current instruction with the first we've got
                    if (first)
                    {
                        IsPatched = true;
                        first = false;

                        ci.opcode = output.opcode;
                        ci.operand = output.operand;
                    }
                    // Return the others as new code instructions. Avoid passing the same CodeInstruction instance
                    // multiple times in case it'll ever have some unintended side-effects.
                    else yield return new CodeInstruction(output.opcode, output.operand);
                }
            }

            private static IReadOnlyList<CodeInstruction> PrepareInstructions(CodeInstruction earlyInstruction, CurrentMapUserType type)
            {
                var instructions = new List<CodeInstruction>();

                if (earlyInstruction != null)
                    instructions.Add(earlyInstruction);

                switch (type)
                {
                    case CurrentMapUserType.Thing:
                    {
                        // Call the thing's Map getter
                        instructions.Add(new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map))));

                        break;
                    }
                    case CurrentMapUserType.ThingComp:
                    {
                        // Load the comps's parent thing field
                        instructions.Add(new CodeInstruction(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(ThingComp), nameof(ThingComp.parent))));
                        // Call the parent thing's Map getter
                        instructions.Add(new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map))));

                        break;
                    }
                    case CurrentMapUserType.Hediff:
                    {
                        // Load the hediff's pawn field
                        instructions.Add(new CodeInstruction(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(Hediff), nameof(Hediff.pawn))));
                        // Call the pawn's Map getter
                        instructions.Add(new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.Map))));

                        break;
                    }
                    case CurrentMapUserType.HediffComp:
                    {
                        // Call the comp's Pawn getter (which is just short for getting parent field followed by pawn field)
                        instructions.Add(new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(HediffComp), nameof(HediffComp.Pawn))));
                        // Call the pawn's Map getter
                        instructions.Add(new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.Map))));

                        break;
                    }
                    case CurrentMapUserType.GameCondition:
                    {
                        // Call the `SingleMap` getter
                        instructions.Add(new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(GameCondition), nameof(GameCondition.SingleMap))));

                        break;
                    }
                    case CurrentMapUserType.MapComponent:
                    {
                        // Access the `map` field
                        instructions.Add(new CodeInstruction(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(MapComponent), nameof(MapComponent.map))));

                        break;
                    }
                    // Based on method arguments
                    case CurrentMapUserType.IncidentParms:
                    {
                        // Load the `IncidentParms.target` field
                        instructions.Add(new CodeInstruction(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(IncidentParms), nameof(IncidentParms.target))));
                        // Cast the IIncidentTarget to Map
                        instructions.Add(new CodeInstruction(OpCodes.Castclass, typeof(Map)));

                        break;
                    }
                }

                return instructions.AsReadOnly();
            }

            private static CurrentMapUserType GetMapUserForMethod(MethodBase method, out CodeInstruction earlyInstr)
            {
                earlyInstr = null;

                // Based on declaring type
                if (method.DeclaringType != null && !method.IsStatic)
                {
                    var type = GetMapUserForType(method.DeclaringType);
                    if (type != CurrentMapUserType.UnsupportedType)
                    {
                        earlyInstr = new CodeInstruction(OpCodes.Ldarg_0); // Call to `this`
                        return type;
                    }
                }

                // Based on method arguments
                var parms = method.GetParameters();
                for (var index = 0; index < parms.Length; index++)
                {
                    var param = parms[index];
                    var type = GetMapUserForType(param.ParameterType);
                    if (type != CurrentMapUserType.UnsupportedType)
                    {
                        earlyInstr = GetLdargForIndex(method, index);
                        return type;
                    }
                }

                return CurrentMapUserType.UnsupportedType;
            }

            private static CurrentMapUserType GetMapUserForType(Type type)
            {
                if (typeof(Thing).IsAssignableFrom(type))
                    return CurrentMapUserType.Thing;
                
                if (typeof(ThingComp).IsAssignableFrom(type))
                    return CurrentMapUserType.ThingComp;

                if (typeof(Hediff).IsAssignableFrom(type))
                    return CurrentMapUserType.Hediff;

                if (typeof(HediffComp).IsAssignableFrom(type))
                    return CurrentMapUserType.HediffComp;

                if (typeof(GameCondition).IsAssignableFrom(type))
                    return CurrentMapUserType.GameCondition;

                if (typeof(MapComponent).IsAssignableFrom(type))
                    return CurrentMapUserType.MapComponent;

                if (typeof(IncidentParms).IsAssignableFrom(type))
                    return CurrentMapUserType.IncidentParms;

                return CurrentMapUserType.UnsupportedType;
            }

            private static CodeInstruction GetLdargForIndex(MethodBase method, int index)
            {
                if (index < 0)
                    return null;

                // For non-static method, arg 0 is `this`
                if (!method.IsStatic)
                    index++;

                return index switch
                {
                    0 => new CodeInstruction(OpCodes.Ldarg_0),
                    1 => new CodeInstruction(OpCodes.Ldarg_1),
                    2 => new CodeInstruction(OpCodes.Ldarg_2),
                    3 => new CodeInstruction(OpCodes.Ldarg_3),
                    <= byte.MaxValue => new CodeInstruction(OpCodes.Ldarg_S, index),
                    <= ushort.MaxValue => new CodeInstruction(OpCodes.Ldarg, index),
                    _ => null
                };
            }
        }

        #endregion
        
        #region TryGainMemory early thought init

        public delegate bool TryHandleGainMemory(Thought_Memory thought);
        private static List<TryHandleGainMemory> tryGainMemoryHandlers;
        private static bool patchedVanillaMethod = false;

        public static void PatchTryGainMemory(TryHandleGainMemory memoryGainHandler)
        {
            if (memoryGainHandler == null)
            {
                Log.Error("Trying to patch TryGainMemory, but delegate is null.");
                return;
            }

            tryGainMemoryHandlers ??= [];
            tryGainMemoryHandlers.Add(memoryGainHandler);

            if (patchedVanillaMethod)
                return;

            // Mods sometimes initialize unsafe stuff during MoodOffset, which is not called during ticking (but alert updates).
            // We can't easily patch those classes to initialize their stuff earlier (like during a constructor or Init() call),
            // as those are called while the pawn field is still not set (so the initialization will fail/error out).
            // The earliest place I could find was `MemoryThoughtHandler.TryGainMemory` call, as that's where the pawn field is initialized.
            var targetMethod = AccessTools.DeclaredMethod(
                typeof(MemoryThoughtHandler),
                nameof(MemoryThoughtHandler.TryGainMemory),
                [typeof(Thought_Memory), typeof(Pawn)]);
            MpCompat.harmony.Patch(targetMethod, postfix: new HarmonyMethod(typeof(PatchingUtilities), nameof(PostTryGainMemory)));
            patchedVanillaMethod = true;
        }

        private static void PostTryGainMemory(Thought_Memory newThought)
        {
            // Only do something if the pawn is set (the call wasn't cancelled)
            if (newThought.pawn == null)
                return;

            // No need to check for tryGainMemoryHandlers being null, it's instantiated before the patch is applied
            for (var i = 0; i < tryGainMemoryHandlers.Count; i++)
            {
                if (tryGainMemoryHandlers[i](newThought))
                    return;
            }
        }

        #endregion

        #region Async Time

        private static bool isAsyncTimeSetup = false;
        private static bool isAsyncTimeGameCompSuccessful = false;
        private static bool isAsyncTimeMapCompSuccessful = false;
        // Multiplayer
        private static AccessTools.FieldRef<object> multiplayerGameField;
        // MultiplayerGame
        private static AccessTools.FieldRef<object, object> gameGameCompField;
        // MultiplayerGameComp
        private static AccessTools.FieldRef<object, bool> gameCompAsyncTimeField;
        // Extensions
        private static FastInvokeHandler getAsyncTimeCompForMapMethod;
        // AsyncTimeComp
        private static AccessTools.FieldRef<object, int> asyncTimeMapTicksField;
        private static AccessTools.FieldRef<object, TimeSlower> asyncTimeSlowerField;
        private static AccessTools.FieldRef<object, TimeSpeed> asyncTimeTimeSpeedIntField;

        public static bool IsAsyncTime
            => isAsyncTimeGameCompSuccessful &&
               gameCompAsyncTimeField(gameGameCompField(multiplayerGameField()));

        public static void SetupAsyncTime()
        {
            if (isAsyncTimeSetup)
                return;
            isAsyncTimeSetup = true;

            try
            {
                // Multiplayer
                multiplayerGameField = AccessTools.StaticFieldRefAccess<object>(
                    AccessTools.DeclaredField("Multiplayer.Client.Multiplayer:game"));
            }
            catch (Exception e)
            {
                Log.Error($"Encountered an exception while settings up core async time functionality:\n{e}");
                // Nothing else will work here without this, just return early.
                return;
            }

            try
            {
                // MultiplayerGame
                gameGameCompField = AccessTools.FieldRefAccess<object>(
                    "Multiplayer.Client.MultiplayerGame:gameComp");
                // MultiplayerGameComp
                gameCompAsyncTimeField = AccessTools.FieldRefAccess<bool>(
                    "Multiplayer.Client.Comp.MultiplayerGameComp:asyncTime");

                isAsyncTimeGameCompSuccessful = true;
            }
            catch (Exception e)
            {
                Log.Error($"Encountered an exception while settings up game async time:\n{e}");
            }

            try
            {
                getAsyncTimeCompForMapMethod = MethodInvoker.GetHandler(
                    AccessTools.DeclaredMethod("Multiplayer.Client.Extensions:AsyncTime"));

                var type = AccessTools.TypeByName("Multiplayer.Client.AsyncTimeComp");
                asyncTimeMapTicksField = AccessTools.FieldRefAccess<int>(type, "mapTicks");
                asyncTimeSlowerField = AccessTools.FieldRefAccess<TimeSlower>(type, "slower");
                asyncTimeTimeSpeedIntField = AccessTools.FieldRefAccess<TimeSpeed>(type, "timeSpeedInt");

                isAsyncTimeMapCompSuccessful = true;
            }
            catch (Exception e)
            {
                Log.Error($"Encountered an exception while settings up map async time:\n{e}");
            }
        }

        // Taken from MP, GetAndSetFromMap was slightly modified.
        // https://github.com/rwmt/Multiplayer/blob/master/Source/Client/AsyncTime/SetMapTime.cs#L166-L204
        // We could access MP's struct and methods using reflection, but there were some issues
        // in that approach - mainly performance wasn't perfect, as MethodInfo.Invoke call was required.
        // Having an identical struct in MP Compat won't cause issues, as there's nothing to conflict with MP.
        // On top of that - if the struct ever gets renamed or moved to a different namespace in MP, it won't
        // affect MP Compat. However, in case there's any logic changes in MP - they won't be reflected here.
        // Ideally, we'll make something like this in the MP API.
        public struct TimeSnapshot
        {
            public int ticks;
            public TimeSpeed speed;
            public TimeSlower slower;

            public void Set()
            {
                Find.TickManager.ticksGameInt = ticks;
                Find.TickManager.slower = slower;
                Find.TickManager.curTimeSpeed = speed;
            }

            public static TimeSnapshot Current()
            {
                return new TimeSnapshot
                {
                    ticks = Find.TickManager.ticksGameInt,
                    speed = Find.TickManager.curTimeSpeed,
                    slower = Find.TickManager.slower
                };
            }

            public static TimeSnapshot? GetAndSetFromMap(Map map)
            {
                if (map == null) return null;
                if (!isAsyncTimeMapCompSuccessful) return null;

                var prev = Current();

                var tickManager = Find.TickManager;
                var mapComp = getAsyncTimeCompForMapMethod(null, map);

                tickManager.ticksGameInt = asyncTimeMapTicksField(mapComp);
                tickManager.slower = asyncTimeSlowerField(mapComp);
                tickManager.CurTimeSpeed = asyncTimeTimeSpeedIntField(mapComp);

                return prev;
            }
        }

        #endregion

        #region Timestamp Fixer

        private static bool isTimestampFixerInitialized = false;
        private static Type timestampFixerDelegateType;
        private static Type timestampFixerListType;
        private static AccessTools.FieldRef<IDictionary> timestampFieldsDictionaryField;

        public static void RegisterTimestampFixer(Type type, MethodInfo timestampFixerMethod)
        {
            if (type == null || timestampFixerMethod == null)
            {
                Log.Error($"Trying to register timestamp fixer failed - value null. Type={type.ToStringSafe()}, Method={timestampFixerMethod.ToStringSafe()}");
                return;
            }

            InitializeTimestampFixer();

            // Initialize call will display proper errors if needed
            if (timestampFixerDelegateType == null || timestampFixerListType == null || timestampFieldsDictionaryField == null)
                return;

            try
            {
                var dict = timestampFieldsDictionaryField();
                IList list;
                // If the dictionary already contains list of timestamp fixers for
                // a given type, use that list and add another one to it.
                if (dict.Contains(type))
                    list = (IList)dict[type];
                // If needed, create a new list of timestamp fixers for a given type.
                else
                    dict[type] = list = (IList)Activator.CreateInstance(timestampFixerListType);

                // Create a FieldGetter<IExposable> delegate using the provided method
                list.Add(Delegate.CreateDelegate(timestampFixerDelegateType, timestampFixerMethod));
            }
            catch (Exception e)
            {
                Log.Error($"Trying to initialize timestamp fixer failed, exception caught:\n{e}");
            }
        }

        public static void InitializeTimestampFixer()
        {
            if (isTimestampFixerInitialized)
                return;
            isTimestampFixerInitialized = true;

            try
            {
                var type = AccessTools.TypeByName("Multiplayer.Client.Patches.TimestampFixer");
                if (type == null)
                {
                    Log.Error("Trying to initialize timestamp fixer failed, could not find TimestampFixer type.");
                    return;
                }

                // Get the type of the delegate. We need to specify `1 as it's a generic delegate.
                var delType = AccessTools.Inner(type, "FieldGetter`1");
                if (delType == null)
                {
                    Log.Error("Trying to initialize timestamp fixer failed, could not find FieldGetter inner type.");
                    return;
                }

                timestampFieldsDictionaryField = AccessTools.StaticFieldRefAccess<IDictionary>(
                    AccessTools.DeclaredField(type, "timestampFields"));
                // The list only accepts FieldGetter<IExposable>
                timestampFixerDelegateType = delType.MakeGenericType(typeof(IExposable));
                timestampFixerListType = typeof(List<>).MakeGenericType(timestampFixerDelegateType);
            }
            catch (Exception e)
            {
                Log.Error($"Trying to initialize timestamp fixer failed, exception caught:\n{e}");

                timestampFixerDelegateType = null;
                timestampFixerListType = null;
                timestampFieldsDictionaryField = null;
            }
        }

        #endregion

        #region Unsafe long event cancelling

        public static bool AllowedToRunLongEvents { get; private set; } = true;
        private static bool longEventMarkerPatchActive = false;

        /// <summary>Patches the methods to cancel the call if it's unsafe to start long events</summary>
        /// <param name="methodNames">Names (type colon name) of the methods to patch</param>
        public static void CancelCallIfLongEventsAreUnsafe(params string[] methodNames) => CancelCallIfLongEventsAreUnsafe(methodNames as IEnumerable<string>);

        /// <summary>Patches the methods to cancel the call if it's unsafe to start long events</summary>
        /// <param name="methodNames">Names (type colon name) of the methods to patch</param>
        public static void CancelCallIfLongEventsAreUnsafe(IEnumerable<string> methodNames)
            => CancelCallIfLongEventsAreUnsafe(methodNames
                .Select(m =>
                {
                    var method = AccessTools.DeclaredMethod(m) ?? AccessTools.Method(m);
                    if (method == null)
                        Log.Error($"({nameof(PatchingUtilities)}) Could not find method {m}");
                    return method;
                })
                .Where(m => m != null));

        /// <summary>Patches the methods to cancel the call if it's unsafe to start long events</summary>
        /// <param name="methods">Methods to patch</param>
        public static void CancelCallIfLongEventsAreUnsafe(params MethodBase[] methods) => CancelCallIfLongEventsAreUnsafe(methods as IEnumerable<MethodBase>);

        /// <summary>Patches the methods to cancel the call if it's unsafe to start long events</summary>
        /// <param name="methods">Methods to patch</param>
        public static void CancelCallIfLongEventsAreUnsafe(IEnumerable<MethodBase> methods)
        {
            PatchLongEventMarkers();

            var patch = new HarmonyMethod(typeof(PatchingUtilities), nameof(StopSecondHostCall));
            foreach (var method in methods)
            {
                if (method == null)
                    Log.Error("Trying to patch unsafe long event, but the method is null.");
                else
                    MpCompat.harmony.Patch(method, prefix: patch);
            }
        }

        public static void PatchLongEventMarkers()
        {
            if (longEventMarkerPatchActive)
                return;

            longEventMarkerPatchActive = true;

            // Patch to MP to mark when to stop long events Perspective: Ores
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod("Multiplayer.Client.SaveLoad:LoadInMainThread"),
                prefix: new HarmonyMethod(typeof(PatchingUtilities), nameof(SetDisallowedToRun)),
                finalizer: new HarmonyMethod(typeof(PatchingUtilities), nameof(SetAllowedToRun)));
        }

        private static void SetDisallowedToRun() => AllowedToRunLongEvents = false;

        private static void SetAllowedToRun() => AllowedToRunLongEvents = true;

        private static bool StopSecondHostCall()
        {
            // Stop the host from running it for the second time. It messes stuff up due to the place it's called from.
            // It can cause the host to start generating non-local IDs when they should generate local ones. This causes
            // the host to assign higher IDs than all the connected clients.
            return AllowedToRunLongEvents;
        }

        #endregion

        #region Method replacer

        /// <summary>
        /// A more specialized alternative to <see cref="Transpilers.MethodReplacer"/>.
        /// It will replace all occurrences of a given method, but only if the specified text was encountered (unless another disallowed text was encountered).
        /// It may come especially handy for replacing text buttons with a specific text.
        /// </summary>
        /// <param name="instr">The enumeration of <see cref="T:HarmonyLib.CodeInstruction"/> to act on.</param>
        /// <param name="from">Method or constructor to search for.</param>
        /// <param name="to">Method or constructor to replace with.</param>
        /// <param name="baseMethod">Method or constructor that is being patched, used for logging to provide information on which patched method had issues.</param>
        /// <param name="extraInstructionsBefore">Extra instructions to insert before the method or constructor is called.</param>
        /// <param name="extraInstructionsAfter">Extra instructions to insert after the method or constructor is called.</param>
        /// <param name="expectedReplacements">The expected number of times the method should be replaced. Use -1 to disable, or use -2 to expect unspecified amount (but more than 1 replacement).</param>
        /// <param name="targetText">The text that should appear before replacing a method. A first occurence of the method after this text will be replaced. Mutually exclusive with <paramref name="targetInstruction"/>.</param>
        /// <param name="excludedText">The text that excludes the next method from being patched. Will skip patching the next time the method was going to be patched. Mutually exclusive with <paramref name="excludedInstruction"/>.</param>
        /// <param name="targetInstruction">A function that will match a <see cref="T:HarmonyLib.CodeInstruction"/> that must appear before replacing a method. A first occurence of the method after this match will be replaced. Mutually exclusive with <paramref name="targetText"/>.</param>
        /// <param name="excludedInstruction">A function that will match a <see cref="T:HarmonyLib.CodeInstruction"/> that will prevent the next method from being patched. Will skip patching the next time the method was going to be patched. Mutually exclusive with <paramref name="excludedText"/>.</param>
        /// <returns>Modified enumeration of <see cref="T:HarmonyLib.CodeInstruction"/></returns>
        public static IEnumerable<CodeInstruction> ReplaceMethod(this IEnumerable<CodeInstruction> instr, MethodBase from, MethodBase to = null, MethodBase baseMethod = null, Func<CodeInstruction, IEnumerable<CodeInstruction>> extraInstructionsBefore = null, Func<CodeInstruction, IEnumerable<CodeInstruction>> extraInstructionsAfter = null, int expectedReplacements = -1, string targetText = null, string excludedText = null, Func<CodeInstruction, bool> targetInstruction = null, Func<CodeInstruction, bool> excludedInstruction = null)
        {
            if (instr == null)
                throw new ArgumentNullException(nameof(instr));
            if (from == null)
                Log.Error($"Call to {nameof(ReplaceMethod)} is meaningless as the target method is null for method {MethodName()}");
            // Check if all meaningful arguments are null and provide a proper warning if they are.
            if (to == null && extraInstructionsBefore == null && extraInstructionsAfter == null)
                Log.Error($"Call to {nameof(ReplaceMethod)} is meaningless as no useful arguments were provided for method {MethodName()}");

            if (targetText != null)
            {
                if (targetInstruction != null)
                    Log.Error($"Call to {nameof(ReplaceMethod)} provides both {nameof(targetText)} and {nameof(targetInstruction)}, which are mutually exclusive, for method {MethodName()}");
                targetInstruction = ci => ci.opcode == OpCodes.Ldstr && ci.operand as string == targetText;
            }

            if (excludedText != null)
            {
                if (excludedInstruction != null)
                    Log.Error($"Call to {nameof(ReplaceMethod)} provides both {nameof(excludedText)} and {nameof(excludedInstruction)}, which are mutually exclusive, for method {MethodName()}");
                excludedInstruction = ci => ci.opcode == OpCodes.Ldstr && ci.operand as string == excludedText;
            }

            // Check for instructions only if target instruction isn't null
            var isCorrectText = targetInstruction == null;
            var skipNextCall = false;
            var replacedCount = 0;
            var insertInstructionsAfter = false;

            foreach (var ci in instr)
            {
                // Excluded instructions if matched will cancel replacement of the next occurrence
                // of the method. Used by `MagicCardUtility:CustomPowersHandler`, as the text
                // `TM_Learn` appears twice there, but in a single case it's combined with
                // `TM_MCU_PointsToLearn`, in which case we ignore the button (as the
                // button does nothing in that particular case).
                if (excludedInstruction?.Invoke(ci) == true)
                {
                    skipNextCall = true;
                }
                else if (targetInstruction?.Invoke(ci) == true)
                {
                    isCorrectText = true;
                }
                else if (isCorrectText)
                {
                    if (ci.operand is MethodBase method && method == from)
                    {
                        // Check for next instruction only if target instruction isn't null
                        isCorrectText = targetInstruction == null;

                        if (skipNextCall)
                        {
                            skipNextCall = false;
                        }
                        else
                        {
                            if (to != null)
                            {
                                // Replace method with our own
                                ci.opcode = to.IsConstructor ? OpCodes.Newobj : OpCodes.Call;
                                ci.operand = to;
                            }

                            if (extraInstructionsBefore != null)
                            {
                                foreach (var extraInstr in extraInstructionsBefore(ci))
                                    yield return extraInstr;
                            }

                            replacedCount++;
                            // If extraInstructionsAfter isn't null,
                            // make sure they are actually inserted.
                            insertInstructionsAfter = true;
                        }
                    }
                }

                yield return ci;

                if (insertInstructionsAfter && extraInstructionsAfter != null)
                {
                    insertInstructionsAfter = false;

                    foreach (var extraInstr in extraInstructionsAfter(ci))
                    {
                        yield return extraInstr;
                    }
                }
            }

            string MethodName()
            {
                if (baseMethod == null)
                    return "(unknown)";
                if ((baseMethod.DeclaringType?.Namespace).NullOrEmpty())
                    return baseMethod.Name;
                return $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
            }

            if (replacedCount != expectedReplacements && expectedReplacements >= 0)
                Log.Warning($"Patched incorrect number of {from.DeclaringType?.Name ?? "null"}.{from.Name} calls (patched {replacedCount}, expected {expectedReplacements}) for method {MethodName()}");
            // Special case (-2) - expected some patched methods, but amount unspecified
            else if (replacedCount == 0 && expectedReplacements == -2)
                Log.Warning($"No calls of {from.DeclaringType?.Name ?? "null"}.{from.Name} were patched for method {MethodName()}");
        }

        #endregion
    }
}