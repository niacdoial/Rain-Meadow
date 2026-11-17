using Menu;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using RainMeadow.Shared;

namespace RainMeadow
{
    // Static/singleton class for online features and callbacks
    // is a mainloopprocess so update bound to game update? worth it? idk
    public class OnlineManager : MainLoopProcess
    {
        public interface INetworkUpdator
        {
            abstract bool updateALLDomains { get; }
        }

        public static OnlineManager instance;
        public static Serializer serializer = new Serializer(65536);
        public static List<ResourceSubscription> subscriptions;
        public static List<EntityFeed> feeds;
        public static Dictionary<OnlineEntity.EntityId, OnlineEntity> recentEntities;
        public static List<FieldInfo> recentFailedComparisons = new();
        public static float lastSend;
        public static float lastReceive;
        public static OnlinePlayer mePlayer;
        public static List<OnlinePlayer> players;
        public static Queue<OnlinePlayer> queuedSessionPlayers = new(4);
        public static Lobby lobby;

        public static LobbyInfo currentlyJoiningLobby;
        public int milisecondsPerFrame;

        public OnlineManager(ProcessManager manager) : base(manager, RainMeadow.Ext_ProcessID.OnlineManager)
        {
            instance = this;
            framesPerSecond = 20; // alternatively, run as fast as we can for the receiving stuff, but send on a lower tickrate?
            milisecondsPerFrame = 1000 / framesPerSecond;
            NetworkDomain.Initialize();
            LeaveLobby();
            NetworkDomain.OnLobbyJoined += OnlineManager_OnLobbyJoined;
            new StateProfiler();
            RainMeadow.Debug("OnlineManager Created");
        }

        private void OnlineManager_OnLobbyJoined(bool ok, string error)
        {
            RainMeadow.Debug(ok);
            currentlyJoiningLobby = default;
            if (ok)
            {
                manager.rainWorld.progression.Destroy();
                manager.rainWorld.progression = new PlayerProgression(manager.rainWorld, tryLoad: true, saveAfterLoad: false);
                manager.rainWorld.progression.Update();
                // manager.RequestMainProcessSwitch(lobby.gameMode.MenuProcessId());
            }
            else
            {
                // assume that lobby==null means the cleanup either has already been done,
                // or it doesn't need to be
                if (lobby != null) {
                    LeaveLobby();
                }
            }
        }

        public static void AddPlayer(OnlinePlayer player)
        {
            players.Add(player);
            // if (lobby != null && mePlayer == lobby.owner && lobby.bannedUsers.list.Contains(player.id))
            // {
            //     BanHammer.BanUser(player);
            //     ChatLogManager.LogSystemMessage((player.id.GetPersonaName()) + " " + Utils.Translate("tried to join the game but was kicked."));
            //     return;
            // }
            // ChatLogManager.LogSystemMessage((player.id.GetPersonaName()) + " " + Utils.Translate("joined the game."));
        }

        public static void RemovePlayer(OnlinePlayer player)
        {
            RainMeadow.Debug($"Handling player disconnect:{player}");
            player.hasLeft = true;
            lobby?.OnPlayerDisconnect(player);
            while (player.HasUnacknoledgedEvents() && OnlineManager.lobby is not null)
            {
                player.AbortUnacknoledgedEvents();
                lobby?.OnPlayerDisconnect(player);
                ForceLoadUpdate(); // process incoming data
            }
            RainMeadow.Debug($"Actually removing player:{player}");
            players.Remove(player);
            NetworkDomain.currentInstance.ForgetPlayer(player);
            ChatLogManager.LogSystemMessage((player.id.GetPersonaName()) + " " + Utils.Translate("left the game."));
        }


