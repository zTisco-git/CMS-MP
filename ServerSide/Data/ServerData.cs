using System;
using System.Collections.Generic;
using System.Linq;
using CMS21Together.ClientSide.Data;
using CMS21Together.ClientSide.Data.Player;
using CMS21Together.Shared;
using CMS21Together.Shared.Data;
using CMS21Together.Shared.Data.Vanilla.Cars;
using CMS21Together.Shared.Data.Vanilla.GarageTool;
using CMS21Together.Shared.Data.Vanilla.Jobs;
using CMS21Together.Shared.Data.Vanilla;
using MelonLoader;
using UnityEngine;

namespace CMS21Together.ServerSide.Data;

public class ServerData
{
	public static ServerData Instance;
	public Dictionary<ModIOSpecialType, ModCarPlace> toolsPosition = new();
	
	public Dictionary<int, ModCarInfo> CarPartInfo = new();
	public Dictionary<int, ModNewCarData> CarSpawnDatas = new();
	public Dictionary<int, ModNewCarData> CarOnPark = new();
	public ModEngineStand engineStand = new(null);
	public ModEngineStand engineStand2 = new(null);
	public float engineStandAngle;
	public float engineStand2Angle;

	public Dictionary<int, UserData> connectedClients = new();

	public Dictionary<string, GarageUpgrade> garageUpgrades = new();
	public Dictionary<long, ModGroupItem> groupItems = new();
	public Dictionary<long, ModItem> items = new();

	public List<ModJob> jobs = new();
	public int money, scrap;
	public List<ModJob> selectedJobs = new();

	public GarageTool springClamp = new();
	public GarageTool tireChanger = new();
	public GarageTool wheelBalancer = new();
	public Dictionary<int, ModLifterState> lifterStates = new();
	public ModGarageCustomizationData garageCustomization;
	public ModRadioData radioData;

	public void SetGarageUpgrade(GarageUpgrade upgrade)
	{
		garageUpgrades[upgrade.upgradeID] = upgrade;
	}

	public void DeleteCar(int carLoaderID)
	{
		if (CarSpawnDatas.ContainsKey(carLoaderID))
			CarSpawnDatas.Remove(carLoaderID);
		if (CarPartInfo.ContainsKey(carLoaderID))
			CarPartInfo.Remove(carLoaderID);
	}

	public void UpdatePartScripts(ModPartScript partScript, int carLoaderID)
	{
		if (carLoaderID == -1 || carLoaderID == -2)
		{
			UpdateEngineStand(partScript, carLoaderID == -2);
			MelonLogger.Msg("received a enginestand part.");
			return;
		}
		
		if (!Instance.CarPartInfo.ContainsKey(carLoaderID))
			Instance.CarPartInfo.Add(carLoaderID, new ModCarInfo());

		var carInfos = Instance.CarPartInfo[carLoaderID];
		var key = partScript.partID;
		var index = partScript.partIdNumber;

		switch (partScript.type)
		{
			case ModPartType.engine:
				carInfos.EnginePartsReferences[key] = partScript;
				break;
			case ModPartType.suspension:
				if (!carInfos.SuspensionPartsReferences.ContainsKey(key))
					carInfos.SuspensionPartsReferences.Add(key, new Dictionary<int, ModPartScript>());

				if (!carInfos.SuspensionPartsReferences[key].ContainsKey(index))
					carInfos.SuspensionPartsReferences[key].Add(index, partScript);
				else
					carInfos.SuspensionPartsReferences[key][index] = partScript;

				break;
			case ModPartType.other:
				if (!carInfos.OtherPartsReferences.ContainsKey(key))
					carInfos.OtherPartsReferences.Add(key, new Dictionary<int, ModPartScript>());

				if (!carInfos.OtherPartsReferences[key].ContainsKey(index))
					carInfos.OtherPartsReferences[key].Add(index, partScript);
				else
					carInfos.OtherPartsReferences[key][index] = partScript;
				break;
			case ModPartType.driveshaft:
				carInfos.DriveshaftPartsReferences[key] = partScript;
				break;
		}
	}

	private void UpdateEngineStand(ModPartScript partScript, bool alt)
	{
		if (!alt)
			engineStand.parts[partScript.partID] = partScript;
		else
			engineStand2.parts[partScript.partID] = partScript;
	}

	public void UpdateBodyParts(ModCarPart carPart, int carLoaderID)
	{
		if (!Instance.CarPartInfo.ContainsKey(carLoaderID))
			Instance.CarPartInfo.Add(carLoaderID, new ModCarInfo());

		var carInfos = Instance.CarPartInfo[carLoaderID];
		carInfos.BodyPartsReferences[carPart.carPartID] = carPart;
	}

