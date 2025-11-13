using System;
using System.ComponentModel;
using CMS21Together.ClientSide.Data;
using CMS21Together.ClientSide.Data.Handle;
using CMS21Together.ClientSide.Data.Player;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CMS21Together.Shared.Data;

[Serializable]
public class UserData
{
	public string username;
	public string ip;
	public string lobbyID;
	public string playerGUID;

	public NetworkType selectedNetworkType = NetworkType.TCP;
	[DefaultValue(true)] public bool autoStartLocalClient = true;

	[JsonIgnore] public int playerID;
	[JsonIgnore] public bool isReady;

	[JsonIgnore] public GameScene scene;
	[JsonIgnore] public Vector3Serializable position = new(Vector3.zero);
	[JsonIgnore] public QuaternionSerializable rotation = new(Quaternion.identity);
	[JsonIgnore] public int playerLevel;
	[JsonIgnore] public int playerExp;
	[JsonIgnore] public int playerSkillPoints;
	[JsonIgnore] [NonSerialized] public Vector3Serializable lastPosition;
	[JsonIgnore] [NonSerialized] public Animator userAnimator;


	[JsonIgnore] [NonSerialized] public GameObject userObject;
	[JsonIgnore] [NonSerialized] public float lastUpdateTime;

	public UserData()
	{
		username = "player";
		ip = "127.0.0.1";
		lobbyID = "";
		playerID = 1;
		playerGUID = Guid.NewGuid().ToString();
		selectedNetworkType = NetworkType.TCP;
		autoStartLocalClient = true;
	}

	public UserData(string _username, int _playerID, string playerGuid)
	{
		username = _username;
		playerID = _playerID;
		playerGUID = playerGuid;
		autoStartLocalClient = true;
	}

	public void UpdateScene(string sceneName)
	{
		scene = SceneManager.UpdateScene(sceneName);
		ClientSend.SceneChangePacket(scene);
	}

	public void SpawnPlayer()
	{
		if (ClientData.Instance == null || ClientData.Instance.playerPrefab == null)
		{
			return;
		}
		if (playerID == ClientData.UserData.playerID)
		{
			if (GameData.Instance.localPlayer == null)
			{
				MelonLogger.Error("[CMS21-Together] Cannot spawn local player: localPlayer is null.");
				return;
			}
			userObject = GameData.Instance.localPlayer;
		}
		else
		{
			if (userObject != null)
			{
				MelonLogger.Warning($"[UserData->SpawnPlayer] Player {username} (ID: {playerID}) already exists, destroying old instance.");
				DestroyPlayer();
			}
			
			if (GameData.Instance == null || GameData.Instance.localPlayer == null)
			{
				return;
			}
			
			userObject = Object.Instantiate(ClientData.Instance.playerPrefab, position.toVector3(), rotation.toQuaternion());
			if (userObject == null)
			{
				MelonLogger.Error("[UserData->SpawnPlayer] Failed to instantiate player object.");
				return;
			}
			
			userObject.AddComponent<InfoBillboard>();
			userAnimator = userObject.GetComponent<Animator>();
			userObject.name = username;
			
			if (GameData.Instance != null && GameData.Instance.localPlayer != null)
			{
				var localPlayerCollider = GameData.Instance.localPlayer.GetComponent<Collider>();
				var userCollider = userObject.GetComponent<Collider>();
				if (localPlayerCollider != null && userCollider != null)
				{
					Physics.IgnoreCollision(localPlayerCollider, userCollider);
				}
			}
		}

	}

	public void DestroyPlayer()
	{
		if (userObject == null) return;
		Object.Destroy(userObject);
		userObject = null;
		userAnimator = null;
	}
}