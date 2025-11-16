using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CMS21Together.ClientSide.Data.Garage.Tools;
using CMS21Together.ClientSide.Data.Handle;
using CMS21Together.ClientSide.Data.Player;
using CMS21Together.Shared.Data;
using CMS21Together.Shared.Data.Vanilla;
using CMS21Together.Shared.Data.Vanilla.Cars;
using CMS21Together.Shared.Data.Vanilla.GarageTool;
using CMS21Together.Shared.Data.Vanilla.Jobs;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace CMS21Together.ClientSide.Data.Garage.Car;

[HarmonyPatch]
public static class PartUpdateHooks
{
	public static bool listen = true;
	
	[HarmonyPatch(typeof(FluidsData), nameof(FluidsData.SetLevel))]
	[HarmonyPostfix]
	public static void SetLevelAltHook(float level, CarFluidType carFluidType, int id, FluidsData __instance)
	{
		if (!Client.Instance.isConnected || !listen) {listen = true; return;}

		if (carFluidType == CarFluidType.EngineOil && __instance.Oil.CarFluid != null)
		{
			int carLoaderID = __instance.Oil.CarFluid.GetComponentInParent<CarLoaderOnCar>().CarLoader.gameObject.name[10] - '0' - 1;
			ClientSend.CarFluid(carLoaderID, new ModFluidData(__instance.Oil));
			SyncJobProgressForCar(carLoaderID);
		}

	}
	
	[HarmonyPatch(typeof(FluidData), nameof(FluidData.SetLevel))]
	[HarmonyPostfix]
	public static void SetLevelHook(float level, FluidData __instance)
	{
		if (!Client.Instance.isConnected || !listen) {listen = true; return;}

		if (__instance != null && __instance.CarFluid != null)
		{
			int carLoaderID = __instance.CarFluid.GetComponentInParent<CarLoaderOnCar>().CarLoader.gameObject.name[10] - '0' - 1;
			ClientSend.CarFluid(carLoaderID, new ModFluidData(__instance));
			SyncJobProgressForCar(carLoaderID);
		}

	}

	[HarmonyPatch(typeof(PartScript), nameof(PartScript.DoMount))]
	[HarmonyPostfix]
	public static void DoMountHook(PartScript __instance)
	{
		if (!Client.Instance.isConnected) return;
		MelonLogger.Msg($"[PartUpdateHooks->DoMountHook] Hook triggered for part {__instance.id} (IsUnmounted={__instance.IsUnmounted})");
		MelonCoroutines.Start(HandleDoMount(__instance));
		MelonCoroutines.Start(RemoveMountedPartFromInventory(__instance));
	}

	private static bool AreBoltsMounted(PartScript partScript)
	{
		var allMounted = true;
		foreach (var bolt in partScript.MountObjects)
			if (bolt.unmounted)
			{
				allMounted = false;
				break;
			}

		return allMounted;
	}