        public static void LeaveLobby()
        {
            ChatLogManager.ResetPlayerColors();
            NetworkDomain.currentInstance.HandleLeavingLobby();
            lobby = null;

            subscriptions = new();
            feeds = new();
            recentEntities = new();

            WorldSession.map = new();
            RoomSession.map = new();
            OnlinePhysicalObject.map = new();

            RainMeadowModManager.Reset();

            mePlayer = NetworkDomain.currentInstance.CreateMePlayer();
            players = new List<OnlinePlayer>() { mePlayer };

            instance.manager.rainWorld.progression.Destroy();
            instance.manager.rainWorld.progression = new PlayerProgression(instance.manager.rainWorld, tryLoad: true, saveAfterLoad: false);
            instance.manager.rainWorld.progression.Update();
        }

        private static void RecieveData()
        {
            if (OnlineManager.instance.manager.currentMainLoop is INetworkUpdator networkUpdator && networkUpdator.updateALLDomains)
            {
                foreach (var domain in NetworkDomain.supportedDomains)
                {
                    NetworkDomain.instances[domain].RecieveData();
                }
            }
            else
            {
                NetworkDomain.currentInstance.RecieveData();
            }

            int networkBudget = 32768;
            while (queuedSessionPlayers.Any() && networkBudget > 0)
            {
                OnlinePlayer p = queuedSessionPlayers.Dequeue();
                try
                {
                    Buffer.BlockCopy(p.latestSessionBytes, 0, OnlineManager.serializer.buffer, 0, p.latestSessionSize);
                    serializer.ReadData(p, p.latestSessionSize);
                }
                catch (Exception e)
                {
                    RainMeadow.Error("Error reading packet from player : " + p.id);
                    RainMeadow.Error(e);
                    serializer.EndRead();
                }
                networkBudget -= p.latestSessionSize;
            }
        }

        public override void RawUpdate(float dt)
        {
            myTimeStacker += dt * (float)framesPerSecond;
            RecieveData();
            lastReceive = UnityEngine.Time.realtimeSinceStartup;

            if (myTimeStacker >= 1f)
            {
                myTimeStacker -= 1f;
                if (myTimeStacker >= 1f)
                {
                    myTimeStacker = 0f;
                }
                Update(); // outgoing data
            }
        }

        // from a force-load situation
        public static void ForceLoadUpdate()
        {
            RecieveData();
            lastReceive = UnityEngine.Time.realtimeSinceStartup;

            if (UnityEngine.Time.realtimeSinceStartup > lastSend + 1f / instance.framesPerSecond)
            {
                instance.Update();
            }
        }

        public override void Update()
        {
            try
            {

                if (lobby != null)
                {
                    mePlayer.tick++;
                    ProcessSelfEvents();
                    ProcessDeferredEvents();

                    if (lobby.isActive)
                    {
                        lobby.Tick(mePlayer.tick);
                    }
                    else if (lobby.isAvailable)
                    {
                        lobby.Activate();
                    }

                    foreach (OnlinePlayer player in players)
                    {
                        player.Update();
                    }
                    recentFailedComparisons.Clear();
                    // Prepare outgoing messages
                    foreach (var subscription in subscriptions)
                    {
                        subscription.Update(mePlayer.tick);
                    }

                    foreach (var feed in feeds)
                    {
                        feed.Update(mePlayer.tick);
                    }

                    // Outgoing messages
                    foreach (var player in players)
                    {
                        SendData(player);
                    }

                    lastSend = UnityEngine.Time.realtimeSinceStartup;
                }
            }
            catch (Exception except)
            {
                RainMeadow.Error("Exception during network frame " + except.ToString());
            }
        }

        public static void SendData(OnlinePlayer toPlayer)
        {
            if (toPlayer.isMe)
                return;

            if (toPlayer.needsAck || toPlayer.OutgoingEvents.Count > 0 || toPlayer.OutgoingStates.Count > 0)
            {
                NetworkDomain.currentInstance?.SendSessionData(toPlayer);
            }
        }