	public void ChangePosition(int carLoaderID, int placeNo)
	{
		if (Instance.CarPartInfo.TryGetValue(carLoaderID, out var info)) info.placeNo = placeNo;
		if (Instance.CarSpawnDatas.TryGetValue(carLoaderID, out var info2)) info2.carPosition = placeNo;
	}

	public void AddJob(ModJob job)
	{
		jobs.Add(job);
	}

	public void RemoveJob(int jobID)
	{
		var job = jobs.Find(j => j.id == jobID);
		if (job != null)
			jobs.Remove(job);
	}

	public void SetLoadJobCar(ModCar carData)
	{
		if (Instance.CarPartInfo.ContainsKey(carData.carLoaderID)) return;

		Instance.CarPartInfo[carData.carLoaderID] = new ModCarInfo();
		ModCarInfo data = Instance.CarPartInfo[carData.carLoaderID];

		data.carToLoad = carData.carID;
		data.carLoaderID = carData.carLoaderID;
		data.configVersion = carData.configVersion;
		data.placeNo = carData.carPosition;
		data.customerCar = carData.customerCar;
	}

	public void UpdateSelectedJobs(ModJob job, bool action)
	{
		if (action)
		{
			if (selectedJobs.All(j => j.id != job.id)) selectedJobs.Add(job);
		}
		else
		{
			if (selectedJobs.Any(j => j.id == job.id)) selectedJobs.Remove(selectedJobs.First(j => j.id == job.id));
		}
	}

	public void ChangeToolPosition(ModIOSpecialType tool, ModCarPlace place)
	{
		toolsPosition[tool] = place;
	}

	public void SetSpringClampState(bool remove, ModGroupItem item)
	{
		if (remove)
		{
			springClamp.isMounted = true;
			springClamp.groupItem = null;
			return;
		}

		springClamp.isMounted = false;
		springClamp.groupItem = item;
	}

	public void SetTireChangerState(bool remove, ModGroupItem item)
	{
		if (remove)
		{
			tireChanger.isMounted = true;
			tireChanger.groupItem = null;
			return;
		}

		tireChanger.isMounted = false;
		tireChanger.groupItem = item;
	}

	public void SetWheelBalancerState(ModGroupItem item)
	{
		if (item == null)
		{
			wheelBalancer.isMounted = false;
			wheelBalancer.additionalState = false;
			wheelBalancer.groupItem = null;
			return;
		}

		if (!wheelBalancer.additionalState)
		{
			wheelBalancer.groupItem = item;
			wheelBalancer.additionalState = true;
			wheelBalancer.isMounted = true;
			return;
		}

		wheelBalancer.groupItem = item;
		wheelBalancer.additionalState = true;
		wheelBalancer.isMounted = true;
	}

	public void EndJob(ModJob job)
	{
		if (selectedJobs.Any(j => j.id == job.id)) selectedJobs.Remove(selectedJobs.First(j => j.id == job.id));
	}

