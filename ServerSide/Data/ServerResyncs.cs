using System;
using System.Collections.Generic;
using CMS21Together.Shared.Data;
using CMS21Together.Shared.Data.Vanilla;
using CMS21Together.Shared.Data.Vanilla.Cars;
using CMS21Together.Shared.Data.Vanilla.GarageTool;
using MelonLoader;

namespace CMS21Together.ServerSide.Data;

public static class ServerResyncs
{
	public static void ResyncCar(int playerID, int carLoaderID)
	{
		ModNewCarData carToResync = ServerData.Instance.CarSpawnDatas[carLoaderID];
		ModCarInfo carInfo = ServerData.Instance.CarPartInfo[carLoaderID];
		
		MelonLogger.Msg($"Sent a resync car from: {carLoaderID}  {carToResync.CarInfoData.CarFrom}");
		
		ServerSend.LoadCarPacket(playerID, carToResync, carLoaderID, true);

		foreach (KeyValuePair<int, ModCarPart> partsReference in carInfo.BodyPartsReferences)
		{
			ServerSend.BodyPartPacket(playerID, partsReference.Value, carLoaderID, true);
		}
		
		foreach (KeyValuePair<int,Dictionary<int,ModPartScript>> partsReference in carInfo.OtherPartsReferences)
		{
			foreach (KeyValuePair<int,ModPartScript> modPartScript in partsReference.Value)
			{
				ServerSend.PartScriptPacket(playerID, modPartScript.Value, carLoaderID, true);
				MelonLogger.Msg("Sent part.");
			}
		}
		
		foreach (KeyValuePair<int, ModPartScript> partsReference in carInfo.DriveshaftPartsReferences)
		{
			ServerSend.PartScriptPacket(playerID, partsReference.Value, carLoaderID, true);
		}
		
		foreach (KeyValuePair<int, ModPartScript> partsReference in carInfo.EnginePartsReferences)
		{
			ServerSend.PartScriptPacket(playerID, partsReference.Value, carLoaderID, true);
		}
		
		foreach (KeyValuePair<int,Dictionary<int,ModPartScript>> partsReference in carInfo.SuspensionPartsReferences)
		{
			foreach (KeyValuePair<int,ModPartScript> modPartScript in partsReference.Value)
			{
				ServerSend.PartScriptPacket(playerID, modPartScript.Value, carLoaderID, true);
				MelonLogger.Msg("Sent part.");
			}
		}
		MelonLogger.Msg("[ServerResyncs->ResyncCar] Resent car info to client!");
	}
	
	public static void ResyncEngineStand(int fromClient, bool alt)
	{
		MelonLogger.Msg($"Client asked for es resync!");
		if (alt)
		{
			if (ServerData.Instance.engineStand2 != null && ServerData.Instance.engineStand2.engineGroupItem != null)
			{
				ServerSend.EngineStandSetGroupPacket(fromClient, ServerData.Instance.engineStand2.engineGroupItem,  ServerData.Instance.engineStand2.position, true, true);
				foreach (KeyValuePair<int,ModPartScript> part in ServerData.Instance.engineStand2.parts)
				{
					ServerSend.PartScriptPacket(fromClient, part.Value, -2, true);
					MelonLogger.Msg($"Sent Engine Stand part {part.Value.id}!");
				}
				MelonLogger.Msg($"Sent Engine Stand Resync!");
			}
		}
		else
		{
			if (ServerData.Instance.engineStand != null && ServerData.Instance.engineStand.engineGroupItem != null)
			{
				ServerSend.EngineStandSetGroupPacket(fromClient, ServerData.Instance.engineStand.engineGroupItem, ServerData.Instance.engineStand.position, false, true);
				foreach (KeyValuePair<int, ModPartScript> part in ServerData.Instance.engineStand.parts)
				{
					ServerSend.PartScriptPacket(fromClient, part.Value, -1, true);
				}
				MelonLogger.Msg($"Sent Engine Stand Resync!");
			}
		}
		
	}

	public static void ResyncTools(int fromClient)
	{
		foreach (KeyValuePair<ModIOSpecialType, ModCarPlace> tool in ServerData.Instance.toolsPosition)
		{
			ServerSend.ToolsMovePacket(fromClient, tool.Key, tool.Value, false, true);
		}
	}

	public static void ResyncLifters(int fromClient)
	{
		foreach (KeyValuePair<int, ModLifterState> lifter in ServerData.Instance.lifterStates)
		{
			ServerSend.LifterPacket(fromClient, lifter.Value, lifter.Key, true);
		}
	}

	public static void ResyncGarageCustomization(int fromClient)
	{
		if (ServerData.Instance.garageCustomization != null)
			ServerSend.GarageCustomizationPacketToClient(fromClient, ServerData.Instance.garageCustomization);
	}
	
	public static void ResyncPark(int fromClient)
	{
		foreach (KeyValuePair<int, ModNewCarData> car in ServerData.Instance.CarOnPark)
		{
			ServerSend.AddCarToParkPacket(fromClient, car.Value, car.Key, true);
		}
	}

	public static void ResyncUpgrade(int fromClient)
	{
		foreach (KeyValuePair<string,GarageUpgrade> upgrade in ServerData.Instance.garageUpgrades)
		{
			ServerSend.GarageUpgradePacket(fromClient, upgrade.Value, true);
		}
	}

	public static void ResyncRadio(int fromClient)
	{
		if (ServerData.Instance.radioData != null)
			ServerSend.RadioPacketToClient(fromClient, ServerData.Instance.radioData);
	}

	public static void ResyncAll(int fromClient)
	{
		MelonLogger.Msg($"[ServerResyncs->ResyncAll] Sending all sync data to client {fromClient}...");
		
		ResyncGarageCustomization(fromClient);
		ResyncUpgrade(fromClient);
		ResyncTools(fromClient);
		ResyncLifters(fromClient);
		ResyncPark(fromClient);
		ResyncRadio(fromClient);
		
		foreach (var carLoaderID in ServerData.Instance.CarSpawnDatas.Keys)
		{
			ResyncCar(fromClient, carLoaderID);
		}
		
		ResyncEngineStand(fromClient, false);
		ResyncEngineStand(fromClient, true);
		
		foreach (var job in ServerData.Instance.jobs)
		{
			ServerSend.JobPacket(fromClient, job, true);
		}
		
		foreach (var selectedJob in ServerData.Instance.selectedJobs)
		{
			ServerSend.SelectedJobPacket(fromClient, selectedJob, true);
		}
		
		MelonLogger.Msg($"[ServerResyncs->ResyncAll] All sync data sent to client {fromClient}!");
	}
}