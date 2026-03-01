/*
 * ii's Stupid Menu  Classes/Menu/Console.cs
 * A mod menu for Gorilla Tag with over 1000+ mods
 *
 * Copyright (C) 2026  Goldentrophy Software
 * https://github.com/iiDk-the-actual/iis.Stupid.Menu
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using ExitGames.Client.Photon;
using GorillaLocomotion;
using GorillaNetworking;
using HarmonyLib;
using iiMenu.Managers;
using iiMenu.Menu;
using iiMenu.Mods;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Video;
using JoinType = GorillaNetworking.JoinType;
using Random = UnityEngine.Random;

namespace iiMenu.Classes.Menu
{
    public class NoConsole : MonoBehaviour
    {
        #region Configuration
        public static readonly string MenuName = "stupid";
        public static readonly string MenuVersion = PluginInfo.Version;

        public static readonly string ConsoleResourceLocation = $"{PluginInfo.BaseDirectory}/Console";

        public static bool DisableMenu
        {
            get => Main.Lockdown;
            set => Main.Lockdown = value;
        }

        public static void SendNotification(string text, int sendTime = 1000) =>
            NotificationManager.SendNotification(text, sendTime);

        public static void TeleportPlayer(Vector3 position)
        {
            GTPlayer.Instance.TeleportTo(World2Player(position), GTPlayer.Instance.transform.rotation, true);
            VRRig.LocalRig.transform.position = position;

            Movement.lastPosition = position;
            Main.closePosition = position;
        }

        public static void EnableMod(string mod, bool enable)
        {
            if (mod == "Decline Prompt" || mod == "Accept Prompt")
                return;

            ButtonInfo Button = Buttons.GetIndex(mod);
            if (!Button.isTogglable)
                Button.method.Invoke();
            else
            {
                Button.enabled = !enable;
                ToggleMod(Button.buttonText);
            }
        }

        public static void ToggleMod(string mod)
        {
            if (mod == "Decline Prompt" || mod == "Accept Prompt")
                return;

            Main.Toggle(mod);
        }

        public static IEnumerator JoinRoom(string room)
        {
            PhotonNetwork.Disconnect();
            yield return new WaitForSeconds(5f);
            PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(room, JoinType.Solo);
        }

        public static void Log(string text) =>
            LogManager.Log(text);
        #endregion

        #region Events
        public static readonly string ConsoleVersion = "3.0.7";
        public static NoConsole instance;

        public void Awake()
        {
            instance = this;
            PhotonNetwork.NetworkingClient.EventReceived += EventReceived;

            NetworkSystem.Instance.OnReturnedToSinglePlayer += ClearConsoleAssets;
            NetworkSystem.Instance.OnPlayerJoined += SyncConsoleAssets;
            NetworkSystem.Instance.OnPlayerLeft += SyncConsoleUsers;

            if (!Directory.Exists(ConsoleResourceLocation))
                Directory.CreateDirectory(ConsoleResourceLocation);

            Log($@"

     ▄▄·        ▐ ▄ .▄▄ ·       ▄▄▌  ▄▄▄ .
    ▐█ ▌▪▪     •█▌▐█▐█ ▀. ▪     ██•  ▀▄.▀·
    ██ ▄▄ ▄█▀▄ ▐█▐▐▌▄▀▀▀█▄ ▄█▀▄ ██▪  ▐▀▀▪▄
    ▐███▌▐█▌.▐▌██▐█▌▐█▄▪▐█▐█▌.▐▌▐█▌▐▌▐█▄▄▌
    ·▀▀▀  ▀█▄▀▪▀▀ █▪ ▀▀▀▀  ▀█▄▀▪.▀▀▀  ▀▀▀       
           Console {MenuName} {ConsoleVersion}
");
        }

        public static void LoadConsole() =>
            GorillaTagger.OnPlayerSpawned(() => LoadConsoleImmediately());

        public static GameObject LoadConsoleImmediately()
        {
            string ConsoleGUID = "mochi_NoConsole";
            GameObject ConsoleObject = GameObject.Find(ConsoleGUID) ?? new GameObject(ConsoleGUID);
            ConsoleObject.AddComponent<NoConsole>();
            return ConsoleObject;
        }

        public void OnDisable() =>
            PhotonNetwork.NetworkingClient.EventReceived -= EventReceived;

        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            string justName = Path.GetFileName(fileName);
            return string.IsNullOrWhiteSpace(justName) ? null : Path.GetInvalidFileNameChars().Aggregate(justName, (current, c) => current.Replace(c.ToString(), ""));
        }

        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        public static IEnumerator GetTextureResource(string url, Action<Texture2D> onComplete = null)
        {
            if (!textures.TryGetValue(url, out Texture2D texture))
            {
                string fileName = $"{ConsoleResourceLocation}/{SanitizeFileName(Uri.UnescapeDataString(url.Split("/")[^1]))}";

                if (File.Exists(fileName))
                    File.Delete(fileName);

                Log($"Downloading {fileName}");
                using HttpClient client = new HttpClient();
                Task<byte[]> downloadTask = client.GetByteArrayAsync(url);

                while (!downloadTask.IsCompleted)
                    yield return null;

                if (downloadTask.Exception != null)
                {
                    Log("Failed to download texture: " + downloadTask.Exception);
                    yield break;
                }

                byte[] downloadedData = downloadTask.Result;
                Task writeTask = File.WriteAllBytesAsync(fileName, downloadedData);

                while (!writeTask.IsCompleted)
                    yield return null;

                if (writeTask.Exception != null)
                {
                    Log("Failed to save texture: " + writeTask.Exception);
                    yield break;
                }

                Task<byte[]> readTask = File.ReadAllBytesAsync(fileName);
                while (!readTask.IsCompleted)
                    yield return null;

                if (readTask.Exception != null)
                {
                    Log("Failed to read texture file: " + readTask.Exception);
                    yield break;
                }

                byte[] bytes = readTask.Result;
                texture = new Texture2D(2, 2);
                texture.LoadImage(bytes);
            }

            textures[url] = texture;
            onComplete?.Invoke(texture);
        }

        private static readonly Dictionary<string, AudioClip> audios = new Dictionary<string, AudioClip>();
        public static IEnumerator GetSoundResource(string url, Action<AudioClip> onComplete = null)
        {
            if (!audios.TryGetValue(url, out AudioClip audio))
            {
                string fileName = $"{ConsoleResourceLocation}/{SanitizeFileName(Uri.UnescapeDataString(url.Split("/")[^1]))}";

                if (File.Exists(fileName))
                    File.Delete(fileName);

                Log($"Downloading {fileName}");
                using HttpClient client = new HttpClient();
                Task<byte[]> downloadTask = client.GetByteArrayAsync(url);

                while (!downloadTask.IsCompleted)
                    yield return null;

                if (downloadTask.Exception != null)
                {
                    Log("Failed to download audio: " + downloadTask.Exception);
                    yield break;
                }

                byte[] downloadedData = downloadTask.Result;
                Task writeTask = File.WriteAllBytesAsync(fileName, downloadedData);

                while (!writeTask.IsCompleted)
                    yield return null;

                if (writeTask.Exception != null)
                {
                    Log("Failed to save audio: " + writeTask.Exception);
                    yield break;
                }

                string filePath = Assembly.GetExecutingAssembly().Location.Split("BepInEx\\")[0] + fileName;

                Log($"Loading audio from {filePath}");

                using UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip(
                    $"file://{filePath}",
                    GetAudioType(GetFileExtension(fileName))
                );
                yield return audioRequest.SendWebRequest();

                if (audioRequest.result != UnityWebRequest.Result.Success)
                {
                    Log("Failed to load audio: " + audioRequest.error);
                    yield break;
                }

                audio = DownloadHandlerAudioClip.GetContent(audioRequest);
            }

            audios[url] = audio;
            onComplete?.Invoke(audio);
        }

        public static IEnumerator PlaySoundMicrophone(AudioClip sound)
        {
            GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.AudioClip;
            GorillaTagger.Instance.myRecorder.AudioClip = sound;
            GorillaTagger.Instance.myRecorder.RestartRecording(true);
            GorillaTagger.Instance.myRecorder.DebugEchoMode = true;

            yield return new WaitForSeconds(sound.length + 0.4f);

            GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.Microphone;
            GorillaTagger.Instance.myRecorder.AudioClip = null;
            GorillaTagger.Instance.myRecorder.RestartRecording(true);
            GorillaTagger.Instance.myRecorder.DebugEchoMode = false;
        }

        public static string GetFileExtension(string fileName) =>
            fileName.ToLower().Split(".")[fileName.Split(".").Length - 1];

        public static AudioType GetAudioType(string extension)
        {
            return extension.ToLower() switch
            {
                "mp3" => AudioType.MPEG,
                "wav" => AudioType.WAV,
                "ogg" => AudioType.OGGVORBIS,
                "aiff" => AudioType.AIFF,
                _ => AudioType.WAV,
            };
        }

        public const byte ConsoleByte = 68;

        public static void TeleportToMap(string mapName)
        {
            string MapTrigger = "";
            string NetworkTrigger = "";

            if (mapName == "Forest")
            {
                MapTrigger = "Environment Objects/TriggerZones_Prefab/ZoneTransitions_Prefab/Regional Transition/TreeRoomSpawnForestZone";
                NetworkTrigger = "Environment Objects/TriggerZones_Prefab/JoinRoomTriggers_Prefab/JoinPublicRoom - Forest, Tree Exit";
            }
            if (mapName == "City")
            {
                MapTrigger = "Environment Objects/TriggerZones_Prefab/ZoneTransitions_Prefab/Regional Transition/ForestToCity";
                NetworkTrigger = "Environment Objects/TriggerZones_Prefab/JoinRoomTriggers_Prefab/JoinPublicRoom - City Front";
            }
            if (mapName == "Canyons")
            {
                MapTrigger = "Environment Objects/TriggerZones_Prefab/ZoneTransitions_Prefab/Regional Transition/ForestCanyonTransition";
                NetworkTrigger = "Environment Objects/TriggerZones_Prefab/JoinRoomTriggers_Prefab/JoinPublicRoom - Canyon";
            }
            if (mapName == "Clouds")
            {
                MapTrigger = "Environment Objects/TriggerZones_Prefab/ZoneTransitions_Prefab/Regional Transition/CityToSkyJungle";
                NetworkTrigger = "Environment Objects/TriggerZones_Prefab/JoinRoomTriggers_Prefab/JoinPublicRoom - Clouds From Computer";
            }
            if (mapName == "Caves")
            {
                MapTrigger = "Environment Objects/TriggerZones_Prefab/ZoneTransitions_Prefab/Regional Transition/ForestToCave";
                NetworkTrigger = "Environment Objects/TriggerZones_Prefab/JoinRoomTriggers_Prefab/JoinPublicRoom - Cave";
            }
            if (mapName == "Beach")
            {
                MapTrigger = "Environment Objects/TriggerZones_Prefab/ZoneTransitions_Prefab/Regional Transition/BeachToForest";
                NetworkTrigger = "Environment Objects/TriggerZones_Prefab/JoinRoomTriggers_Prefab/JoinPublicRoom - Beach for Computer";
            }
            if (mapName == "Mountains")
            {
                MapTrigger = "Environment Objects/TriggerZones_Prefab/ZoneTransitions_Prefab/Regional Transition/CityToMountain";
                NetworkTrigger = "Environment Objects/TriggerZones_Prefab/JoinRoomTriggers_Prefab/JoinPublicRoom - Mountain";
            }
            if (mapName == "Basement")
            {
                MapTrigger = "Environment Objects/TriggerZones_Prefab/ZoneTransitions_Prefab/Regional Transition/CityToBasement";
                NetworkTrigger = "Environment Objects/TriggerZones_Prefab/JoinRoomTriggers_Prefab/JoinPublicRoom - Basement For Computer";
            }
            if (mapName == "Metropolis")
            {
                MapTrigger = "Environment Objects/TriggerZones_Prefab/ZoneTransitions_Prefab/Regional Transition/MetropolisOnly";
                NetworkTrigger = "Environment Objects/TriggerZones_Prefab/JoinRoomTriggers_Prefab/JoinPublicRoom - Metropolis from Computer";
            }
            if (mapName == "Arcade")
            {
                MapTrigger = "Environment Objects/TriggerZones_Prefab/ZoneTransitions_Prefab/Regional Transition/CityToArcade";
                NetworkTrigger = "Environment Objects/TriggerZones_Prefab/JoinRoomTriggers_Prefab/JoinPublicRoom - City frm Arcade";
            }
            if (mapName == "Critters")
            {
                MapTrigger = "Environment Objects/TriggerZones_Prefab/ZoneTransitions_Prefab/Regional Transition/CityCrittersTransition";
                NetworkTrigger = "Environment Objects/TriggerZones_Prefab/JoinRoomTriggers_Prefab/JoinPublicRoom - City from Critters";
            }
            if (mapName == "Rotating")
            {
                MapTrigger = "Environment Objects/TriggerZones_Prefab/ZoneTransitions_Prefab/Regional Transition/CityToRotating";
                NetworkTrigger = "Environment Objects/TriggerZones_Prefab/JoinRoomTriggers_Prefab/JoinPublicRoom - Rotating Map";
            }
            if (mapName == "Bayou")
            {
                MapTrigger = "Environment Objects/TriggerZones_Prefab/ZoneTransitions_Prefab/Regional Transition/BayouOnly";
                NetworkTrigger = "Environment Objects/TriggerZones_Prefab/JoinRoomTriggers_Prefab/JoinPublicRoom - BayouComputer2";
            }
            if (mapName == "Virtual Stump")
            {
                VirtualStumpTeleporter vstumpt = GameObject.Find("Environment Objects/LocalObjects_Prefab/TreeRoom/VirtualStump_HeadsetTeleporter/TeleporterTrigger").GetComponent<VirtualStumpTeleporter>();
                vstumpt.gameObject.transform.parent.parent.parent.parent.parent.parent.gameObject.SetActive(true);
                vstumpt.gameObject.transform.parent.parent.parent.parent.gameObject.SetActive(true);
                vstumpt.TeleportPlayer();
                return;
            }

            GameObject.Find(MapTrigger).GetComponent<GorillaSetZoneTrigger>().OnBoxTriggered();
            GameObject.Find(NetworkTrigger).SetActive(false);
            TeleportPlayer(GameObject.Find(MapTrigger).transform.position);
        }

        public static readonly int TransparentFX = LayerMask.NameToLayer("TransparentFX");
        public static readonly int IgnoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
        public static readonly int Zone = LayerMask.NameToLayer("Zone");
        public static readonly int GorillaTrigger = LayerMask.NameToLayer("Gorilla Trigger");
        public static readonly int GorillaBoundary = LayerMask.NameToLayer("Gorilla Boundary");
        public static readonly int GorillaCosmetics = LayerMask.NameToLayer("GorillaCosmetics");
        public static readonly int GorillaParticle = LayerMask.NameToLayer("GorillaParticle");

        public static int NoInvisLayerMask() =>
            ~(1 << TransparentFX | 1 << IgnoreRaycast | 1 << Zone | 1 << GorillaTrigger | 1 << GorillaBoundary | 1 << GorillaCosmetics | 1 << GorillaParticle);

        public static Vector3 World2Player(Vector3 world) =>
            world - GorillaTagger.Instance.bodyCollider.transform.position + GorillaTagger.Instance.transform.position;

        public static VRRig GetVRRigFromPlayer(NetPlayer p) =>
            GorillaGameManager.instance.FindPlayerVRRig(p);

        public static NetPlayer GetPlayerFromID(string id) =>
            PhotonNetwork.PlayerList.FirstOrDefault(player => player.UserId == id);

        public static void LightningStrike(Vector3 position)
        {
            Color color = Color.cyan;

            GameObject line = new GameObject("LightningOuter");
            LineRenderer liner = line.AddComponent<LineRenderer>();
            liner.startColor = color; liner.endColor = color; liner.startWidth = 0.25f; liner.endWidth = 0.25f; liner.positionCount = 5; liner.useWorldSpace = true;
            Vector3 victim = position;
            for (int i = 0; i < 5; i++)
            {
                VRRig.LocalRig.PlayHandTapLocal(68, false, 0.25f);
                VRRig.LocalRig.PlayHandTapLocal(68, true, 0.25f);

                liner.SetPosition(i, victim);
                victim += new Vector3(Random.Range(-5f, 5f), 5f, Random.Range(-5f, 5f));
            }
            liner.material.shader = Shader.Find("GUI/Text Shader");
            Destroy(line, 2f);

            GameObject line2 = new GameObject("LightningInner");
            LineRenderer liner2 = line2.AddComponent<LineRenderer>();
            liner2.startColor = Color.white; liner2.endColor = Color.white; liner2.startWidth = 0.15f; liner2.endWidth = 0.15f; liner2.positionCount = 5; liner2.useWorldSpace = true;
            for (int i = 0; i < 5; i++)
                liner2.SetPosition(i, liner.GetPosition(i));

            liner2.material.shader = Shader.Find("GUI/Text Shader");
            liner2.material.renderQueue = liner.material.renderQueue + 1;
            Destroy(line2, 2f);
        }
        
        private static readonly Dictionary<VRRig, List<int>> indicatorDistanceList = new Dictionary<VRRig, List<int>>();
        public static float GetIndicatorDistance(VRRig rig)
        {
            if (indicatorDistanceList.ContainsKey(rig))
            {
                if (indicatorDistanceList[rig][0] == Time.frameCount)
                {
                    indicatorDistanceList[rig].Add(Time.frameCount);
                    return (0.3f + indicatorDistanceList[rig].Count * 0.5f);
                }

                indicatorDistanceList[rig].Clear();
                indicatorDistanceList[rig].Add(Time.frameCount);
                return (0.3f + indicatorDistanceList[rig].Count * 0.5f);
            }

            indicatorDistanceList.Add(rig, new List<int> { Time.frameCount });
            return 0.8f;
        }

        public static Coroutine laserCoroutine;
        public static IEnumerator RenderLaser(bool rightHand, VRRig rigTarget)
        {
            float stoplasar = Time.time + 0.2f;
            while (Time.time < stoplasar)
            {
                rigTarget.PlayHandTapLocal(18, !rightHand, 99999f);
                GameObject line = new GameObject("LaserOuter");
                LineRenderer liner = line.AddComponent<LineRenderer>();
                liner.startColor = Color.red; liner.endColor = Color.red; liner.startWidth = 0.15f + Mathf.Sin(Time.time * 5f) * 0.01f; liner.endWidth = liner.startWidth; liner.positionCount = 2; liner.useWorldSpace = true;
                Vector3 startPos = (rightHand ? rigTarget.rightHandTransform.position : rigTarget.leftHandTransform.position) + (rightHand ? rigTarget.rightHandTransform.up : rigTarget.leftHandTransform.up) * 0.1f;
                Vector3 endPos = Vector3.zero;
                Vector3 dir = rightHand ? rigTarget.rightHandTransform.right : -rigTarget.leftHandTransform.right;
                try
                {
                    Physics.Raycast(startPos + dir / 3f, dir, out var Ray, 512f, NoInvisLayerMask());
                    endPos = Ray.point;
                    if (endPos == Vector3.zero)
                        endPos = startPos + dir * 512f;
                }
                catch { }
                liner.SetPosition(0, startPos + dir * 0.1f);
                liner.SetPosition(1, endPos);
                liner.material.shader = Shader.Find("GUI/Text Shader");
                Destroy(line, Time.deltaTime);

                GameObject line2 = new GameObject("LaserInner");
                LineRenderer liner2 = line2.AddComponent<LineRenderer>();
                liner2.startColor = Color.white; liner2.endColor = Color.white; liner2.startWidth = 0.1f; liner2.endWidth = 0.1f; liner2.positionCount = 2; liner2.useWorldSpace = true;
                liner2.SetPosition(0, startPos + dir * 0.1f);
                liner2.SetPosition(1, endPos);
                liner2.material.shader = Shader.Find("GUI/Text Shader");
                liner2.material.renderQueue = liner.material.renderQueue + 1;
                Destroy(line2, Time.deltaTime);

                GameObject whiteParticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(whiteParticle, 2f);
                Destroy(whiteParticle.GetComponent<Collider>());
                whiteParticle.GetComponent<Renderer>().material.color = Color.yellow;
                whiteParticle.AddComponent<Rigidbody>().linearVelocity = new Vector3(Random.Range(-7.5f, 7.5f), Random.Range(0f, 7.5f), Random.Range(-7.5f, 7.5f));
                whiteParticle.transform.position = endPos + new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(-0.1f, 0.1f), Random.Range(-0.1f, 0.1f));
                whiteParticle.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
                yield return null;
            }
        }

        public static IEnumerator ControllerPress(string buttton, float value, float duration)
        {
            float stop = Time.time + duration;
            while (Time.time < stop)
            {
                switch (buttton)
                {
                    case "lGrip": ControllerInputPoller.instance.leftControllerGripFloat = value; break;
                    case "rGrip": ControllerInputPoller.instance.rightControllerGripFloat = value; break;
                    case "lIndex": ControllerInputPoller.instance.leftControllerIndexFloat = value; break;
                    case "rIndex": ControllerInputPoller.instance.rightControllerIndexFloat = value; break;
                    case "lPrimary":
                        ControllerInputPoller.instance.leftControllerPrimaryButtonTouch = value > 0.33f;
                        ControllerInputPoller.instance.leftControllerPrimaryButton = value > 0.66f;
                        break;
                    case "lSecondary":
                        ControllerInputPoller.instance.leftControllerSecondaryButtonTouch = value > 0.33f;
                        ControllerInputPoller.instance.leftControllerSecondaryButton = value > 0.66f;
                        break;
                    case "rPrimary":
                        ControllerInputPoller.instance.rightControllerPrimaryButtonTouch = value > 0.33f;
                        ControllerInputPoller.instance.rightControllerPrimaryButton = value > 0.66f;
                        break;
                    case "rSecondary":
                        ControllerInputPoller.instance.rightControllerSecondaryButtonTouch = value > 0.33f;
                        ControllerInputPoller.instance.rightControllerSecondaryButton = value > 0.66f;
                        break;
                }
                yield return null;
            }
        }

        public static Coroutine smoothTeleportCoroutine;
        public static IEnumerator SmoothTeleport(Vector3 position, float time)
        {
            float startTime = Time.time;
            Vector3 startPosition = GorillaTagger.Instance.bodyCollider.transform.position;
            while (Time.time < startTime + time)
            {
                TeleportPlayer(Vector3.Lerp(startPosition, position, (Time.time - startTime) / time));
                GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;

                yield return null;
            }

            smoothTeleportCoroutine = null;
        }

        public static IEnumerator AssetSmoothTeleport(ConsoleAsset asset, Vector3? position, Quaternion? rotation, float time)
        {
            float startTime = Time.time;

            Vector3 startPosition = asset.assetObject.transform.position;
            Quaternion startRotation = asset.assetObject.transform.rotation;

            Vector3 targetPosition = position ?? startPosition;
            Quaternion targetRotation = rotation ?? startRotation;

            while (Time.time < startTime + time)
            {
                asset.SetPosition(Vector3.Lerp(startPosition, targetPosition, (Time.time - startTime) / time));
                asset.SetRotation(Quaternion.Lerp(startRotation, targetRotation, (Time.time - startTime) / time));
                yield return null;
            }
        }

        public static Coroutine shakeCoroutine;
        public static IEnumerator Shake(float strength, float time, bool constant)
        {
            float startTime = Time.time;
            while (Time.time < startTime + time)
            {
                float shakePower = constant ? strength : strength * (1f - (Time.time - startTime) / time);
                TeleportPlayer(GorillaTagger.Instance.bodyCollider.transform.position + new Vector3(Random.Range(-shakePower, shakePower), Random.Range(-shakePower, shakePower), Random.Range(-shakePower, shakePower)));

                yield return null;
            }

            shakeCoroutine = null;
        }

        public static void EventReceived(EventData data)
        {
            try
            {
                if (data.Code != ConsoleByte) return;
                // Admin event handling removed
            }
            catch { }
        }

        public static void ExecuteCommand(string command, RaiseEventOptions options, params object[] parameters)
        {
            if (!PhotonNetwork.InRoom)
                return;

            PhotonNetwork.RaiseEvent(ConsoleByte,
                new object[] { command }
                    .Concat(parameters)
                    .ToArray(),
            options, SendOptions.SendReliable);
        }

        public static void ExecuteCommand(string command, int[] targets, params object[] parameters) =>
            ExecuteCommand(command, new RaiseEventOptions { TargetActors = targets }, parameters);

        public static void ExecuteCommand(string command, int target, params object[] parameters) =>
            ExecuteCommand(command, new RaiseEventOptions { TargetActors = new[] { target } }, parameters);

        public static void ExecuteCommand(string command, ReceiverGroup target, params object[] parameters) =>
            ExecuteCommand(command, new RaiseEventOptions { Receivers = target }, parameters);
        #endregion

        #region Asset Loading
        public static readonly Dictionary<string, AssetBundle> assetBundlePool = new Dictionary<string, AssetBundle>();
        public static readonly Dictionary<int, ConsoleAsset> consoleAssets = new Dictionary<int, ConsoleAsset>();

        public static async Task LoadAssetBundle(string assetBundle)
        {
            while (!CosmeticsV2Spawner_Dirty.completed)
                await Task.Yield();

            assetBundle = assetBundle.Replace("\\", "/");
            if (assetBundle.Contains("..") || assetBundle.Contains("%2E%2E"))
                return;

            string fileName;
            if (assetBundle.Contains("/"))
            {
                string[] split = assetBundle.Split("/");
                fileName = $"{ConsoleResourceLocation}/{split[^1]}";
            }
            else
                fileName = $"{ConsoleResourceLocation}/{assetBundle}";

            if (File.Exists(fileName))
                File.Delete(fileName);

            // NOTE: You must provide your own asset bundle URL/source here
            throw new NotImplementedException("Provide your own asset bundle source URL.");
        }

        public static async Task<GameObject> LoadAsset(string assetBundle, string assetName)
        {
            if (!assetBundlePool.ContainsKey(assetBundle))
                await LoadAssetBundle(assetBundle);

            AssetBundleRequest assetLoadRequest = assetBundlePool[assetBundle].LoadAssetAsync<GameObject>(assetName);
            while (!assetLoadRequest.isDone)
                await Task.Yield();

            return assetLoadRequest.asset as GameObject;
        }

        public static IEnumerator SpawnConsoleAsset(string assetBundle, string assetName, int id, string uniqueKey)
        {
            if (consoleAssets.TryGetValue(id, out var asset))
                asset.DestroyObject();

            Task<GameObject> loadTask = LoadAsset(assetBundle, assetName);

            while (!loadTask.IsCompleted)
                yield return null;

            if (loadTask.Exception != null)
            {
                Log($"Failed to load {assetBundle}.{assetName}");
                yield break;
            }

            GameObject targetObject = Instantiate(loadTask.Result);
            new GameObject(uniqueKey).transform.SetParent(targetObject.transform, false);

            consoleAssets.Add(id, new ConsoleAsset(id, targetObject, assetName, assetBundle));
        }

        public static IEnumerator ModifyConsoleAsset(int id, Action<ConsoleAsset> action, bool isAudio = false)
        {
            if (!PhotonNetwork.InRoom)
            {
                Log("Attempt to retrieve asset while not in room");
                yield break;
            }

            if (!consoleAssets.ContainsKey(id))
            {
                float timeoutTime = Time.time + 10f;
                while (Time.time < timeoutTime && !consoleAssets.ContainsKey(id))
                    yield return null;
            }

            if (!consoleAssets.TryGetValue(id, out var asset))
            {
                Log("Failed to retrieve asset from ID");
                yield break;
            }

            if (!PhotonNetwork.InRoom)
            {
                Log("Attempt to retrieve asset while not in room");
                yield break;
            }

            if (isAudio && asset.pauseAudioUpdates)
            {
                float timeoutTime = Time.time + 10f;
                while (Time.time < timeoutTime && asset.pauseAudioUpdates)
                    yield return null;
            }

            if (isAudio && asset.pauseAudioUpdates)
            {
                Log("Failed to update audio data");
                yield break;
            }

            action.Invoke(asset);
        }

        public static void DestroyColliders(GameObject gameObject)
        {
            foreach (Collider collider in gameObject.GetComponentsInChildren<Collider>(true))
                collider.Destroy();
        }

        public static IEnumerator PreloadAssetBundle(string name)
        {
            if (assetBundlePool.ContainsKey(name)) yield break;
            Task loadTask = LoadAssetBundle(name);

            while (!loadTask.IsCompleted)
                yield return null;
        }

        public static void ClearConsoleAssets()
        {
            DisableMenu = false;

            foreach (ConsoleAsset asset in consoleAssets.Values)
                asset.DestroyObject();

            consoleAssets.Clear();
        }

        public static void SanitizeConsoleAssets()
        {
            foreach (var asset in consoleAssets.Values.Where(asset => asset.assetObject == null || !asset.assetObject.activeSelf))
                asset.DestroyObject();
        }

        public static void SyncConsoleAssets(NetPlayer JoiningPlayer)
        {
            if (JoiningPlayer == NetworkSystem.Instance.LocalPlayer)
                return;

            if (consoleAssets.Count <= 0) return;

            foreach (ConsoleAsset asset in consoleAssets.Values)
            {
                ExecuteCommand("asset-spawn", JoiningPlayer.ActorNumber, asset.assetBundle, asset.assetName, asset.assetId);

                if (asset.modifiedPosition)
                    ExecuteCommand("asset-setposition", JoiningPlayer.ActorNumber, asset.assetId, asset.assetObject.transform.position);

                if (asset.modifiedRotation)
                    ExecuteCommand("asset-setrotation", JoiningPlayer.ActorNumber, asset.assetId, asset.assetObject.transform.rotation);

                if (asset.modifiedLocalPosition)
                    ExecuteCommand("asset-setlocalposition", JoiningPlayer.ActorNumber, asset.assetId, asset.assetObject.transform.localPosition);

                if (asset.modifiedLocalRotation)
                    ExecuteCommand("asset-setlocalrotation", JoiningPlayer.ActorNumber, asset.assetId, asset.assetObject.transform.localRotation);

                if (asset.modifiedScale)
                    ExecuteCommand("asset-setscale", JoiningPlayer.ActorNumber, asset.assetId, asset.assetObject.transform.localScale);

                if (asset.bindedToIndex >= 0)
                    ExecuteCommand("asset-setanchor", JoiningPlayer.ActorNumber, asset.assetId, asset.bindedToIndex, asset.bindPlayerActor);
            }

            PhotonNetwork.SendAllOutgoingCommands();
        }

        public static void SyncConsoleUsers(NetPlayer player)
        {
            // Nothing to clean up without userDictionary
        }

        public static int GetFreeAssetID()
        {
            int id;
            do
                id = Random.Range(0, int.MaxValue);
            while (consoleAssets.ContainsKey(id));

            return id;
        }

        public class ConsoleAsset
        {
            public int assetId { get; private set; }

            public int bindedToIndex = -1;
            public int bindPlayerActor;

            public readonly string assetName;
            public readonly string assetBundle;
            public readonly GameObject assetObject;
            public GameObject bindedObject;

            public bool modifiedPosition;
            public bool modifiedRotation;

            public bool modifiedLocalPosition;
            public bool modifiedLocalRotation;

            public bool modifiedScale;

            public bool pauseAudioUpdates;

            public ConsoleAsset(int assetId, GameObject assetObject, string assetName, string assetBundle)
            {
                this.assetId = assetId;
                this.assetObject = assetObject;

                this.assetName = assetName;
                this.assetBundle = assetBundle;
            }

            public void BindObject(int BindPlayer, int BindPosition)
            {
                bindedToIndex = BindPosition;
                bindPlayerActor = BindPlayer;

                VRRig Rig = GetVRRigFromPlayer(PhotonNetwork.NetworkingClient.CurrentRoom.GetPlayer(bindPlayerActor));
                GameObject TargetAnchorObject = null;

                switch (bindedToIndex)
                {
                    case 0:
                        TargetAnchorObject = Rig.headMesh;
                        break;
                    case 1:
                        TargetAnchorObject = Rig.leftHandTransform.parent.gameObject;
                        break;
                    case 2:
                        TargetAnchorObject = Rig.rightHandTransform.parent.gameObject;
                        break;
                    case 3:
                        TargetAnchorObject = Rig.transform.Find("rig/body_pivot").gameObject;
                        break;
                }

                bindedObject = TargetAnchorObject;
                assetObject.transform.SetParent(bindedObject.transform, false);
            }

            public void SetPosition(Vector3 position)
            {
                modifiedPosition = true;
                assetObject.transform.position = position;
            }

            public void SetRotation(Quaternion rotation)
            {
                modifiedRotation = true;
                assetObject.transform.rotation = rotation;
            }

            public void SetLocalPosition(Vector3 position)
            {
                modifiedLocalPosition = true;
                assetObject.transform.localPosition = position;
            }

            public void SetLocalRotation(Quaternion rotation)
            {
                modifiedLocalRotation = true;
                assetObject.transform.localRotation = rotation;
            }

            public void SetScale(Vector3 scale)
            {
                modifiedScale = true;
                assetObject.transform.localScale = scale;
            }

            public void PlayAudioSource(string objectName, string audioClipName = null)
            {
                AudioSource audioSource = (objectName.IsNullOrEmpty() ? assetObject.transform : assetObject.transform.Find(objectName)).GetComponent<AudioSource>();

                if (audioClipName != null)
                    audioSource.clip = assetBundlePool[assetBundle].LoadAsset<AudioClip>(audioClipName);

                audioSource.Play();
            }

            public void PlayAudioSourceOneShot(string objectName, string audioClipName = null)
            {
                AudioSource audioSource = (objectName.IsNullOrEmpty() ? assetObject.transform : assetObject.transform.Find(objectName)).GetComponent<AudioSource>();
                AudioClip clip = audioSource.clip;

                if (audioClipName != null)
                    audioSource.clip = clip;

                audioSource.PlayOneShot(clip);
            }

            public void PlayAnimation(string objectName, string animationClip) =>
                (objectName.IsNullOrEmpty() ? assetObject.transform : assetObject.transform.Find(objectName)).GetComponent<Animator>().Play(animationClip);

            public void StopAudioSource(string objectName) =>
                (objectName.IsNullOrEmpty() ? assetObject.transform : assetObject.transform.Find(objectName)).GetComponent<AudioSource>().Stop();

            public void ChangeAudioVolume(string objectName, float volume)
            {
                if ((objectName.IsNullOrEmpty() ? assetObject.transform : assetObject.transform.Find(objectName)).TryGetComponent(out AudioSource source))
                    source.volume = volume;

                if ((objectName.IsNullOrEmpty() ? assetObject.transform : assetObject.transform.Find(objectName)).TryGetComponent(out VideoPlayer video))
                    video.SetDirectAudioVolume(0, volume);
            }

            public void SetVideoURL(string objectName, string urlName) =>
                (objectName.IsNullOrEmpty() ? assetObject.transform : assetObject.transform.Find(objectName)).GetComponent<VideoPlayer>().url = urlName;

            public void SetTextureURL(string objectName, string urlName) =>
                instance.StartCoroutine(GetTextureResource(urlName, texture =>
                    (objectName.IsNullOrEmpty() ? assetObject.transform : assetObject.transform.Find(objectName)).GetComponent<Renderer>().material.SetTexture("_MainTex", texture)));

            public void SetColor(string objectName, Color color) =>
                (objectName.IsNullOrEmpty() ? assetObject.transform : assetObject.transform.Find(objectName)).GetComponent<Renderer>().material.color = color;

            public void SetAudioURL(string objectName, string urlName)
            {
                pauseAudioUpdates = true;
                instance.StartCoroutine(GetSoundResource(urlName, audio =>
                { (objectName.IsNullOrEmpty() ? assetObject.transform : assetObject.transform.Find(objectName)).GetComponent<AudioSource>().clip = audio; pauseAudioUpdates = false; }));
            }

            public void SetText(string objectName, string text)
            {
                GameObject targetObject = (objectName.IsNullOrEmpty() ? assetObject.transform : assetObject.transform.Find(objectName)).gameObject;
                if (targetObject.TryGetComponent(out Text legacyText))
                    legacyText.text = text;
                if (targetObject.TryGetComponent(out TMP_Text tmpText))
                    tmpText.text = text;
            }

            public void DestroyObject()
            {
                Destroy(assetObject);
                consoleAssets.Remove(assetId);
            }
        }
        #endregion
    }
}