        public static void SendCustomData(OnlinePlayer toPlayer, string key, byte[] data, NetworkDomain.PacketReliability sendType, bool boxed = false)
        {
            if (toPlayer.isMe)
                return;
            if (!lobby.clientSettings.TryGetValue(toPlayer, out var settings) || !settings.TryGetData<CustomClientSettings>(out var customSettings))
                return;
            if (!customSettings.keys.Contains(key))
                return;
            NetworkDomain.currentInstance?.SendCustomData(toPlayer, key, data, sendType, boxed);
        }

        public void ProcessSelfEvents()
        {
            // Stuff mePlayer set to itself, events from the distributed lease system
            int runMax = 1000;
            while (mePlayer.OutgoingEvents.Count > 0 && runMax > 0)
            {
                runMax--;
                try
                {
                    mePlayer.OutgoingEvents.Dequeue().Process();
                }
                catch (Exception e)
                {
                    RainMeadow.Error(e);
                }
            }
        }


        private static Queue<Action> deferredEvents = new Queue<Action>(4);
        public static void RunDeferred(Action action) { deferredEvents.Enqueue(action); }
        public void ProcessDeferredEvents()
        {
            // stuff we want to process after done reading everything incoming
            int runMax = 1000;
            while (deferredEvents.Count > 0 && runMax > 0)
            {
                runMax--;
                try
                {
                    deferredEvents.Dequeue()();
                }
                catch (Exception e)
                {
                    RainMeadow.Error(e);
                }
            }
        }

        public static void ProcessIncomingEvent(OnlineEvent onlineEvent)
        {
            OnlinePlayer fromPlayer = onlineEvent.from;
            fromPlayer.needsAck = true;
            if (EventMath.IsNewer(onlineEvent.eventId, fromPlayer.lastEventFromRemote))
            {
                RainMeadow.Debug($"New event {onlineEvent} from {fromPlayer}, processing...");
                fromPlayer.lastEventFromRemote = onlineEvent.eventId;

                try
                {
                    if (onlineEvent.runDeferred)
                    {
                        RunDeferred(() => onlineEvent.Process());
                        RainMeadow.Debug("deferred: " + onlineEvent);
                    }
                    else
                    {
                        onlineEvent.Process();
                    }
                }
                catch (Exception e)
                {
                    RainMeadow.Error(e);
                }
            }
        }

        public static void ProcessIncomingState(OnlineState state)
        {
            try
            {
                if (state is OnlineResource.ResourceState resourceState)
                {
                    if (resourceState.resource != null && (resourceState.resource.isAvailable || resourceState.resource.isWaitingForState || resourceState.resource.isPending))
                    {
                        RainMeadow.Trace($"Processing {resourceState} for {resourceState.resource}");
                        resourceState.resource.ReadState(resourceState);
                    }
                    else // resource unloaded or not available
                    {
                        RainMeadow.Trace($"Couldn't process {resourceState} for {resourceState.resource?.ToString() ?? "null"}");
                    }
                }
                else if (state is EntityFeedState entityFeedState)
                {
                    if (entityFeedState.inResource != null && entityFeedState.inResource.isAvailable && !entityFeedState.inResource.transitionInProgress)
                    {
                        var ent = entityFeedState.entityState.entityId.FindEntity();
                        if (ent != null)
                        {
                            RainMeadow.Trace($"Processing {entityFeedState} for {ent}");
                            ent.ReadState(entityFeedState);
                        }
                        else
                        {
                            RainMeadow.Error($"Entity {entityFeedState.entityState.entityId} not found for incoming state from {entityFeedState.entityState.from} in {entityFeedState.inResource}");
                        }
                    }
                    else // resource unloaded or not available
                    {
                        RainMeadow.Trace($"Couldn't process {entityFeedState} for {entityFeedState.inResource?.ToString() ?? "null"}");
                    }
                }
                else
                {
                    RainMeadow.Error($"Unexpected incoming state: {state}");
                }
            }
            catch (Exception e)
            {
                RainMeadow.Error($"Error reading state {state}");
                if (state is OnlineResource.ResourceState resourceState && resourceState.resource != null && (resourceState.resource.isAvailable || resourceState.resource.isWaitingForState || resourceState.resource.isPending))
                {
                    RainMeadow.Error(resourceState.resource);
                }
                else if (state is EntityFeedState entityFeedState && entityFeedState.inResource != null && entityFeedState.inResource.isAvailable)
                {
                    var ent = entityFeedState.entityState.entityId.FindEntity();
                    RainMeadow.Error(entityFeedState.inResource);
                    RainMeadow.Error(entityFeedState.entityState);
                    RainMeadow.Error(entityFeedState.entityState.entityId);
                    RainMeadow.Error(ent);
                }
                RainMeadow.Error(e);
            }
        }

