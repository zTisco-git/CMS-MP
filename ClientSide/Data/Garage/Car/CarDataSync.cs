using System;
using System.Collections;
using CMS;
using CMS21Together.ClientSide.Data.Handle;
using CMS21Together.Shared;
using CMS21Together.Shared.Data;
using CMS21Together.Shared.Data.Vanilla.Cars;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace CMS21Together.ClientSide.Data.Garage.Car;

[HarmonyPatch]
public static class CarDataSync
{
	public static bool listen = true;

	[HarmonyPatch(typeof(CarLoader), nameof(CarLoader.SaveCarToFile), new Type[] { })]
	[HarmonyPostfix]
	public static void SaveCarToFileHook(CarLoader __instance)
	{
		if (!Client.Instance.isConnected || !listen)
		{
			listen = true;
			return;
		}

		if (__instance == null || string.IsNullOrEmpty(__instance.carToLoad))
			return;

		if (!NotificationCenter.IsGameReady)
			return;

		if (SceneManager.CurrentScene() != GameScene.garage)
			return;

		try
		{
			var carLoaderID = __instance.gameObject.name[10] - '0' - 1;
			if (carLoaderID < 0 || carLoaderID >= GameData.Instance.carLoaders.Length)
				return;

			int saveIndex = Helper.GetIndexFromCarLoaderName(__instance.name);
			var profile = Singleton<GameManager>.Instance.GameDataManager.CurrentProfileData;
			if (saveIndex < 0 || saveIndex >= profile.carsInGarage.Count)
				return;

			NewCarData carData = profile.carsInGarage[saveIndex];
			var modData = new ModNewCarData(carData, __instance.placeNo, __instance.orderConnection);
			ClientSend.LoadCarPacket(modData, carLoaderID);
		}
		catch (Exception ex)
		{
			MelonLogger.Warning($"[CarDataSync] Failed to sync car data: {ex.Message}");
		}
	}

	public static IEnumerator RestoreListeningFlag()
	{
		yield return new WaitForEndOfFrame();
		yield return new WaitForEndOfFrame();
		listen = true;
	}
}