	public void UpdateFluid(ModFluidData fluid, int carLoaderID)
	{
		if (!CarPartInfo.ContainsKey(carLoaderID))
			CarPartInfo.Add(carLoaderID, new ModCarInfo());

		var carInfo = CarPartInfo[carLoaderID];
		if (carInfo.FluidsData == null)
		{
			carInfo.FluidsData = new ModFluidsData();
		}

		if (fluid?.CarFluid != null)
		{
			switch (fluid.CarFluid.FluidType)
			{
				case ModCarFluidType.EngineOil:
					carInfo.FluidsData.Oil = fluid;
					break;
				case ModCarFluidType.Brake:
				if (carInfo.FluidsData.Brake == null)
					carInfo.FluidsData.Brake = new System.Collections.Generic.List<ModFluidData>();
				var brakeIndex = -1;
					for (int i = 0; i < carInfo.FluidsData.Brake.Count; i++)
					{
						if (carInfo.FluidsData.Brake[i]?.CarFluid?.ID == fluid.CarFluid.ID)
						{
							brakeIndex = i;
							break;
						}
					}
					if (brakeIndex >= 0)
						carInfo.FluidsData.Brake[brakeIndex] = fluid;
					else
						carInfo.FluidsData.Brake.Add(fluid);
					break;
				case ModCarFluidType.EngineCoolant:
					if (carInfo.FluidsData.EngineCoolant == null)
						carInfo.FluidsData.EngineCoolant = new System.Collections.Generic.List<ModFluidData>();
					var coolantIndex = -1;
					for (int i = 0; i < carInfo.FluidsData.EngineCoolant.Count; i++)
					{
						if (carInfo.FluidsData.EngineCoolant[i]?.CarFluid?.ID == fluid.CarFluid.ID)
						{
							coolantIndex = i;
							break;
						}
					}
					if (coolantIndex >= 0)
						carInfo.FluidsData.EngineCoolant[coolantIndex] = fluid;
					else
						carInfo.FluidsData.EngineCoolant.Add(fluid);
					break;
				case ModCarFluidType.PowerSteering:
					if (carInfo.FluidsData.PowerSteering == null)
						carInfo.FluidsData.PowerSteering = new System.Collections.Generic.List<ModFluidData>();
					var psIndex = -1;
					for (int i = 0; i < carInfo.FluidsData.PowerSteering.Count; i++)
					{
						if (carInfo.FluidsData.PowerSteering[i]?.CarFluid?.ID == fluid.CarFluid.ID)
						{
							psIndex = i;
							break;
						}
					}
					if (psIndex >= 0)
						carInfo.FluidsData.PowerSteering[psIndex] = fluid;
					else
						carInfo.FluidsData.PowerSteering.Add(fluid);
					break;
				case ModCarFluidType.WindscreenWash:
					if (carInfo.FluidsData.WindscreenWash == null)
						carInfo.FluidsData.WindscreenWash = new System.Collections.Generic.List<ModFluidData>();
					var washIndex = -1;
					for (int i = 0; i < carInfo.FluidsData.WindscreenWash.Count; i++)
					{
						if (carInfo.FluidsData.WindscreenWash[i]?.CarFluid?.ID == fluid.CarFluid.ID)
						{
							washIndex = i;
							break;
						}
					}
					if (washIndex >= 0)
						carInfo.FluidsData.WindscreenWash[washIndex] = fluid;
					else
						carInfo.FluidsData.WindscreenWash.Add(fluid);
					break;
			}
		}
	}

	public void SetEngineOnStand(ModGroupItem engineGroup, Vector3Serializable position, bool alt)
	{
		if (!alt)
		{
			engineStand = new ModEngineStand(null);
			engineStand.engineGroupItem = engineGroup;
			engineStand.position = position;
		}
		else
		{
			engineStand2 = new ModEngineStand(null);
			engineStand2.engineGroupItem = engineGroup;
			engineStand2.position = position;
		}
	}

	public void ClearEngineFromStand(bool alt)
	{
		MelonLogger.Msg("SV: Clear engine.");
		if (!alt)
			engineStand = new ModEngineStand(null);
		else
			engineStand2 = new ModEngineStand(null);
	}

	public void IncreaseStandAngle(float val, bool alt)
	{
		if (!alt)
			engineStandAngle = val;
		else
			engineStand2Angle = val;
	}

	public void SetPlayerInfo(int id, PlayerInfo info)
	{
		connectedClients[id].playerExp = info.playerExp;
		connectedClients[id].playerLevel = info.playerLevel;
		connectedClients[id].playerSkillPoints = info.skillPoints;
		connectedClients[id].position = info.position;
		connectedClients[id].rotation = info.rotation;
	}

	public void SetCarColor(ModColor color)
	{
		foreach (ModCarInfo car in CarPartInfo.Values)
		{
			if (car.placeNo == 5)
			{
				CarSpawnDatas[car.carLoaderID].color = color;
			}
		}
	}

	public void UpdatePartInfo(ModPartInfo info, bool isBody, bool success)
	{
		if (info?.Item == null) return;
		if (!items.TryGetValue(info.Item.UID, out var item)) return;

		item.Condition = success ? info.SuccessCondition : info.FailCondition;
		item.RepairAmount++;
		if (isBody)
			item.Dent = success ? info.DentSuccessCondition : info.DentFailCondition;
	}
	
