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
        
        [HarmonyPatch(typeof(WheelBalanceWindow), nameof(WheelBalanceWindow.StartMiniGame))]
        [HarmonyPostfix]
        public static void WheelBalancer2Fix(WheelBalanceWindow __instance)
        {
            if(!Client.Instance.isConnected) return;
            
            MelonCoroutines.Start(MonitorWheelBalance());
        }
        
        private static IEnumerator MonitorWheelBalance()
        {
            yield return new WaitForSeconds(0.5f);
            
            while (GameData.Instance != null && GameData.Instance.wheelBalancer != null && 
                   GameData.Instance.wheelBalancer.groupOnWheelBalancer != null)
            {
                bool allBalanced = true;
                foreach (var item in GameData.Instance.wheelBalancer.groupOnWheelBalancer.ItemList)
                {
                    if (item != null)
                    {
                        try
                        {
                            if (!item.WheelData.IsBalanced)
                            {
                                allBalanced = false;
                                break;
                            }
                        }
                        catch
                        {
                            allBalanced = false;
                            break;
                        }
                    }
                }
                
                if (allBalanced)
                {
                    ClientSend.SendWheelBalancer(1, GameData.Instance.wheelBalancer.groupOnWheelBalancer);
                    yield break;
                }
                
                yield return new WaitForSeconds(0.1f);
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