        public static void AddSubscription(OnlineResource onlineResource, OnlinePlayer player)
        {
            subscriptions.Add(new ResourceSubscription(onlineResource, player));
        }

        public static void RemoveSubscription(OnlineResource onlineResource, OnlinePlayer player)
        {
            subscriptions.RemoveAll(s => s.resource == onlineResource && s.player == player);
        }

        public static void RemoveSubscriptions(OnlineResource onlineResource)
        {
            subscriptions.RemoveAll(s => s.resource == onlineResource);
        }

        public static void AddFeed(OnlineResource resource, OnlineEntity oe)
        {
            feeds.Add(new EntityFeed(resource, oe));

        }

        public static void RemoveFeed(OnlineResource resource, OnlineEntity oe)
        {
            feeds.RemoveAll(f => f.resource == resource && f.entity == oe);
        }

        public static void RemoveFeeds(OnlineResource resource)
        {
            feeds.RemoveAll(f => f.resource == resource);
        }

        // this smells
        public static OnlineResource ResourceFromIdentifier(string rid)
        {
            if (lobby != null)
            {
                if (rid == "@overworld") return lobby.overworld;
                if (rid == ".") return lobby;
                if (lobby.overworld.isActive)
                {
                    var split = rid.Split('.');
                    if (lobby.overworld.worldSessions.TryGetValue(split[0], out var ws))
                    {
                        if (split.Length >= 2 && ws.roomSessions.TryGetValue(split[1], out var rs))
                            return rs;
                        return ws;
                    }
                }

            }
            RainMeadow.Error("resource not found : " + rid);
            RainMeadow.Stacktrace();
            return null;
        }

        public static void QuitWithError(string v, bool urgent = false)
        {
            RainMeadow.Error(v);
            if (lobby != null)
            {
                var manager = RWCustom.Custom.rainWorld.processManager;
                try
                {
                    if (manager.currentMainLoop is RainWorldGame game && manager.upcomingProcess is null)
                    {
                        if (manager.musicPlayer != null)
                        {
                            manager.musicPlayer.DeathEvent();
                        }

                        game.ExitGame(asDeath: false, asQuit: true);
                    }
                }
                catch (Exception except)
                {
                    RainMeadow.Error(except);
                }

                try
                {
                    LeaveLobby();
                }
                catch (Exception except)
                {
                    RainMeadow.Error(except);
                }

                manager.RequestMainProcessSwitch(RainMeadow.Ext_ProcessID.LobbySelectMenu);
                if (urgent)
                {
                    manager.PreSwitchMainProcess(RainMeadow.Ext_ProcessID.LobbySelectMenu);
                    manager.finalizeModsStep = 0;
                    manager.finalizeModsDelay = 0;
                    manager.modFinalizationDone = false;
                    manager.processAfterModFinalization = RainMeadow.Ext_ProcessID.LobbySelectMenu;
                    manager.upcomingProcess = null;
                }

                manager.ShowDialog(new Menu.DialogNotify(v, manager, () => { }));
                if (RPCEvent.currentRPCEvent is null) // rpc will return normally.
                {
                    throw new Exception(v);
                }
            }
            else if (currentlyJoiningLobby is not null)
            {
                NetworkDomain.AbortJoinLobby(v);
                return;
            }
        }
    }
}
