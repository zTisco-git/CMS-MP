using System.Collections;
using System.Linq;
using CMS21Together.ClientSide.Data.Handle;
using CMS21Together.ClientSide.Data.Player;
using CMS21Together.Shared.Data;
using CMS21Together.Shared.Data.Vanilla;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

using TireC= TireChangerLogic;

namespace CMS21Together.ClientSide.Data.Garage.Tools;

[HarmonyPatch]
public static class TireChangerLogic
{
	public static bool listen = true;

	[HarmonyPatch(typeof(TireC), nameof(TireC.SetGroupOnTireChanger), typeof(GroupItem), typeof(bool), typeof(bool))]
	[HarmonyPostfix]
	public static void TireChangerFix(GroupItem groupItem, bool instant, bool connect, TireC __instance)
	{
		if (!Client.Instance.isConnected || !listen)
		{
			listen = true;
			return;
		}

		if (groupItem == null || groupItem.ItemList.Count == 0) return;

		ClientSend.SetTireChangerPacket(new ModGroupItem(groupItem), instant, connect);
		
		if (connect)
		{
			MelonCoroutines.Start(RemoveTireItemsFromInventory(groupItem));
		}
	}

	private static IEnumerator RemoveTireItemsFromInventory(GroupItem groupItem)
	{
		yield return new WaitForEndOfFrame();
		yield return new WaitForSeconds(0.5f);
		
		if (groupItem != null && GameData.Instance != null && GameData.Instance.localInventory != null)
		{
			foreach (var item in groupItem.ItemList)
			{
				if (item != null && Player.Inventory.modItems.Any(i => i.UID == item.UID))
				{
					var modItem = Player.Inventory.modItems.First(i => i.UID == item.UID);
					Player.Inventory.modItems.Remove(modItem);
					ClientSend.ItemPacket(modItem, InventoryAction.remove);
				}
			}
			
			if (Player.Inventory.modGroupItems.Any(i => i.UID == groupItem.UID))
			{
				var modGroup = Player.Inventory.modGroupItems.First(i => i.UID == groupItem.UID);
				Player.Inventory.modGroupItems.Remove(modGroup);
				ClientSend.GroupItemPacket(modGroup, InventoryAction.remove);
			}
		}
	}

	[HarmonyPatch(typeof(PieMenuController), "_GetOnClick_b__72_61")]
	[HarmonyPostfix]
	public static void TireRemoveActionFix()
	{
		if (!Client.Instance.isConnected || !listen)
		{
			listen = true;
			return;
		}

		ClientSend.ClearTireChangerPacket();
	}
}