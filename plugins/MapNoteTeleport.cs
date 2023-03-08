using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;
using ProtoBuf;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MapNote Teleport", "MON@H", "2.2.1")]
    [Description("Teleports player to marker on map when placed.")]
    public class MapNoteTeleport : CovalencePlugin
    {
        #region Initialization

        private const string PermissionUse = "mapnoteteleport.use";
        private readonly List<ulong> _playersOnCooldown = new List<ulong> ();
        private readonly List<ulong> _playersWithGodMode = new List<ulong> ();

        private void Init()
        {
            LoadData();
            permission.RegisterPermission(PermissionUse, this);
            foreach (var command in _configData.GlobalSettings.Commands)
            {
                AddCovalenceCommand(command, nameof(CmdMapNoteTeleport));
            }
            Unsubscribe(nameof(OnEntityTakeDamage));
        }

        private void OnServerInitialized()
        {
            if (_configData.GlobalSettings.Commands.Length == 0)
            {
                _configData.GlobalSettings.Commands = new[] { "mnt" };
                SaveConfig();
            } 
        }

        private void OnServerSave() => timer.Once(UnityEngine.Random.Range(0f, 60f), SaveData);

        private void Unload()
        {
            SaveData();
        }

        #endregion Initialization

        #region Configuration

        private ConfigData _configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Global settings")]
            public GlobalConfiguration GlobalSettings = new GlobalConfiguration();

            [JsonProperty(PropertyName = "Chat settings")]
            public ChatConfiguration ChatSettings = new ChatConfiguration();

            public class GlobalConfiguration
            {
                [JsonProperty(PropertyName = "Use permissions")]
                public bool UsePermission = true;

                [JsonProperty(PropertyName = "Allow admins to use without permission")]
                public bool AdminsAllowed = true;

                [JsonProperty(PropertyName = "Default Enabled")]
                public bool DefaultEnabled = true;

                [JsonProperty(PropertyName = "Default Cooldown")]
                public float DefaultCooldown = 10f;

                [JsonProperty(PropertyName = "Maximum Cooldown")]
                public float MaximumCooldown = 15f;

                [JsonProperty(PropertyName = "Minimum Cooldown")]
                public float MinimumCooldown = 5f;

                [JsonProperty(PropertyName = "GodMode Cooldown")]
                public float GodModeCooldown = 5f;

                [JsonProperty(PropertyName = "Commands list")]
                public string[] Commands = new[] { "mnt", "mapnoteteleport" };
            }

            public class ChatConfiguration
            {
                [JsonProperty(PropertyName = "Chat steamID icon")]
                public ulong SteamIDIcon = 0;

                [JsonProperty(PropertyName = "Notifications Enabled by default")]
                public bool DefaultNotification = true;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null)
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }
            }
            catch
            {
                PrintError("The configuration file is corrupted");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            _configData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(_configData);

        #endregion Configuration

        #region DataFile

        private StoredData _storedData;

        private class StoredData
        {
            public readonly Dictionary<ulong, PlayerData> PlayerData = new Dictionary<ulong, PlayerData>();
        }

        public class PlayerData
        {
            public bool Enabled;
            public float Cooldown;
            public bool Notification;
        }

        private PlayerData GetPlayerData(ulong playerId, bool addToStored = false)
        {
            PlayerData playerData;
            if (!_storedData.PlayerData.TryGetValue(playerId, out playerData))
            {
                playerData = new PlayerData
                {
                    Enabled = _configData.GlobalSettings.DefaultEnabled,
                    Cooldown = _configData.GlobalSettings.DefaultCooldown,
                    Notification = _configData.ChatSettings.DefaultNotification,
                };

                if (addToStored)
                {
                    _storedData.PlayerData.Add(playerId, playerData);
                }
            }

            return playerData;
        }

        private void LoadData()
        {
            try
            {
                _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {
                _storedData = null;
            }
            finally
            {
                if (_storedData == null)
                {
                    ClearData();
                }
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);

        private void ClearData()
        {
            _storedData = new StoredData();
            SaveData();
        }

        #endregion DataFile

        #region Localization

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CooldownEnded"] = "Cooldown is now ended. You can teleport again.",
                ["GodModeEnded"] = "Godmode is now <color=#B22222>Disabled</color>.",
                ["Disabled"] = "<color=#B22222>Disabled</color>",
                ["Enabled"] = "<color=#228B22>Enabled</color>",
                ["MapNoteTeleport"] = "Teleporting to map marker is now {0}",
                ["MapNoteTeleportCooldown"] = "Teleporting to map marker Cooldown set to <color=#FFA500>{0}</color>s.",
                ["MapNoteTeleportCooldownLimit"] = "Teleporting to map marker Cooldown allowed is between <color=#FFA500>{0}</color>s and <color=#FFA500>{1}</color>s",
                ["MapNoteTeleportDead"] = "You can't teleport while being dead!",
                ["MapNoteTeleportMounted"] = "You can't teleport while seated!",
                ["MapNoteTeleportNotification"] = "Notification to chat is now {0}",
                ["NotAllowed"] = "You do not have permission to use this command!",
                ["Prefix"] = "<color=#BDC3C7>[ <color=#E74C3C>Strangeland</color> ] ",
                ["Teleported"] = "Teleported to <color=#FFA500>{0}</color>. Godmode is now <color=#228B22>Enabled</color> for <color=#FFA500>{1}</color>s. Teleport is on Cooldown for <color=#FFA500>{2}</color>s.",
            }, this);
        }

        #endregion Localization

        #region Commands

        private void CmdMapNoteTeleport(IPlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.Id, PermissionUse))
            {
                //Print(player, Lang("NotAllowed", player.Id));
                return;
            }

            var playerData = GetPlayerData(ulong.Parse(player.Id), true);

            playerData.Enabled = !playerData.Enabled;
            Print(player, Lang("MapNoteTeleport", player.Id, playerData.Enabled ? Lang("Enabled", player.Id) : Lang("Disabled", player.Id)));
        }

        #endregion Commands

        #region Helpers

        private void TeleportToNote(BasePlayer player, MapNote note)
        {
            var err = CheckPlayer(player);
            if (err != null)
            {
                Print(player.IPlayer, Lang(err, player.UserIDString));
                return;
            }

            ulong userID = player.userID;
            _playersOnCooldown.Add(userID);
            _playersWithGodMode.Add(userID);
            Subscribe(nameof(OnEntityTakeDamage));
            player.flyhackPauseTime = _configData.GlobalSettings.MaximumCooldown;
            var pos = note.worldPosition;
            pos.y = GetGroundPosition(pos);

            player.Teleport(pos);
            player.RemoveFromTriggers();
            player.ForceUpdateTriggers();
            var playerData = GetPlayerData(userID);
        }

        private string CheckPlayer(BasePlayer player)
        {
            if (player.isMounted)
            {
                return "MapNoteTeleportMounted";
            }

            if (!player.IsAlive())
            {
                return "MapNoteTeleportDead";
            }

            return null;
        }

        private void OnMapMarkerAdded(BasePlayer player, MapNote note)
        {
            if (player == null || note == null)
            {
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                return;
            }

            var playerData = GetPlayerData(player.userID);
            if (!playerData.Enabled)
            {
                return;
            }

            TeleportToNote(player, note);
        }

        static float GetGroundPosition(Vector3 pos)
        {
            float y = TerrainMeta.HeightMap.GetHeight(pos);
            RaycastHit hitInfo;

            if (Physics.Raycast(
                new Vector3(pos.x, pos.y + 200f, pos.z),
                Vector3.down,
                out hitInfo,
                float.MaxValue,
                (Rust.Layers.Mask.Vehicle_Large | Rust.Layers.Solid | Rust.Layers.Mask.Water)))
            {
                var cargoShip = hitInfo.GetEntity() as CargoShip;
                if (cargoShip != null)
                {
                    return hitInfo.point.y;
                }

                return Mathf.Max(hitInfo.point.y, y);
            }

            return y;
        }

        private object OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (player == null || !player.userID.IsSteamId())
            {
                return null;
            }

            if (_playersWithGodMode.Contains(player.userID))
            {
                return true;
            }

            return null;
        }

        private void Print(IPlayer player, string message)
        {
            string text;
            if (string.IsNullOrEmpty(Lang("Prefix", player.Id)))
            {
                text = message;
            }
            else
            {
                text = Lang("Prefix", player.Id) + message;
            }
            
            text = text + "</color>";
#if RUST
            (player.Object as BasePlayer).SendConsoleCommand ("chat.add", 2, _configData.ChatSettings.SteamIDIcon, text);
            return;
#endif
            player.Message(text);
        }

        #endregion Helpers
    }
}