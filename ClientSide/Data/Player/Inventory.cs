using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CMS.Helpers;
using CMS21Together.ClientSide.Data.Garage.Car;
using CMS21Together.ClientSide.Data.Handle;
using CMS21Together.ServerSide;
using CMS21Together.Shared.Data.Vanilla;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace CMS21Together.ClientSide.Data.Player;

[HarmonyPatch]
public static class Inventory
{
	public static List<ModItem> modItems = new();
	public static List<ModGroupItem> modGroupItems = new();
	private static bool loadSkip;

	public static void Reset()
	{
		modItems.Clear();
		modGroupItems.Clear();
		loadSkip = false;
		
	}

	[HarmonyPatch(typeof(UIHelper), nameof(UIHelper.GetItemsForID))]
	[HarmonyPrefix]
	public static bool GetItemsForIDFix(Il2CppSystem.Collections.Generic.List<Item> items,
		string id, ref Il2CppSystem.Collections.Generic.List<BaseItem> __result)
	{
		if (!Client.Instance.isConnected)
			return true;

		var array = items.ToArray();
		var snapshot = new List<string>();
		for (int i = 0; i < array.Count; i++)
			snapshot.Add(array[i].ID);
		
		var matches = new ConcurrentBag<int>();
		Parallel.For(0, snapshot.Count, i =>
		{
			if (snapshot[i].IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0)
				matches.Add(i);
		});
		
		var newRes = new Il2CppSystem.Collections.Generic.List<BaseItem>();
		foreach (int index in matches)
			newRes.Add(array[index]);

		__result = newRes;
		return false;
	}
	
	[HarmonyPatch(typeof(UIHelper), nameof(UIHelper.GetBaseItemsForIDExact))]
	[HarmonyPrefix]
	public static bool GetBaseItemsForIDExactFix(Il2CppSystem.Collections.Generic.List<Item> items,
		string id, ref Il2CppSystem.Collections.Generic.List<BaseItem> __result)
	{
		if (!Client.Instance.isConnected) {return true;}

		var array = items.ToArray();
		var snapshotIds = new string[items.Count];
		for (int i = 0; i < array.Count; i++)
			snapshotIds[i] = array[i]?.ID;
		
		var matchedIndices = new ConcurrentBag<int>();
		Parallel.For(0, snapshotIds.Length, i =>
		{
			var idValue = snapshotIds[i];
			if (idValue != null && idValue == id)
				matchedIndices.Add(i);
		});
		
		var resultList = new Il2CppSystem.Collections.Generic.List<BaseItem>();
		foreach (int index in matchedIndices)
		{
			var item = array[index];
			if (item != null)
				resultList.Add(item);
		}

		__result = resultList;
		
		return false;
	}


	[HarmonyPatch(typeof(global::Inventory), "Add", typeof(Item), typeof(bool))]
	[HarmonyPrefix]
	public static void AddItemHook(Item item, bool showPopup = false)
	{
		if (!Client.Instance.isConnected) {return;}
		if (modItems.Any(i => i.UID == item.UID)) return;
		
		MelonLogger.Msg($"[Inventory->AddItemHook] Adding item to inventory: ID={item.ID}, UID={item.UID}");
		var newItem = new ModItem(item);
		modItems.Add(newItem);
		ClientSend.ItemPacket(newItem, InventoryAction.add);
	}

	[HarmonyPatch(typeof(global::Inventory), "AddGroup")]
	[HarmonyPrefix]
	public static void AddGroupItemHook(GroupItem group)
	{
		if (!Client.Instance.isConnected) {return;}
		if (modGroupItems.Any(i => i.UID == group.UID)) return;

		var newItem = new ModGroupItem(group);
		modGroupItems.Add(newItem);
		ClientSend.GroupItemPacket(newItem, InventoryAction.add);
	}

