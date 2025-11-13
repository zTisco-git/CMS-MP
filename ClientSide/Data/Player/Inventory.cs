using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CMS.Helpers;
using CMS21Together.ClientSide.Data.Handle;
using CMS21Together.ServerSide;
using CMS21Together.Shared.Data.Vanilla;
using HarmonyLib;
using MelonLoader;

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

		if (modItems.Any(s => s.UID == item.UID))
		{
			var itemToRemove = modItems.First(s => s.UID == item.UID);
			ClientSend.ItemPacket(itemToRemove, InventoryAction.remove);
			modItems.Remove(itemToRemove);
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

		if (string.IsNullOrEmpty(partID))
			yield break;

		var itemsToRemove = modItems.Where(i => i.ID == partID).ToList();
		foreach (var modItem in itemsToRemove)
		{
			modItems.Remove(modItem);
			Item itemToDelete = null;
			foreach (var invItem in GameData.Instance.localInventory.items)
			{
				if (invItem != null && invItem.ID == partID && invItem.UID == modItem.UID)
				{
					itemToDelete = invItem;
					break;
				}
			}
			if (itemToDelete != null)
			{
				GameData.Instance.localInventory.Delete(itemToDelete);
			}
		}
	}

	public static IEnumerator RemoveGroupItemByPartID(string partID)
	{
		yield return GameData.GameReady();

		if (string.IsNullOrEmpty(partID))
			yield break;

		var groupsToRemove = modGroupItems.Where(g => 
			g.ItemList != null && g.ItemList.Any(item => item != null && item.ID == partID)).ToList();

		foreach (var modGroup in groupsToRemove)
		{
			modGroupItems.Remove(modGroup);
			GroupItem groupToDelete = null;
			foreach (var group in GameData.Instance.localInventory.groups)
			{
				if (group != null && group.UID == modGroup.UID)
				{
					groupToDelete = group;
					break;
				}
			}
			if (groupToDelete != null)
			{
				GameData.Instance.localInventory.DeleteGroup(modGroup.UID);
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