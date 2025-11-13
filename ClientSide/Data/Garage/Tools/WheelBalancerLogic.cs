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
    }

    [HarmonyPatch(typeof(WheelBalanceWindow), nameof(WheelBalanceWindow.OnMiniGameFinished))]
    [HarmonyPostfix]
    public static void OnMiniGameFinishedHook(WheelBalanceWindow __instance)
    {
        if(!Client.Instance.isConnected || !listen) {listen = true; return;}
        
        if (GameData.Instance.wheelBalancer.groupOnWheelBalancer != null)
        {
            ClientSend.WheelBalancePacket(GameData.Instance.wheelBalancer.groupOnWheelBalancer);
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