	[HarmonyPatch(typeof(global::Inventory), "Delete")]
	[HarmonyPrefix]
	public static void RemoveItemHook(Item item, global::Inventory __instance)
	{
		if (!Client.Instance.isConnected) {return;}

		if (item == null) return;

		MelonLogger.Msg($"[Inventory->RemoveItemHook] Item {item.ID} (UID: {item.UID}) is being deleted from inventory. Checking if part is being mounted...");
		
		if (modItems.Any(s => s.UID == item.UID))
		{
			var itemToRemove = modItems.First(s => s.UID == item.UID);
			ClientSend.ItemPacket(itemToRemove, InventoryAction.remove);
			modItems.Remove(itemToRemove);
			
			MelonCoroutines.Start(CheckIfPartMounted(item.ID));
		}
	}
	
	private static IEnumerator CheckIfPartMounted(string partID)
	{
		yield return new WaitForEndOfFrame();
		yield return new WaitForSeconds(0.5f);
		
		if (GameData.Instance == null || GameData.Instance.carLoaders == null)
			yield break;
		
		for (int i = 0; i < GameData.Instance.carLoaders.Length; i++)
		{
			var carLoader = GameData.Instance.carLoaders[i];
			if (carLoader == null) continue;
			
			var allParts = new List<PartScript>();
			allParts.AddRange(carLoader.GetComponentsInChildren<PartScript>());
			
			foreach (var part in allParts)
			{
				if (part != null && part.id == partID && !part.IsUnmounted)
				{
					MelonLogger.Msg($"[Inventory->CheckIfPartMounted] Part {partID} is mounted on carLoader {i}. Triggering sync and inventory removal.");
					MelonCoroutines.Start(PartUpdateHooks.SyncPartAfterMount(part));
					MelonCoroutines.Start(PartUpdateHooks.RemoveMountedPartFromInventory(part));
					yield break;
				}
			}
		}
	}

	[HarmonyPatch(typeof(global::Inventory), "DeleteGroup")]
	[HarmonyPrefix]
	public static void RemoveGroupItemHook(long UId)
	{
		if (!Client.Instance.isConnected ) {return;}

		if (modGroupItems.Any(s => s.UID == UId))
		{
			var itemToRemove = modGroupItems.First(s => s.UID == UId);
			ClientSend.GroupItemPacket(itemToRemove, InventoryAction.remove);
			modGroupItems.Remove(itemToRemove);
		}
	}

	[HarmonyPatch(typeof(global::Inventory), "Load")]
	[HarmonyPrefix]
	public static bool LoadHook(global::Inventory __instance)
	{
		if (!Client.Instance.isConnected) return true;

		if (!Server.Instance.isRunning)
		{
			if (loadSkip)
			{
				ClientSend.ItemPacket(null, InventoryAction.resync);
				ClientSend.GroupItemPacket(null, InventoryAction.resync);
			}
			else
			{
				loadSkip = true;
				return false;
			}
		}

		var inventoryData = Singleton<GameManager>.Instance.GameDataManager.CurrentProfileData.inventoryData;
		foreach (var group in inventoryData.groups)
			if (group != null)
			{
				var newItem = new ModGroupItem(group);
				modGroupItems.Add(newItem);
				ClientSend.GroupItemPacket(newItem, InventoryAction.add);
			}

		MelonLogger.Msg($"[Inventory->LoadHook] Loaded {modGroupItems.Count} groupItem.");

		foreach (var item in inventoryData.items)
			if (item != null)
			{
				var newItem = new ModItem(item);
				modItems.Add(newItem);
				ClientSend.ItemPacket(newItem, InventoryAction.add);
			}

		MelonLogger.Msg($"[Inventory->LoadHook] Loaded {modItems.Count} Item.");
		return true;
	}

	public static IEnumerator HandleItem(ModItem item, InventoryAction action)
	{
		yield return GameData.GameReady();

		switch (action)
		{
			case InventoryAction.add:
				if (item == null) yield break;
				MelonLogger.Msg($"[Inventory->HandleItem] Adding item {item.ID} (UID: {item.UID}) to inventory.");
				modItems.RemoveAll(i => i.UID == item.UID);
				modItems.Add(item);
				
				var gameItem = item.ToGame();
				Item existingItem = null;
				foreach (var invItem in GameData.Instance.localInventory.items)
				{
					if (invItem != null && invItem.UID == item.UID)
					{
						existingItem = invItem;
						break;
					}
				}
				if (existingItem != null)
				{
					GameData.Instance.localInventory.Delete(existingItem);
				}
				GameData.Instance.localInventory.Add(gameItem);
				MelonLogger.Msg($"[Inventory->HandleItem] Item {item.ID} (UID: {item.UID}) added successfully. Total items in modItems: {modItems.Count}");
				break;
			case InventoryAction.remove:
				if (item == null) yield break;
				modItems.RemoveAll(i => i.UID == item.UID);
				Item itemToDelete = null;
				foreach (var invItem in GameData.Instance.localInventory.items)
				{
					if (invItem != null && invItem.UID == item.UID)
					{
						itemToDelete = invItem;
						break;
					}
				}
				if (itemToDelete != null)
				{
					GameData.Instance.localInventory.Delete(itemToDelete);
				}
				break;
		}
	}

