using HarmonyLib;
using FrooxEngine;
using System.Reflection;
using System.Reflection.Emit;

namespace Nimbus;

public partial class Nimbus
{

    /// <summary>
    /// Patches for SaveControl
    /// </summary>
    [HarmonyPatch(typeof(SaveControl))]
    public static class SaveControl_Patches
    {
        static readonly MethodInfo? target = typeof(Type).GetProperty("FullName")?.GetGetMethod();
        static readonly MethodInfo? legacyCall = typeof(NET8_Helpers).GetMethod(nameof(NET8_Helpers.GetLegacy));

        /// <summary>
        /// Patches the "SaveControl.StoreTypeVersions" method to store the legacy FullName
        /// </summary>
        /// <param name="instructions">Original code instructions</param>
        /// <returns>Modified code instructions</returns>
        [HarmonyTranspiler]
        [HarmonyPatch("StoreTypeVersions")]
        public static IEnumerable<CodeInstruction> StoreTypeVersions_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var inst in instructions)
            {
                if (inst.Calls(target))
                {
                    yield return new(OpCodes.Call, legacyCall);
                }
                else
                {
                    yield return inst;
                }
            }
        }
    }



    /// <summary>
    /// Patches for Worker
    /// </summary>
    [HarmonyPatch(typeof(Worker))]
    public static class Worker_Patches
    {
        /// <summary>
        /// Patches the "Worker.WorkerTypeName" property to return the legacy FullName
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="__result"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch("WorkerTypeName", MethodType.Getter)]
        public static bool WorkerTypeName_Prefix(Worker __instance, ref string __result)
        {
            #if DEBUG
            Type type = __instance.WorkerType;
            string typeName = type.GetLegacy();
            Debug($"(WorkerTypeName) Re-routing {type.FullName} to {typeName}!");
            #endif

            __result = __instance.WorkerType.GetLegacy();
            return false;
        }
    }



    /// <summary>
    /// Patches for the WorkerManager
    /// </summary>
    [HarmonyPatch(typeof(WorkerManager))]
    public static class WorkerManager_Patches
    {
        /// <summary>
        /// Patches the "WorkerManager.GetTypename method to return the legacy FullName
        /// </summary>
        /// <param name="__result">The result to change</param>
        /// <param name="type">The type to get the name of</param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch("GetTypename")]
        public static bool GetTypename_Prefix(ref string __result, Type type)
        {
            #if DEBUG
            string typeName = type.GetLegacy();
            Debug($"(WorkerManager) Re-routing {type.FullName} to {typeName}! ");
            #endif

            __result = type.GetLegacy();
            return false;
        }



        /// <summary>
        /// Patches <see cref="WorkerManager.GetType"/> to use a versionless check in case a type with a newer assembly version is received.
        /// </summary>
        /// <param name="__result">The result passed to the postfix. If the result is null, a version-less check is performed instead.</param>
        /// <param name="typename">The type to attempt parsing</param>
        [HarmonyPostfix]
        [HarmonyPatch("GetType")]
        public static void GetType_Postfix(string typename, ref Type __result)
        {
            #if DEBUG
            if (__result is null)
            {
                Msg($"Null type for {typename}! Attempting versionless check instead...");
            }
            #endif
            __result ??= GetType(string.Join(',', typename.Split(',').Where(s => !s.Trim().StartsWith("Version="))));

            #if DEBUG
            if (__result is null)
            {
                Msg($"Failed version-less check with type: {typename}... It's over, kids.");
            }
            #endif
        }



        /// <summary>
        /// The original <see cref="WorkerManager.GetType"/> method without any patches applied
        /// </summary>
        /// <param name="typename">Type name to attempt parsing</param>
        /// <returns>Parsed type</returns>
        /// <exception cref="NotImplementedException">Thrown if the reverse patch has failed</exception>
        [HarmonyReversePatch]
        [HarmonyPatch("GetType")]
        public static Type GetType(string typename) => throw new NotImplementedException("Harmony stub");
    }


    
    /// <summary>
    /// Patches for the the ThreadWorker
    /// </summary>
    public static class ThreadWorker_Patches //Trickier to patch, so a manual harmony patch is applied instead
    {
        static readonly MethodInfo? abortInfo = typeof(Thread).GetMethod("Abort", []);

        /// <summary>
        /// Patches "ThreadWorker.Abort" to call Thread.Interrupt instead of the deprecated Thread.Abort method
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        public static IEnumerable<CodeInstruction> Abort_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            if (abortInfo == null)
            {
                Msg("Abort method info was null!");
                foreach (var inst in instructions)
                    yield return inst;
            }
            else
            {
                foreach (var inst in instructions)
                {
                    if (inst.Calls(abortInfo))
                    {
                        yield return new(OpCodes.Call, typeof(Thread).GetMethod("Interrupt"));
                    }
                    else
                    {
                        yield return inst;
                    }
                }
            }
        }


        
        /// <summary>
        /// Finalizes the "WorkProcessor.JobWorker" method to swallow exceptions that arise from using Thread.Interrupt
        /// </summary>
        /// <param name="__exception">The exception to swallow</param>
        /// <returns>The exception to re-throw</returns>
        public static Exception JobWorker_Finalizer(Exception __exception)
        {
            if (__exception is ThreadInterruptedException) // Only catch ThreadInterruptedException
            {
                Debug($"Caught thread interrupt. Exception: {__exception}");
                return null!; // Return null. Emphatically.
            }
            else
            {
                return __exception;
            }
        }
    }
}
