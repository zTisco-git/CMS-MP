using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CMS.MainMenu.Controls;
using CMS.UI.Controls;
using CMS21Together.ClientSide.Data.Handle;
using CMS21Together.ServerSide;
using CMS21Together.ServerSide.Data;
using CMS21Together.Shared;
using CMS21Together.Shared.Data;
using CMS21Together.Shared.Data.Vanilla.Cars;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CMS21Together.ClientSide.Data.NewUI;

public static class UIActions
{
	private static bool isClientConnecting;
	private static bool joinCancelled;
	private static GameObject joinProgressContainer;
	private static Image joinProgressFill;
	private static Text joinStatusText;
	private static MainMenuButton joinConfirmButton;
	private static object joinProgressCoroutine;
	private static Action onClientConnectedHandler;
	private static Action onClientDisconnectedHandler;
	private static Action hostClientConnectedHandler;
	private static Action hostClientDisconnectedHandler;

	public static void RegisterJoinUi(MainMenuButton confirmButton, GameObject container, Image fillImage, Text statusText)
	{
		joinConfirmButton = confirmButton;
		joinProgressContainer = container;
		joinProgressFill = fillImage;
		joinStatusText = statusText;
		if (joinProgressContainer != null)
			joinProgressContainer.SetActive(false);
		if (joinProgressFill != null)
		{
			joinProgressFill.type = Image.Type.Filled;
			joinProgressFill.fillMethod = Image.FillMethod.Horizontal;
			joinProgressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
			joinProgressFill.fillAmount = 0f;
		}
		if (joinStatusText != null)
			joinStatusText.text = string.Empty;
	}

	public static void ClearJoinUiReferences()
	{
		if (joinProgressCoroutine != null)
		{
			MelonCoroutines.Stop(joinProgressCoroutine);
			joinProgressCoroutine = null;
		}
		joinProgressContainer = null;
		joinProgressFill = null;
		joinStatusText = null;
		joinConfirmButton = null;
	}

	public static void CancelJoinAttempt()
	{
		if (!isClientConnecting)
			return;
		joinCancelled = true;
		if (Client.Instance != null)
			Client.Instance.Disconnect(true);
	}

	public static void StartClient(string username, string address)
	{
		if (isClientConnecting)
			return;
		ClientData.UserData.username = username;
		if (ClientData.UserData.selectedNetworkType != NetworkType.Steam)
			ClientData.UserData.ip = address;
		else
			ClientData.UserData.lobbyID = address;
		TogetherModManager.SavePreferences();
		if (Client.Instance == null)
			return;
		if (onClientConnectedHandler != null)
			Client.Instance.OnConnected -= onClientConnectedHandler;
		if (onClientDisconnectedHandler != null)
			Client.Instance.OnDisconnected -= onClientDisconnectedHandler;
		isClientConnecting = true;
		joinCancelled = false;
		BeginJoinUiState();
		onClientConnectedHandler = () =>
		{
			CompleteJoinUiState(true, "Connected");
			UICore.ShowPanel(UICore.MP_Lobby);
			if (!Server.Instance.isRunning)
			{
				if (ClientData.UserData.selectedNetworkType == NetworkType.Steam)
					UILobby.CreateLobby(false, ClientData.UserData.lobbyID);
				else
					UILobby.CreateLobby(false, "");
			}
			ClearJoinUiReferences();
		};
		onClientDisconnectedHandler = () =>
		{
			var wasConnecting = isClientConnecting;
			var wasCancelled = joinCancelled;
			CompleteJoinUiState(false, wasCancelled ? "Cancelled" : "Connection failed");
			UICore.ShowPanel(UICore.MP_Main);
			if (wasConnecting && !wasCancelled)
				UICustomPanel.CreateInfoPanel("Failed to connect to server !");
			if (Server.Instance.isRunning)
				MelonCoroutines.Start(Server.Instance.CloseServer());
			if (!wasConnecting || wasCancelled)
				ClearJoinUiReferences();
		};
		Client.Instance.OnConnected += onClientConnectedHandler;
		Client.Instance.OnDisconnected += onClientDisconnectedHandler;
		Client.Instance.ConnectToServer(ClientData.UserData.selectedNetworkType, address);
	}
	
	private static void BeginJoinUiState()
	{
		if (joinConfirmButton != null)
			joinConfirmButton.SetDisabled(true, true);
		if (joinProgressContainer != null)
			joinProgressContainer.SetActive(true);
		if (joinStatusText != null)
			joinStatusText.text = "Connecting...";
		if (joinProgressFill != null)
			joinProgressFill.fillAmount = 0f;
		if (joinProgressCoroutine != null)
		{
			MelonCoroutines.Stop(joinProgressCoroutine);
			joinProgressCoroutine = null;
		}
		joinProgressCoroutine = MelonCoroutines.Start(JoinProgressAnimation());
	}