	public static IEnumerator HandleGroupItem(ModGroupItem item, InventoryAction action)
	{
		yield return GameData.GameReady();
		switch (action)
		{
			case InventoryAction.add:
				if (item == null) yield break;
				modGroupItems.RemoveAll(i => i.UID == item.UID);
				
				GroupItem existingGroup = null;
				foreach (var group in GameData.Instance.localInventory.groups)
				{
					if (group != null && group.UID == item.UID)
					{
						existingGroup = group;
						break;
					}
				}
				if (existingGroup != null)
				{
					GameData.Instance.localInventory.DeleteGroup(item.UID);
				}
				modGroupItems.Add(item);
				GameData.Instance.localInventory.AddGroup(item.ToGame());
				break;
			case InventoryAction.remove:
				if (item == null) yield break;
				modGroupItems.RemoveAll(i => i.UID == item.UID);
				GroupItem groupToDelete = null;
				foreach (var group in GameData.Instance.localInventory.groups)
				{
					if (group != null && group.UID == item.UID)
					{
						groupToDelete = group;
						break;
					}
				}
				if (groupToDelete != null)
				{
					GameData.Instance.localInventory.DeleteGroup(item.UID);
				}
				break;
		}
	}

	public static IEnumerator RemoveItemByID(string partID)
	{
		yield return GameData.GameReady();

		if (string.IsNullOrEmpty(partID) || GameData.Instance == null || GameData.Instance.localInventory == null)
			yield break;

		MelonLogger.Msg($"[Inventory->RemoveItemByID] Removing items with ID {partID} from inventory.");
		MelonLogger.Msg($"[Inventory->RemoveItemByID] Current modItems count: {modItems.Count}");
		foreach (var modItem in modItems)
		{
			if (modItem != null)
			{
				MelonLogger.Msg($"[Inventory->RemoveItemByID] Checking item: ID={modItem.ID}, UID={modItem.UID}");
			}
		}

		var itemsToRemove = new List<ModItem>();
		foreach (var modItem in modItems)
		{
			if (modItem != null)
			{
				if (modItem.ID == partID)
				{
					itemsToRemove.Add(modItem);
					MelonLogger.Msg($"[Inventory->RemoveItemByID] Found exact match: ID={modItem.ID}");
				}
				else if (modItem.ID.EndsWith("-" + partID) || modItem.ID.EndsWith("_" + partID))
				{
					itemsToRemove.Add(modItem);
					MelonLogger.Msg($"[Inventory->RemoveItemByID] Found match with prefix: ID={modItem.ID} ends with -{partID} or _{partID}");
				}
			}
		}
		MelonLogger.Msg($"[Inventory->RemoveItemByID] Found {itemsToRemove.Count} items in modItems to remove.");
		
		foreach (var modItem in itemsToRemove)
		{
			MelonLogger.Msg($"[Inventory->RemoveItemByID] Removing item {modItem.ID} (UID: {modItem.UID}) from modItems.");
			modItems.Remove(modItem);
		}

		var itemsToDeleteFromGame = new List<Item>();
		MelonLogger.Msg($"[Inventory->RemoveItemByID] Checking game inventory. Total items: {GameData.Instance.localInventory.items.Count}");
		foreach (var invItem in GameData.Instance.localInventory.items)
		{
			if (invItem != null)
			{
				MelonLogger.Msg($"[Inventory->RemoveItemByID] Checking game item: ID={invItem.ID}, UID={invItem.UID}");
				// Check exact match first
				if (invItem.ID == partID)
				{
					itemsToDeleteFromGame.Add(invItem);
					MelonLogger.Msg($"[Inventory->RemoveItemByID] Found exact match in game inventory: ID={invItem.ID}");
				}
				// Check if the item ID ends with the partID (for body parts with car prefix)
				else if (invItem.ID.EndsWith("-" + partID) || invItem.ID.EndsWith("_" + partID))
				{
					itemsToDeleteFromGame.Add(invItem);
					MelonLogger.Msg($"[Inventory->RemoveItemByID] Found match with prefix in game inventory: ID={invItem.ID} ends with -{partID} or _{partID}");
				}
			}
		}

		MelonLogger.Msg($"[Inventory->RemoveItemByID] Found {itemsToDeleteFromGame.Count} items in game inventory to delete.");

		foreach (var itemToDelete in itemsToDeleteFromGame)
		{
			MelonLogger.Msg($"[Inventory->RemoveItemByID] Deleting game item {itemToDelete.ID} (UID: {itemToDelete.UID}) from game inventory.");
			GameData.Instance.localInventory.Delete(itemToDelete);
		}
	}

