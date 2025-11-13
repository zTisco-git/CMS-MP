using System.Collections;
using CMS.UI.Windows;
using CMS21Together.ClientSide.Data.Handle;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace CMS21Together.ClientSide.Data.Garage.Tools;

[HarmonyPatch]
public static class WheelBalancer
{
	public static bool listen = true;
	
        [HarmonyPrefix]
	    [HarmonyPatch(typeof(WheelBalancerLogic), "SetGroupOnWheelBalancer")]
        public static void WheelBalancerFix(GroupItem groupItem, bool instant, WheelBalancerLogic __instance)
        {
            if(!Client.Instance.isConnected) return;
            if(!listen) { listen = true; return;}
            
            if (groupItem != null && groupItem.ItemList.Count != 0)
            {
                ClientSend.SendWheelBalancer(0, groupItem);
            }
        }
        
        [HarmonyPatch(typeof(WheelBalanceWindow), nameof(WheelBalanceWindow.OnMiniGameFinished))]
        [HarmonyPostfix]
        public static void WheelBalancer2Fix(WheelBalanceWindow __instance)
        {
            if(!Client.Instance.isConnected) return;
            
            if (GameData.Instance.wheelBalancer.groupOnWheelBalancer != null)
            {
                ClientSend.SendWheelBalancer(1, GameData.Instance.wheelBalancer.groupOnWheelBalancer);
            }
        }
        
        [HarmonyPatch(typeof(PieMenuController), "_GetOnClick_b__72_64")]
        [HarmonyPostfix]
        public static void WB_TireRemoveActionFix()
        {
            if(!Client.Instance.isConnected) return;
            
            ClientSend.SendWheelBalancer(2);
        }
}