	private static void CompleteJoinUiState(bool success, string message)
	{
		isClientConnecting = false;
		if (joinProgressCoroutine != null)
		{
			MelonCoroutines.Stop(joinProgressCoroutine);
			joinProgressCoroutine = null;
		}
		if (joinProgressContainer != null)
			joinProgressContainer.SetActive(true);
		if (joinProgressFill != null)
			joinProgressFill.fillAmount = success ? 1f : 0f;
		if (joinStatusText != null)
			joinStatusText.text = message;
		if (!success && joinConfirmButton != null)
			joinConfirmButton.SetDisabled(false, true);
		joinCancelled = false;
	}

	private static IEnumerator JoinProgressAnimation()
	{
		var time = 0f;
		while (true)
		{
			time += Time.deltaTime;
			if (joinProgressFill != null)
				joinProgressFill.fillAmount = Mathf.PingPong(time * 0.5f, 1f);
			yield return null;
		}
	}
	public static void StartServer(string username, int save_index, bool startLocalClient = true)
	{
		ClientData.UserData.username = username;
		ClientData.UserData.autoStartLocalClient = startLocalClient;
		TogetherModManager.SavePreferences();
		
		if (startLocalClient)
		{
			if (hostClientConnectedHandler != null)
				Client.Instance.OnConnected -= hostClientConnectedHandler;
			if (hostClientDisconnectedHandler != null)
				Client.Instance.OnDisconnected -= hostClientDisconnectedHandler;

			hostClientConnectedHandler = () =>
			{
				UICore.ShowPanel(UICore.MP_Lobby);
				if (ClientData.UserData.selectedNetworkType == NetworkType.Steam)
					UILobby.CreateLobby(true, Server.Instance.serverID, save_index);
				else
					UILobby.CreateLobby(true, "", save_index);
			};

			hostClientDisconnectedHandler = () =>
			{
				UICore.ShowPanel(UICore.MP_Main);
				UICustomPanel.CreateInfoPanel("Failed to connect to server !");
			};

			Client.Instance.OnConnected += hostClientConnectedHandler;
			Client.Instance.OnDisconnected += hostClientDisconnectedHandler;
		}
		else
		{
			UICustomPanel.CreateInfoPanel("Starting dedicated server...");
		}

		Server.Instance.StartServer(ClientData.UserData.selectedNetworkType);
		SavesManager.LoadSave(SavesManager.ModSaves[save_index]);

		if (!startLocalClient)
			UICustomPanel.CreateInfoPanel($"Dedicated server running on port {MainMod.PORT}.\nKeep CMS21 open to maintain the server.");
	}
	
	public static UnityAction ChangeNetworkType(MainMenuButton button)
	{
		Action action = () =>
		{
			switch (ClientData.UserData.selectedNetworkType)
			{
				case NetworkType.Steam:
					ClientData.UserData.selectedNetworkType = NetworkType.TCP;
					break;
				case NetworkType.TCP:
					ClientData.UserData.selectedNetworkType = NetworkType.Steam;
					break;
			}

			button.text.text = $"Network type: {ClientData.UserData.selectedNetworkType}";
			button.text.OnEnable();
		};
		return action;
	}

	public static UnityAction LoadGame(MainMenuButton button, int save_index)
	{
		Action action = () =>
		{
			if (UIUtils.GetSaveName(save_index) != "New game" && UICore.last_index_pressed != save_index)
				UICore.ShowCustomPanel(UICore.MP_Host.transform, UICustomPanelType.SaveInfo, button, save_index);
			else if (UIUtils.GetSaveName(save_index) != "New game" && UICore.last_index_pressed == save_index)
				UICore.ShowCustomPanel(UICore.MP_Host.transform, UICustomPanelType.JoinAsHostMenu, button, save_index);
			else
				UICore.ShowCustomPanel(UICore.MP_Host.transform,UICustomPanelType.CreateSave, button, save_index);
		};
		return action;
	}

	public static void CreateNewSave(InputField input, StringSelector selector, MainMenuButton btn, int index)
	{
		if (SavesManager.ModSaves.Any(s => s.Value.Name == input.text))
		{
			UICustomPanel.CreateInfoPanel("A save with the same name already exist.");
			return;
		}

		SavesManager.ModSaves[index].Name = input.text;
		SavesManager.ModSaves[index].selectedGamemode = SavesManager.GetGamemodeFromInt(selector.Current);
		btn.text.text = input.text;
		btn.text.OnEnable();
		SavesManager.SaveModSave(index);
		UnityEngine.Object.Destroy(UICore.TMP_Window);
	}

	public static void DeleteSave(MainMenuButton button, int save_index)
	{
		SavesManager.RemoveModSave(save_index);

		button.GetComponentInChildren<Text>().text = "New Game";
		button.OnEnable();
	}