	public static IEnumerator RemoveMountedPartFromInventory(PartScript partScript)
	{
		yield return new WaitForEndOfFrame();
		yield return new WaitForSeconds(0.5f);
		
		if (partScript == null || GameData.Instance == null || GameData.Instance.localInventory == null)
			yield break;
		
		if (partScript.IsUnmounted)
			yield break;
		
		var partID = partScript.id;
		if (string.IsNullOrEmpty(partID))
			yield break;
		
		MelonLogger.Msg($"[PartUpdateHooks->RemoveMountedPartFromInventory] Removing part {partID} from local inventory after mount.");
		
		var itemsToRemove = new List<ModItem>();
		foreach (var modItem in Player.Inventory.modItems.ToList())
		{
			if (modItem != null && modItem.ID == partID)
			{
				Item gameItem = null;
				foreach (var invItem in GameData.Instance.localInventory.items)
				{
					if (invItem != null && invItem.UID == modItem.UID && invItem.ID == partID)
					{
						gameItem = invItem;
						break;
					}
				}
				if (gameItem != null)
				{
					itemsToRemove.Add(modItem);
					break;
				}
			}
		}
		
		if (itemsToRemove.Count == 0)
		{
			var groupsToRemove = new List<ModGroupItem>();
			foreach (var modGroup in Player.Inventory.modGroupItems.ToList())
			{
				if (modGroup != null && modGroup.ItemList != null && modGroup.ItemList.Any(item => item != null && item.ID == partID))
				{
					GroupItem gameGroup = null;
					foreach (var group in GameData.Instance.localInventory.groups)
					{
						if (group != null && group.UID == modGroup.UID)
						{
							gameGroup = group;
							break;
						}
					}
					if (gameGroup != null)
					{
						var matchingItem = modGroup.ItemList.FirstOrDefault(item => item != null && item.ID == partID);
						if (matchingItem != null)
						{
							bool foundInGroup = false;
							foreach (var i in gameGroup.ItemList)
							{
								if (i != null && i.ID == partID)
								{
									foundInGroup = true;
									break;
								}
							}
							if (foundInGroup)
							{
								groupsToRemove.Add(modGroup);
								break;
							}
						}
					}
				}
			}
			
			foreach (var modGroup in groupsToRemove)
			{
				Player.Inventory.modGroupItems.Remove(modGroup);
				ClientSend.GroupItemPacket(modGroup, InventoryAction.remove);
			}
		}
		else
		{
			foreach (var modItem in itemsToRemove)
			{
				Player.Inventory.modItems.Remove(modItem);
				ClientSend.ItemPacket(modItem, InventoryAction.remove);
			}
		}
	}

	public static IEnumerator SyncPartAfterMount(PartScript partScript)
	{
		yield return new WaitForEndOfFrame();
		yield return new WaitForSeconds(0.1f);
		
		if (partScript == null || partScript.IsUnmounted)
			yield break;
			
		MelonLogger.Msg($"[PartUpdateHooks->SyncPartAfterMount] Syncing part {partScript.id} after mount.");
		
		if (partScript.GetComponentInParent<CarLoaderOnCar>())
		{
			var carLoaderID = partScript.GetComponentInParent<CarLoaderOnCar>().CarLoader.gameObject.name[10] - '0' - 1;
			var car = ClientData.Instance.loadedCars[carLoaderID];

			if (FindPartInDictionaries(car, partScript, out var partType, out var key, out var index))
				MelonCoroutines.Start(SendPartUpdate(car, carLoaderID, key, index, partType));
			else
				MelonLogger.Warning("[PartUpdateHooks->SyncPartAfterMount] PartScript not found in any dictionary.");
		}
		else
		{
			ModEngineStand stand;
			if (EngineStand.useAlt)
				stand = ClientData.Instance.engineStand2;
			else
				stand = ClientData.Instance.engineStand;
			foreach (var kvp in stand.partReferences)
			{
				if (kvp.Value == partScript)
				{
					MelonLogger.Msg($"Sending part:{partScript.id} , {kvp.Key}.");
					MelonCoroutines.Start(SendPartUpdate(null, EngineStand.useAlt ? -2 : -1, kvp.Key, null, ModPartType.engineStand));
					break;
				}
			}
		}
	}

	private static IEnumerator HandleDoMount(PartScript partScript)
	{
		if (!partScript.oneClickUnmount)
		{
			var counter = 0;
			while (!AreBoltsMounted(partScript) || counter <= 16)
			{
				yield return new WaitForSeconds(0.25f);
				counter++;
			}
		}

		MelonLogger.Msg("[PartUpdateHooks->DoMountHook] Triggered.");
		if (partScript.GetComponentInParent<CarLoaderOnCar>())
		{
			var carLoaderID = partScript.GetComponentInParent<CarLoaderOnCar>().CarLoader.gameObject.name[10] - '0' - 1;
			var car = ClientData.Instance.loadedCars[carLoaderID];

			if (FindPartInDictionaries(car, partScript, out var partType, out var key, out var index))
				MelonCoroutines.Start(SendPartUpdate(car, carLoaderID, key, index, partType));
			else
				MelonLogger.Warning("[PartUpdateHooks->DoMountHook] PartScript not found in any dictionary.");
		}
		else
		{
			ModEngineStand stand;
			if (EngineStand.useAlt)
				stand = ClientData.Instance.engineStand2;
			else
				stand = ClientData.Instance.engineStand;
			foreach (var kvp in stand.partReferences)
			{
				if (kvp.Value == partScript)
				{
					MelonLogger.Msg($"Sending part:{partScript.id} , {kvp.Key}.");
					MelonCoroutines.Start(SendPartUpdate(null, EngineStand.useAlt ? -2 : -1, kvp.Key, null, ModPartType.engineStand));
					break;
				}
			}
			
		}
	}

