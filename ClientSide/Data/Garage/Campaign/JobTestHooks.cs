using CMS;
using CMS.UI.Logic.Orders;
using CMS21Together.ClientSide.Data.Garage.Car;
using HarmonyLib;
using MelonLoader;

namespace CMS21Together.ClientSide.Data.Garage.Campaign;

[HarmonyPatch]
public static class JobTestHooks
{
	[HarmonyPatch(typeof(GameScript._ExitFromInterior_d__105), "MoveNext")]
	[HarmonyPostfix]
	public static void ExitFromInteriorPathTestHook(GameScript._ExitFromInterior_d__105 __instance, bool __result)
	{
		if (!Client.Instance.isConnected)
			return;

		if (__result)
			return;

		if (!__instance._isPathTestMode_5__2)
			return;

		var gameScript = __instance.__4__this;
		if (gameScript == null)
		{
			MelonLogger.Msg("[JobTestHooks->ExitFromInteriorPathTestHook] gameScript is null, aborting JobUpdate sync.");
			return;
		}

		var carLoader = gameScript.lastCarLoader;
		if (carLoader == null || carLoader.gameObject == null)
		{
			MelonLogger.Msg("[JobTestHooks->ExitFromInteriorPathTestHook] lastCarLoader is null, aborting JobUpdate sync.");
			return;
		}

		int carLoaderID = carLoader.gameObject.name[10] - '0' - 1;

		MelonLogger.Msg($"[JobTestHooks->ExitFromInteriorPathTestHook] Path test finished for carLoaderID {carLoaderID}, sending JobUpdate.");
		PartUpdateHooks.SyncJobProgressForCar(carLoaderID);
	}

	[HarmonyPatch(typeof(JobHelper), "CheckJob")]
	[HarmonyPostfix]
	public static void CheckJobHook(CarLoader carLoader, ref Job job)
	{
		if (!Client.Instance.isConnected)
			return;

		if (carLoader == null || carLoader.gameObject == null || job == null)
			return;

		int carLoaderID = carLoader.gameObject.name[10] - '0' - 1;

		MelonLogger.Msg($"[JobTestHooks->CheckJobHook] Job recalculated for jobID {job.id}, carLoaderID {carLoaderID}, sending JobUpdate.");
		PartUpdateHooks.SyncJobProgressForCar(carLoaderID);
	}
}