	public static UnityAction PreviousSaves(MainMenuButton button, MainMenuButton next_button)
	{
		Action action = () =>
		{
			if (UIMenu.save_btn_index == 0)
			{
				button.SetDisabled(true, true);
				return;
			}
			UIMenu.save_btn_index -= 4;

			UIUtils.DestroySavesButton();
			Vector2 b_pos = new Vector2(0, 344);

			int i = UIMenu.save_btn_index;
			while (i < UIMenu.save_btn_index + 4 && i < 16)
			{
				var saveBtn = UIElements.CreateButton(UICore.MP_Host.transform, UIUtils.GetSaveName(i + 4), null);
				var saveRect = saveBtn.GetComponent<RectTransform>();
				saveRect.anchorMin = new Vector2(0f, 0.5f);
				saveRect.anchorMax = new Vector2(0f, 0.5f);
				saveRect.pivot = new Vector2(0f, 0.5f);
				saveRect.sizeDelta = new Vector2(233, 44);
				saveRect.anchoredPosition = b_pos;
				saveBtn.transform.SetSiblingIndex(i % 4);
				
				saveBtn.OnClick.AddListener(UIActions.LoadGame(saveBtn, i + 4));
				saveBtn.SetLocked(false);
				saveBtn.SetDisabled(false, true);
				
				b_pos.y -= 49;
				i++;
			}

			if (UIMenu.save_btn_index == 0)
				button.SetDisabled(true, true);
			else
				button.SetDisabled(false, true);
			
			next_button.SetLocked(false);
			next_button.SetDisabled(false);
		};
		return action;
	}
	
	public static UnityAction NextSaves(MainMenuButton button, MainMenuButton prev_button)
	{
		Action action = () =>
		{
			if (UIMenu.save_btn_index >= 12)
			{
				button.SetDisabled(true, true);
				return;
			}
			UIMenu.save_btn_index += 4;

			UIUtils.DestroySavesButton();
			Vector2 b_pos = new Vector2(0, 344);

			int i = UIMenu.save_btn_index;
			while (i < UIMenu.save_btn_index + 4 && i < 16)
			{
				var saveBtn = UIElements.CreateButton(UICore.MP_Host.transform, UIUtils.GetSaveName(i + 4), null);
				var saveRect = saveBtn.GetComponent<RectTransform>();
				saveRect.anchorMin = new Vector2(0f, 0.5f);
				saveRect.anchorMax = new Vector2(0f, 0.5f);
				saveRect.pivot = new Vector2(0f, 0.5f);
				saveRect.sizeDelta = new Vector2(233, 44);
				saveRect.anchoredPosition = b_pos;
				saveBtn.transform.SetSiblingIndex(i % 4);
				
				saveBtn.OnClick.AddListener(UIActions.LoadGame(saveBtn, i + 4));
				saveBtn.SetLocked(false);
				saveBtn.SetDisabled(false, true);
				
				b_pos.y -= 49;
				i++;
			}

			if (UIMenu.save_btn_index >= 12)
				button.SetDisabled(true, true);
			else
				button.SetDisabled(false, true);
			
			prev_button.SetLocked(false);
			prev_button.SetDisabled(false);
		};
		return action;
	}

	public static UnityAction SwitchReady(MainMenuButton btn)
	{
		Action action = () =>
		{
			foreach (var i in ClientData.Instance.connectedClients.Keys)
			{
				var player = ClientData.Instance.connectedClients[i];
				if (player != null)
					if (player.playerID == ClientData.UserData.playerID)
					{
						player.isReady = !player.isReady;
						ClientSend.ReadyPacket(player.isReady, i);
						if (player.isReady)
							btn.text.text = "Unready";
						else
							btn.text.text = "Ready Up";
						btn.text.OnEnable();
					}
			}
		};
		return action;
	}

	public static void StartGame(int save_index)
	{
		if (ServerData.Instance == null) return;

		foreach (var player in ServerData.Instance.connectedClients.Values)
		{
			if (player != null && !player.isReady)
			{
				UICustomPanel.CreateInfoPanel("All player are not ready.");
				return;
			}
		}
		MelonCoroutines.Start(GameLaunch(save_index));
	}

	private static IEnumerator GameLaunch(int save_index)
	{
		yield return new WaitForEndOfFrame();
		
		SavesManager.StartGame(save_index);
		int i = 0;
		Dictionary<int, ModNewCarData> parksCars = new Dictionary<int, ModNewCarData>();
		foreach (NewCarData carData in SavesManager.currentSave.carsOnParking)
		{
			if (carData != null && !String.IsNullOrEmpty(carData.carToLoad))
			{
				parksCars.Add(i, new ModNewCarData(carData));
			}
			i++;	
		}
		ServerSend.StartPacket(SavesManager.ModSaves[save_index].selectedGamemode, parksCars);
		
		SavesManager.ModSaves[save_index].alreadyLoaded = true;
		if (SavesManager.ModSaves[save_index].additionnalStand != null)
			ServerData.Instance.engineStand2 = SavesManager.ModSaves[save_index].additionnalStand;
		SavesManager.SaveModSave(save_index);
	}
}