	[HarmonyPatch(typeof(PartScript), nameof(PartScript.Show))]
	[HarmonyPostfix]
	public static void ShowHook(PartScript __instance)
	{
		if (!Client.Instance.isConnected) return;
		
		if (!__instance.IsUnmounted)
		{
			MelonLogger.Msg($"[PartUpdateHooks->ShowHook] Part {__instance.id} is being shown (mounted). Triggering sync and inventory removal.");
			MelonCoroutines.Start(HandleDoMount(__instance));
			MelonCoroutines.Start(RemoveMountedPartFromInventory(__instance));
		}
	}

	[HarmonyPatch(typeof(PartScript), nameof(PartScript.Hide))]
	[HarmonyPostfix]
	public static void HideHook(PartScript __instance) // best way i've found to detect when a partScript is unmounted
	{
		if (!Client.Instance.isConnected) return;
		
		if (__instance.GetComponentInParent<CarLoaderOnCar>())
		{
			var carLoaderID = __instance.GetComponentInParent<CarLoaderOnCar>().CarLoader.gameObject.name[10] - '0' - 1;
			var car = ClientData.Instance.loadedCars[carLoaderID];

			if (FindPartInDictionaries(car, __instance, out var partType, out var key, out var index))
				MelonCoroutines.Start(SendPartUpdate(car, carLoaderID, key, index, partType));
		}
		else // engine stand
		{
			foreach (var kvp in ClientData.Instance.engineStand.partReferences)
			{
				if (kvp.Value == __instance)
				{
					MelonCoroutines.Start(SendPartUpdate(null, -1, kvp.Key, null, ModPartType.engineStand));
					break;
				}
			}

		}
	}

	[HarmonyPatch(typeof(CarLoader), nameof(CarLoader.TakeOffCarPart), typeof(string), typeof(bool))]
	[HarmonyPostfix]
	public static void TakeOffCarPartHook(string name, bool off, CarLoader __instance) // handle both Mount/Unmount with the boolean
	{
		if (!Client.Instance.isConnected) return;

		var carLoaderID = __instance.gameObject.name[10] - '0' - 1;
		var car = ClientData.Instance.loadedCars[carLoaderID];

		if (FindBodyPartInDictionary(car, name, out var key))
		{
			var part = car.CarPartInfo.BodyPartsReferences[key];
			MelonLogger.Msg($"[PartUpdateHooks->TakeOffCarPartHook] Body part {name} (off={off}, unmounted={part.Unmounted})");
			
			if (!off && !part.Unmounted) // Mounting (off=false means mounting)
			{
				MelonLogger.Msg($"[PartUpdateHooks->TakeOffCarPartHook] Body part {name} is being mounted. Removing from inventory.");
				MelonCoroutines.Start(RemoveMountedBodyPartFromInventory(name, carLoaderID));
			}
			
			MelonCoroutines.Start(SendBodyPart(part, key, carLoaderID));
		}
	}
	