	public static IEnumerator RemoveGroupItemByPartID(string partID)
	{
		yield return GameData.GameReady();

		if (string.IsNullOrEmpty(partID) || GameData.Instance == null || GameData.Instance.localInventory == null)
			yield break;

		MelonLogger.Msg($"[Inventory->RemoveGroupItemByPartID] Removing groups with part ID {partID} from inventory.");

		var groupsToRemove = new List<ModGroupItem>();
		foreach (var modGroup in modGroupItems)
		{
			if (modGroup != null && modGroup.ItemList != null)
			{
				foreach (var item in modGroup.ItemList)
				{
					if (item != null)
					{
						// Check exact match first
						if (item.ID == partID)
						{
							groupsToRemove.Add(modGroup);
							MelonLogger.Msg($"[Inventory->RemoveGroupItemByPartID] Found exact match in group: ID={item.ID}");
							break;
						}
						// Check if the item ID ends with the partID (for body parts with car prefix)
						else if (item.ID.EndsWith("-" + partID) || item.ID.EndsWith("_" + partID))
						{
							groupsToRemove.Add(modGroup);
							MelonLogger.Msg($"[Inventory->RemoveGroupItemByPartID] Found match with prefix in group: ID={item.ID} ends with -{partID} or _{partID}");
							break;
						}
					}
				}
			}
		}

		MelonLogger.Msg($"[Inventory->RemoveGroupItemByPartID] Found {groupsToRemove.Count} groups in modGroupItems to remove.");

		foreach (var modGroup in groupsToRemove)
		{
			modGroupItems.Remove(modGroup);
		}

		var groupsToDeleteFromGame = new List<GroupItem>();
		foreach (var group in GameData.Instance.localInventory.groups)
		{
			if (group != null && group.ItemList != null)
			{
				foreach (var item in group.ItemList)
				{
					if (item != null)
					{
						// Check exact match first
						if (item.ID == partID)
						{
							groupsToDeleteFromGame.Add(group);
							MelonLogger.Msg($"[Inventory->RemoveGroupItemByPartID] Found exact match in game group: ID={item.ID}");
							break;
						}
						// Check if the item ID ends with the partID (for body parts with car prefix)
						else if (item.ID.EndsWith("-" + partID) || item.ID.EndsWith("_" + partID))
						{
							groupsToDeleteFromGame.Add(group);
							MelonLogger.Msg($"[Inventory->RemoveGroupItemByPartID] Found match with prefix in game group: ID={item.ID} ends with -{partID} or _{partID}");
							break;
						}
					}
				}
			}
		}

		MelonLogger.Msg($"[Inventory->RemoveGroupItemByPartID] Found {groupsToDeleteFromGame.Count} groups in game inventory to delete.");

		foreach (var groupToDelete in groupsToDeleteFromGame)
		{
			if (groupToDelete != null)
			{
				GameData.Instance.localInventory.DeleteGroup(groupToDelete.UID);
			}
		}
	}
}

[Serializable]
public enum InventoryAction
{
	add,
	remove,
	resync
}