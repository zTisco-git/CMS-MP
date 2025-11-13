using System.Collections;
using CMS21Together.ClientSide.Data.Handle;
using HarmonyLib;
using Il2Cpp;
using Il2CppCMS.UI.Windows;
using MelonLoader;
using UnityEngine;

namespace CMS21Together.ClientSide.Data.Garage.Tools;

[HarmonyPatch]
public static class WheelBalancerLogic
{
    public static bool listen = true;


    [HarmonyPatch(typeof(Il2Cpp.WheelBalancerLogic), nameof(Il2Cpp.WheelBalancerLogic.SetGroupOnWheelBalancer))]
    [HarmonyPrefix]
    public static void SetGroupOnWheelBalancerHook(GroupItem groupItem, bool instant, Il2Cpp.WheelBalancerLogic __instance)
    {
        if(!Client.Instance.isConnected || !listen) {listen = true; return;}
        if(groupItem == null || groupItem.ItemList.Count == 0) return;

        ClientSend.SetWheelBalancerPacket(groupItem);
    }
    
    [HarmonyPatch(typeof(WheelBalanceWindow), nameof(WheelBalanceWindow.StartMiniGame))]
    [HarmonyPostfix]
    public static void StartMiniGameHook(WheelBalanceWindow __instance)
    {
        if(!Client.Instance.isConnected || !listen) {listen = true; return;}
        
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
                if (item != null && item.WheelData != null && !item.WheelData.IsBalanced)
                {
                    allBalanced = false;
                    break;
                }
            }
            
            if (allBalanced)
            {
                ClientSend.WheelBalancePacket(GameData.Instance.wheelBalancer.groupOnWheelBalancer);
                yield break;
            }
            
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    [HarmonyPatch(typeof(PieMenuController), "_GetOnClick_b__72_64")]
    [HarmonyPrefix]
    public static void TireRemoveActionHook()
    {
        if(!Client.Instance.isConnected || !listen) {listen = true; return;}
        
        ClientSend.WheelRemovePacket();
    }
}