	private static IEnumerator RemoveMountedBodyPartFromInventory(string partName, int carLoaderID)
	{
		yield return new WaitForEndOfFrame();
		yield return new WaitForSeconds(0.5f);
		
		if (GameData.Instance == null || GameData.Instance.localInventory == null)
			yield break;
		
		MelonLogger.Msg($"[PartUpdateHooks->RemoveMountedBodyPartFromInventory] Removing body part {partName} from local inventory after mount.");
		MelonLogger.Msg($"[PartUpdateHooks->RemoveMountedBodyPartFromInventory] Current modItems count: {Player.Inventory.modItems.Count}");
		foreach (var modItem in Player.Inventory.modItems)
		{
			if (modItem != null)
			{
				MelonLogger.Msg($"[PartUpdateHooks->RemoveMountedBodyPartFromInventory] Checking modItem: ID={modItem.ID}, UID={modItem.UID}, matches={modItem.ID == partName}");
			}
		}
		
		var itemsToRemove = new List<ModItem>();
		foreach (var modItem in Player.Inventory.modItems.ToList())
		{
			if (modItem != null && modItem.ID == partName)
			{
				MelonLogger.Msg($"[PartUpdateHooks->RemoveMountedBodyPartFromInventory] Found matching modItem: ID={modItem.ID}, UID={modItem.UID}");
				Item gameItem = null;
				foreach (var invItem in GameData.Instance.localInventory.items)
				{
					if (invItem != null && invItem.UID == modItem.UID && invItem.ID == partName)
					{
						gameItem = invItem;
						MelonLogger.Msg($"[PartUpdateHooks->RemoveMountedBodyPartFromInventory] Found matching game item: ID={invItem.ID}, UID={invItem.UID}");
						break;
					}
				}
				if (gameItem != null)
				{
					itemsToRemove.Add(modItem);
					break;
				}
				else
				{
					MelonLogger.Msg($"[PartUpdateHooks->RemoveMountedBodyPartFromInventory] No matching game item found for modItem {modItem.ID} (UID: {modItem.UID})");
				}
			}
		}
		
		if (itemsToRemove.Count == 0)
		{
			MelonLogger.Msg($"[PartUpdateHooks->RemoveMountedBodyPartFromInventory] No items found, checking groups. Current modGroupItems count: {Player.Inventory.modGroupItems.Count}");
			var groupsToRemove = new List<ModGroupItem>();
			foreach (var modGroup in Player.Inventory.modGroupItems.ToList())
			{
				if (modGroup != null && modGroup.ItemList != null)
				{
					bool hasMatchingItem = false;
					foreach (var item in modGroup.ItemList)
					{
						if (item != null && item.ID == partName)
						{
							hasMatchingItem = true;
							MelonLogger.Msg($"[PartUpdateHooks->RemoveMountedBodyPartFromInventory] Found matching item in group: ID={item.ID}, UID={item.UID}");
							break;
						}
					}
					
					if (hasMatchingItem)
					{
						GroupItem gameGroup = null;
						foreach (var group in GameData.Instance.localInventory.groups)
						{
							if (group != null && group.UID == modGroup.UID)
							{
								gameGroup = group;
								break;
							}
						}
						if (gameGroup != null)
						{
							bool foundInGroup = false;
							foreach (var i in gameGroup.ItemList)
							{
								if (i != null && i.ID == partName)
								{
									foundInGroup = true;
									MelonLogger.Msg($"[PartUpdateHooks->RemoveMountedBodyPartFromInventory] Found matching item in game group: ID={i.ID}, UID={i.UID}");
									break;
								}
							}
							if (foundInGroup)
							{
								groupsToRemove.Add(modGroup);
								break;
							}
						}
					}
				}
			}
			
			foreach (var modGroup in groupsToRemove)
			{
				MelonLogger.Msg($"[PartUpdateHooks->RemoveMountedBodyPartFromInventory] Removing group {modGroup.UID} from inventory.");
				Player.Inventory.modGroupItems.Remove(modGroup);
				ClientSend.GroupItemPacket(modGroup, InventoryAction.remove);
			}
		}
		else
		{
			foreach (var modItem in itemsToRemove)
			{
				MelonLogger.Msg($"[PartUpdateHooks->RemoveMountedBodyPartFromInventory] Removing item {modItem.ID} (UID: {modItem.UID}) from inventory.");
				Player.Inventory.modItems.Remove(modItem);
				ClientSend.ItemPacket(modItem, InventoryAction.remove);
			}
		}
	}

