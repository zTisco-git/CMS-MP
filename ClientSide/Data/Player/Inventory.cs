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
	private static bool isHandlingRemoteRemove = false; // Flag to prevent RemoveItemHook from sending packets when HandleItem calls Delete

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
		
		if (modItems.Any(i => i.UID == item.UID))
		{
			MelonLogger.Msg($"[Inventory->AddItemHook] Item {item.ID} (UID: {item.UID}) already exists in modItems, skipping.");
			return;
		}
		
		MelonLogger.Msg($"[Inventory->AddItemHook] Adding item to inventory: ID={item.ID}, UID={item.UID}");
		var newItem = new ModItem(item);
		if (newItem == null)
		{
			MelonLogger.Warning($"[Inventory->AddItemHook] Failed to create ModItem from Item {item.ID} (UID: {item.UID})");
			return;
		}
		modItems.Add(newItem);
		MelonLogger.Msg($"[Inventory->AddItemHook] Sending item {newItem.ID} (UID: {newItem.UID}) to server.");
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
		
		// Don't send packet if this is a remote remove (from HandleItem)
		if (isHandlingRemoteRemove)
		{
			MelonLogger.Msg($"[Inventory->RemoveItemHook] Item {item.ID} (UID: {item.UID}) is being deleted as part of remote remove, skipping packet send.");
			return;
		}

		MelonLogger.Msg($"[Inventory->RemoveItemHook] Item {item.ID} (UID: {item.UID}) is being deleted from inventory. Checking if part is being mounted...");
		
		// Remove from modItems if it exists
		if (modItems.Any(s => s.UID == item.UID))
		{
			var itemToRemove = modItems.First(s => s.UID == item.UID);
			modItems.Remove(itemToRemove);
		}
		
		// Always send remove packet to server, even if item wasn't in modItems
		// This ensures that items added by other players are also removed from server
		var modItemToRemove = new ModItem(item);
		MelonLogger.Msg($"[Inventory->RemoveItemHook] Sending remove packet for item {item.ID} (UID: {item.UID}) to server.");
		ClientSend.ItemPacket(modItemToRemove, InventoryAction.remove);
		
		MelonCoroutines.Start(CheckIfPartMounted(item.ID));
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
		
		// Don't send packet if this is a remote remove (from HandleGroupItem)
		if (isHandlingRemoteRemove)
		{
			MelonLogger.Msg($"[Inventory->RemoveGroupItemHook] Group with UID {UId} is being deleted as part of remote remove, skipping packet send.");
			return;
		}

		// Remove from modGroupItems if it exists
		if (modGroupItems.Any(s => s.UID == UId))
		{
			var itemToRemove = modGroupItems.First(s => s.UID == UId);
			modGroupItems.Remove(itemToRemove);
		}
		

		GroupItem gameGroup = null;
		if (GameData.Instance != null && GameData.Instance.localInventory != null)
		{
			foreach (var group in GameData.Instance.localInventory.groups)
			{
				if (group != null && group.UID == UId)
				{
					gameGroup = group;
					break;
				}
			}
		}
		
		if (gameGroup != null)
		{
			var modGroupToRemove = new ModGroupItem(gameGroup);
			MelonLogger.Msg($"[Inventory->RemoveGroupItemHook] Sending remove packet for group {gameGroup.ID} (UID: {UId}) to server.");
			ClientSend.GroupItemPacket(modGroupToRemove, InventoryAction.remove);
		}
		else
		{
	
			ModGroupItem existingModGroup = null;
			foreach (var group in modGroupItems)
			{
				if (group != null && group.UID == UId)
				{
					existingModGroup = group;
					break;
				}
			}
			
			if (existingModGroup != null)
			{
				MelonLogger.Msg($"[Inventory->RemoveGroupItemHook] Sending remove packet for group {existingModGroup.ID} (UID: {UId}) to server (from modGroupItems).");
				ClientSend.GroupItemPacket(existingModGroup, InventoryAction.remove);
			}
			else
			{
				MelonLogger.Warning($"[Inventory->RemoveGroupItemHook] Group with UID {UId} not found in game inventory or modGroupItems. Cannot send remove packet.");
			}
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
				if (gameItem == null)
				{
					MelonLogger.Warning($"[Inventory->HandleItem] Failed to convert ModItem {item.ID} (UID: {item.UID}) to game item.");
					yield break;
				}
				
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
					MelonLogger.Msg($"[Inventory->HandleItem] Item {item.ID} (UID: {item.UID}) already exists in game inventory, removing old one.");
					GameData.Instance.localInventory.Delete(existingItem);
				}
				
				MelonLogger.Msg($"[Inventory->HandleItem] Adding item {item.ID} (UID: {item.UID}) to game inventory.");
				GameData.Instance.localInventory.Add(gameItem);
				MelonLogger.Msg($"[Inventory->HandleItem] Item {item.ID} (UID: {item.UID}) added successfully. Total items in modItems: {modItems.Count}, game inventory count: {GameData.Instance.localInventory.items.Count}");
				break;
			case InventoryAction.remove:
				if (item == null) yield break;
				MelonLogger.Msg($"[Inventory->HandleItem] Removing item {item.ID} (UID: {item.UID}) from inventory.");
				
				var removedFromModItems = modItems.RemoveAll(i => i.UID == item.UID);
				if (removedFromModItems > 0)
				{
					MelonLogger.Msg($"[Inventory->HandleItem] Removed {removedFromModItems} item(s) from modItems.");
				}
				
		
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
					MelonLogger.Msg($"[Inventory->HandleItem] Deleting item {itemToDelete.ID} (UID: {itemToDelete.UID}) from game inventory.");
					isHandlingRemoteRemove = true; // Prevent RemoveItemHook from sending packet
					GameData.Instance.localInventory.Delete(itemToDelete);
					isHandlingRemoteRemove = false;
					MelonLogger.Msg($"[Inventory->HandleItem] Item {item.ID} (UID: {item.UID}) removed successfully. Total items in modItems: {modItems.Count}, game inventory count: {GameData.Instance.localInventory.items.Count}");
				}
				else
				{
					MelonLogger.Msg($"[Inventory->HandleItem] Item {item.ID} (UID: {item.UID}) not found in game inventory. It may have already been removed.");
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
				MelonLogger.Msg($"[Inventory->HandleGroupItem] Removing group {item.ID} (UID: {item.UID}) from inventory.");
				
				var removedFromModGroupItems = modGroupItems.RemoveAll(i => i.UID == item.UID);
				if (removedFromModGroupItems > 0)
				{
					MelonLogger.Msg($"[Inventory->HandleGroupItem] Removed {removedFromModGroupItems} group(s) from modGroupItems.");
				}
				
			
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
					MelonLogger.Msg($"[Inventory->HandleGroupItem] Deleting group {groupToDelete.ID} (UID: {groupToDelete.UID}) from game inventory.");
					isHandlingRemoteRemove = true; // Prevent RemoveGroupItemHook from sending packet
					GameData.Instance.localInventory.DeleteGroup(item.UID);
					isHandlingRemoteRemove = false;
					MelonLogger.Msg($"[Inventory->HandleGroupItem] Group {item.ID} (UID: {item.UID}) removed successfully. Total groups in modGroupItems: {modGroupItems.Count}, game inventory groups count: {GameData.Instance.localInventory.groups.Count}");
				}
				else
				{
					MelonLogger.Msg($"[Inventory->HandleGroupItem] Group {item.ID} (UID: {item.UID}) not found in game inventory. It may have already been removed.");
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