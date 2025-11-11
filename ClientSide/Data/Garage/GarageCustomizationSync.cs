using System.Collections;
using CMS;
using CMS21Together.ClientSide.Data.Handle;
using CMS21Together.Shared.Data.Vanilla;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace CMS21Together.ClientSide.Data.Garage;

public static class GarageCustomizationSync
{
	private static bool listen = true;
	private static ModGarageCustomizationData lastLocal;
	private static float nextCheck;

	public static void Reset()
	{
		listen = true;
		lastLocal = null;
		nextCheck = 0f;
	}

	public static void Update()
	{
		if (!Client.Instance.isConnected || !ClientData.GameReady)
			return;

		if (Time.time < nextCheck)
			return;
		nextCheck = Time.time + 0.5f;

		var profile = Singleton<GameManager>.Instance.GameDataManager?.CurrentProfileData;
		if (profile == null || profile.garageCustomizationData == null)
			return;

		var current = new ModGarageCustomizationData(profile.garageCustomizationData);
		if (lastLocal != null && current.Equals(lastLocal))
			return;

		if (!listen)
		{
			lastLocal = current;
			return;
		}

		lastLocal = current;
		ClientData.Instance.garageCustomization = current;
		ClientSend.GarageCustomizationPacket(current);
	}

	public static void Apply(ModGarageCustomizationData data)
	{
		if (data == null)
			return;

		ClientData.Instance.garageCustomization = data;
		MelonCoroutines.Start(ApplyRoutine(data));
	}

	private static IEnumerator ApplyRoutine(ModGarageCustomizationData data)
	{
		listen = false;

		while (!ClientData.GameReady)
			yield return new WaitForSeconds(0.25f);

		var profile = Singleton<GameManager>.Instance.GameDataManager?.CurrentProfileData;
		var customization = data.ToGame();
		if (profile != null)
			profile.garageCustomizationData = customization;

		yield return new WaitForEndOfFrame();

		var loader = GarageLoader.Get();
		if (loader != null)
		{
			var loaderType = loader.GetType();
			var loadCustomization = AccessTools.Method(loaderType, "LoadCustomization", new[] { typeof(GarageCustomizationData) });
			if (loadCustomization != null)
			{
				loadCustomization.Invoke(loader, new object[] { customization });
			}
			else
			{
				var applyCustomization = AccessTools.Method(loaderType, "ApplyCustomization", new[] { typeof(GarageCustomizationData) });
				if (applyCustomization != null)
					applyCustomization.Invoke(loader, new object[] { customization });
				else
					MelonLogger.Warning("[GarageCustomizationSync] Unable to locate customization apply method on GarageLoader.");
			}

			var saveMethod = AccessTools.Method(loaderType, "Save");
			saveMethod?.Invoke(loader, null);
		}

		lastLocal = data;
		listen = true;
	}
}