	public static bool FindPartInDictionaries(ModCar car, PartScript partScript, out ModPartType partType, out int key, out int? index)
	{
		index = null;

		foreach (var kvp in car.CarPartInfo.OtherPartsReferences)
		{
			var listIndex = kvp.Value.FindIndex(part => part == partScript);
			if (listIndex >= 0)
			{
				partType = ModPartType.other;
				key = kvp.Key;
				index = listIndex;
				return true;
			}
		}

		foreach (var kvp in car.CarPartInfo.SuspensionPartsReferences)
		{
			var listIndex = kvp.Value.FindIndex(part => part == partScript);
			if (listIndex >= 0)
			{
				partType = ModPartType.suspension;
				key = kvp.Key;
				index = listIndex;
				return true;
			}
		}

		foreach (var kvp in car.CarPartInfo.EnginePartsReferences)
			if (kvp.Value == partScript)
			{
				partType = ModPartType.engine;
				key = kvp.Key;
				return true;
			}

		foreach (var kvp in car.CarPartInfo.DriveshaftPartsReferences)
			if (kvp.Value == partScript)
			{
				partType = ModPartType.driveshaft;
				key = kvp.Key;
				return true;
			}

		partType = default;
		key = 0;
		MelonLogger.Warning("[PartUpdateHooks->FindPartInDictionaries] PartScript not found in any dictionary.");
		return false;
	}

	public static bool FindBodyPartInDictionary(ModCar car, string carPartName, out int key)
	{
		foreach (var kvp in car.CarPartInfo.BodyPartsReferences)
			if (kvp.Value.name == carPartName)
			{
				key = kvp.Key;
				return true;
			}

		key = 0;
		MelonLogger.Warning("[PartUpdateHooks->FindBodyPartInDictionary] BodyPart not found in dictionary.");
		return false;
	}

	private static IEnumerator SendPartUpdate(ModCar car, int carLoaderID, int key, int? index, ModPartType partType)
	{
		yield return new WaitForEndOfFrame();

		PartScript part;
		switch (partType)
		{
			case ModPartType.engine:
				part = car.CarPartInfo.EnginePartsReferences[key];
				break;
			case ModPartType.engineStand:
				if (carLoaderID == -1)
					part = ClientData.Instance.engineStand.partReferences[key];
				else
					part = ClientData.Instance.engineStand2.partReferences[key];
				break;
			case ModPartType.suspension:
				part = car.CarPartInfo.SuspensionPartsReferences[key][index.Value];
				break;
			case ModPartType.other:
				part = car.CarPartInfo.OtherPartsReferences[key][index.Value];
				break;
			case ModPartType.driveshaft:
				part = car.CarPartInfo.DriveshaftPartsReferences[key];
				break;
			default:
				yield break;
		}

		if (part == null)
		{
			MelonLogger.Warning($"[PartUpdateHooks->SendPartUpdate] Part is null for key={key}, index={index}, type={partType}");
			yield break;
		}

		yield return new WaitForEndOfFrame();

		var modPart = index.HasValue && partType != ModPartType.engineStand
			? new ModPartScript(part, key, index.Value, partType)
			: new ModPartScript(part, key, -1, partType);
		
		MelonLogger.Msg($"[PartUpdateHooks->SendPartUpdate] Sending part {modPart.id} (unmounted={modPart.unmounted}) to server for carLoader {carLoaderID}");
		
		ClientSend.PartScriptPacket(modPart, carLoaderID);
	}

	public static IEnumerator SendBodyPart(CarPart part, int key, int carLoaderID)
	{
		yield return new WaitForEndOfFrame();
		yield return new WaitForEndOfFrame();

		ClientSend.BodyPartPacket(new ModCarPart(part, key, carLoaderID), carLoaderID);
	}

	private static void SyncJobProgressForCar(int carLoaderID)
	{
		if (!Client.Instance.isConnected)
			return;

		if (GameData.Instance == null || GameData.Instance.orderGenerator == null)
			return;

		foreach (var job in GameData.Instance.orderGenerator.selectedJobs)
		{
			if (job != null && job.carLoaderID == carLoaderID)
			{
				ClientSend.JobUpdatePacket(new ModJob(job));
				break;
			}
		}
	}
}