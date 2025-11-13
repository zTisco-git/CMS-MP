using System.Collections;
using CMS;
using CMS21Together.ClientSide.Data.Handle;
using CMS21Together.Shared.Data.Vanilla;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace CMS21Together.ClientSide.Data.Garage;

[HarmonyPatch]
public static class RadioSync
{
	private static bool listen = true;
	private static ModRadioData lastLocal;
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

		var current = GetCurrentRadioData();
		if (current == null)
			return;

		if (lastLocal != null && current.Equals(lastLocal))
			return;

		if (!listen)
		{
			lastLocal = current;
			return;
		}

		lastLocal = current;
		ClientSend.RadioPacket(current);
	}

	private static ModRadioData GetCurrentRadioData()
	{
		try
		{
			var profile = Singleton<GameManager>.Instance.GameDataManager?.CurrentProfileData;
			if (profile == null || profile.jukeboxData == null)
				return null;

			var radioData = profile.jukeboxData;
			
			var radioType = radioData.GetType();
			var currentTrackIndex = 0;
			var isPlaying = false;
			var volume = 1.0f;
			var isEnabled = false;
			var currentTrackProp = AccessTools.Property(radioType, "currentTrackIndex");
			if (currentTrackProp != null)
				currentTrackIndex = (int)currentTrackProp.GetValue(radioData);

			var isPlayingProp = AccessTools.Property(radioType, "isPlaying");
			if (isPlayingProp != null)
				isPlaying = (bool)isPlayingProp.GetValue(radioData);

			var volumeProp = AccessTools.Property(radioType, "volume");
			if (volumeProp != null)
				volume = (float)volumeProp.GetValue(radioData);

			var isEnabledProp = AccessTools.Property(radioType, "isEnabled");
			if (isEnabledProp != null)
				isEnabled = (bool)isEnabledProp.GetValue(radioData);

			return new ModRadioData(currentTrackIndex, isPlaying, volume, isEnabled);
		}
		catch (System.Exception ex)
		{
			MelonLogger.Warning($"[RadioSync->GetCurrentRadioData] Error getting radio data: {ex.Message}");
			return null;
		}
	}

	public static void Apply(ModRadioData data)
	{
		if (data == null)
			return;

		MelonCoroutines.Start(ApplyRoutine(data));
	}

	private static IEnumerator ApplyRoutine(ModRadioData data)
	{
		listen = false;

		while (!ClientData.GameReady)
			yield return new WaitForSeconds(0.25f);

		yield return new WaitForEndOfFrame();

		try
		{
			var profile = Singleton<GameManager>.Instance.GameDataManager?.CurrentProfileData;
			if (profile == null || profile.jukeboxData == null)
			{
				MelonLogger.Warning("[RadioSync->Apply] Profile or jukeboxData is null");
				lastLocal = data;
				listen = true;
				yield break;
			}

			var radioData = profile.jukeboxData;
			var radioType = radioData.GetType();

			var currentTrackProp = AccessTools.Property(radioType, "currentTrackIndex");
			if (currentTrackProp != null)
				currentTrackProp.SetValue(radioData, data.currentTrackIndex);

			var isPlayingProp = AccessTools.Property(radioType, "isPlaying");
			if (isPlayingProp != null)
				isPlayingProp.SetValue(radioData, data.isPlaying);

			var volumeProp = AccessTools.Property(radioType, "volume");
			if (volumeProp != null)
				volumeProp.SetValue(radioData, data.volume);

			var isEnabledProp = AccessTools.Property(radioType, "isEnabled");
			if (isEnabledProp != null)
				isEnabledProp.SetValue(radioData, data.isEnabled);

			var updateMethod = AccessTools.Method(radioType, "UpdateRadio");
			if (updateMethod != null)
				updateMethod.Invoke(radioData, null);

			MelonLogger.Msg($"[RadioSync->Apply] Applied radio: Track={data.currentTrackIndex}, Playing={data.isPlaying}, Volume={data.volume}");
		}
		catch (System.Exception ex)
		{
			MelonLogger.Error($"[RadioSync->Apply] Error applying radio data: {ex.Message}");
		}

		lastLocal = data;
		listen = true;
	}
}