	public void SetCarWash(int loaderID, bool interior)
	{
		if (!CarPartInfo.TryGetValue(loaderID, out var car))
			return;

		if (interior)
		{
			foreach (KeyValuePair<int, ModCarPart> part in car.BodyPartsReferences)
			{
				if (!part.Value.unmounted)
				{
						part.Value.washFactor = 1;
						part.Value.Dust = 0;
					
				}
			}
			ModCarPart detailsPart = GetCarPart(loaderID, "details");
			if (detailsPart != null)
			{
				detailsPart.Dust = 0;
				ModCarPart details2 = GetCarPart(loaderID, "details2");
				if (details2 != null) details2.Dust = 0;
				ModCarPart details3 = GetCarPart(loaderID, "details3");
				if (details3 != null) details3.Dust = 0;
			}
		}
		else
		{
			string[] interiorParts = { "benchFront", "bench", "steeringWheel", "seatLeft", "seatRight", "details", "details2", "details3" };
			foreach (string partName in interiorParts)
			{
				ModCarPart part = GetCarPart(loaderID, partName);
				if (part != null && !part.unmounted)
				{
					part.condition = 1;
					part.Dust = 0;
					if (partName == "details")
					{
						ModCarPart details2 = GetCarPart(loaderID, "details2");
						if (details2 != null) details2.Dust = 0;

						ModCarPart details3 = GetCarPart(loaderID, "details3");
						if (details3 != null) details3.Dust = 0;
					}
				}
			}
		}
	}

	public void SetLifterState(int carLoaderID, ModLifterState state)
	{
		lifterStates[carLoaderID] = state;
	}

	public void RemoveClient(int clientId)
	{
		if (!connectedClients.ContainsKey(clientId))
			return;

		connectedClients.Remove(clientId);
	}

	public PlayerInfo GetPlayerInfo(string playerId)
	{
		return SavesManager.ModSaves[SavesManager.currentSaveIndex].playerInfos.FirstOrDefault(p => p.id == playerId);
	}

	public Vector3 GetDefaultSpawnPosition()
	{
		if (connectedClients.TryGetValue(1, out var host) && host.position != null)
		{
			var position = host.position.toVector3();
			position.y = Mathf.Max(position.y, 0f);
			return position;
		}

		var existingInfo = SavesManager.ModSaves[SavesManager.currentSaveIndex].playerInfos.FirstOrDefault();
		if (existingInfo != null)
		{
			var position = existingInfo.position.toVector3();
			position.y = Mathf.Max(position.y, 0f);
			return position;
		}

		return new Vector3(-9.5f, 0f, -4.5f);
	}

	public Quaternion GetDefaultSpawnRotation()
	{
		if (connectedClients.TryGetValue(1, out var host) && host.rotation != null)
			return host.rotation.toQuaternion();

		var existingInfo = SavesManager.ModSaves[SavesManager.currentSaveIndex].playerInfos.FirstOrDefault();
		if (existingInfo != null)
			return existingInfo.rotation.toQuaternion();

		return Quaternion.identity;
	}

	public void UpdatePlayerTransform(int clientId, Vector3Serializable position, QuaternionSerializable rotation)
	{
		if (!connectedClients.ContainsKey(clientId))
			return;

		var clientData = connectedClients[clientId];
		var info = GetPlayerInfo(clientData.playerGUID);
		if (info != null)
			info.UpdateStats(position, rotation, clientData.playerExp, clientData.playerLevel, clientData.playerSkillPoints);
	}

	private ModCarPart GetCarPart(int loaderID, string partName)
	{
		return CarPartInfo[loaderID].BodyPartsReferences
			.Values.FirstOrDefault(p => p.name == partName);
	}

	public void SetWelder(int loaderID)
	{
		if (!CarPartInfo.ContainsKey(loaderID))
			return;
		
		ModCarPart part = GetCarPart(loaderID, "body");
		part.condition = 1;
		part.dent = 1;
		ModCarPart part2 = GetCarPart(loaderID, "details");
		part2.dent = 1;
	}

	public void AddCarToPark(ModNewCarData car, int index)
	{
		CarOnPark.Add(index, car);
	}

	public void RemoveCarFromPark(int index)
	{
		if (CarOnPark.ContainsKey(index))
			CarOnPark.Remove(index);
	}
}


public class GarageTool
{
	public bool additionalState;
	public ModGroupItem groupItem;
	public bool isMounted;
}

public class ModCarInfo
{
	public int carLoaderID;
	public string carToLoad;
	public int configVersion;
	public bool customerCar;
	public int placeNo;
	public Dictionary<int, ModCarPart> BodyPartsReferences = new();
	public Dictionary<int, ModPartScript> DriveshaftPartsReferences = new();
	public Dictionary<int, ModPartScript> EnginePartsReferences = new();
	public Dictionary<int, Dictionary<int, ModPartScript>> OtherPartsReferences = new();
	public Dictionary<int, Dictionary<int, ModPartScript>> SuspensionPartsReferences = new();
}