// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Photon Engine">Photon Engine 2024</copyright>
// <summary>Shows how to use the Photon features with minimal "game" code.</summary>
// <author>developer@photonengine.com</author>
// --------------------------------------------------------------------------------------------------------------------

#if UNITY_4_7 || UNITY_5 || UNITY_5_3_OR_NEWER
#define SUPPORTED_UNITY
#endif


namespace DemoParticle
{
    using ExitGames.Client.Photon;
    using Photon.Realtime;
    using Photon.Utils;
    using System.Collections.Generic;
    using System;
    using System.Collections;
    #if SUPPORTED_UNITY
    using Hashtable = ExitGames.Client.Photon.Hashtable;
    #endif


    /// <summary>
    /// Main class of the Photon Particle Demo which makes simple use of several Realtime APIs and features.
    /// </summary>
    /// <returns>
    /// This represents a player / client for the demo. Players move on a grid and got a color.
    /// Position and color are sent via events while the demo uses a Custom Room Property for the current grid size.
    /// 
    /// This GameLogic does not implement the UI and does not depend on Unity.
    /// Instead, it must be integrated into a game loop, depending on the platform / engine.
    /// 
    /// Example: Unity calls a Update method per frame, while Windows Forms could use a Thread to run this.
    ///
    /// The GameLogic will not communicate until CallConnect() gets called.
    /// When the client connected successfully, it will join or create a room automatically, which is where the actual "gameplay" takes place.
    /// Call GameLoop() frequently to establish and keep the connection. It calls the Realtime APIs to triggers all updates and callbacks.
    ///
    /// In networked games, there is info that has to be sent to other clients very often, on event or only once per game.
    /// This demo uses RaiseEvent() and the OnEvent() callback to show how to send positions and other info.
    ///
    /// Read through the code comments and feel free to experiment with this code or use it as basis for your games.
    /// </returns>
    public class GameLogic : IConnectionCallbacks, IInRoomCallbacks, IMatchmakingCallbacks, IOnEventCallback
    {
        /// <summary>Keeps this players/clients instance of the Realtime API.</summary>
        public LoadBalancingClient LbClient;

        /// <summary>The local ParticlePlayer relates to the Photon Player and keeps the position and color for the local client.</summary>
        public ParticlePlayer LocalPlayer;


        /// <summary>Container for the ParticlePlayers (one per Player in Room). Key is the ActorNumber (which is an ID inside a Photon room).</summary>
        public Dictionary<int, ParticlePlayer> ParticlePlayers = new Dictionary<int, ParticlePlayer>();

        /// <summary>Adds a new PhotonPlayer to the ParticlePlayers (one per Photon Player).</summary>
        private ParticlePlayer AddParticlePlayer(Player player)
        {
            ParticlePlayer particlePlayer = new ParticlePlayer(this, player);
            this.ParticlePlayers.Add(player.ActorNumber, particlePlayer);

            return particlePlayer;
        }

        /// <summary>Removes the corresponding PhotonPlayer from ParticlePlayers.</summary>
        private bool RemoveParticlePlayer(Player player)
        {
            return this.ParticlePlayers.Remove(player.ActorNumber);
        }

        /// <summary>Fetches the custom room-property "grid size" or returns default (Constants.GridSizeDefault).</summary>
        public int GridSize
        {
            get
            {
                Hashtable customRoomProps = this.LbClient.CurrentRoom.CustomProperties;
                return customRoomProps.ContainsKey(Constants.GridSizeProp)
                           ? Convert.ToInt32(customRoomProps[Constants.GridSizeProp])   // using Convert for js compatibility (these clients use "number" which is not guaranteed to be int)
                           : Constants.GridSizeDefault;
            }
        }

        /// <summary>Fetches the custom room-property "map" or returns default (MapType.Forest).</summary>
        public string Map
        {
            get
            {
                Hashtable customRoomProps = this.LbClient.CurrentRoom.CustomProperties;
                return customRoomProps.ContainsKey(Constants.MapProp)
                           ? (string)customRoomProps[Constants.MapProp]
                           : Constants.MapType.Forest.ToString();
            }
        }


        /// <summary>When true, update the screen (display the new info) and set to false when done.</summary>
        /// <remarks>Set to true when some state changed or an event or result was dispatched.</remarks>
        public bool UpdateVisuals;

        /// <summary>Send EvColor and EvMove as reliable (or not).</summary>
        /// <remarks>You can set per operation/event if it is reliable or not (this demo simply uses the same reliability for both).</remarks>
        public bool SendReliable;

        /// <summary>The particle demo runs with or without Interest Groups and this is the toggle.</summary>
        /// <remarks>
        /// Clients (GameLogic instances) can turn this on or off independently. Depending on the combination, one game logic
        /// might get no updates at all (if others use the groups) or all info despite using the groups (others might send to all).
        /// </remarks>
        public bool UseInterestGroups { get; private set; }

        /// <summary>Tracks the interval in which the local player should move (unless disabled).</summary>
        public TimeKeeper MoveInterval { get; set; }

        /// <summary>Tracks the interval in which the current position should be sent.</summary>
        /// <remarks>This actually defines how many updates per second this player creates by position updates.</remarks>
        public TimeKeeper UpdateOthersInterval { get; set; }

        /// <summary>Tracks the interval in which DispatchIncomingCommands should be called.</summary>
        /// <remarks>Instead of dispatching incoming info every frame, this demo will do find with a slightly lower rate.</remarks>
        public TimeKeeper DispatchInterval { get; set; }

        /// <summary>Tracks the interval in which SendOutgoingCommands should be called.</summary>
        /// <remarks>You can send in fixed intervals and additionally send when some update was created (to speed up delivery).</remarks>
        public TimeKeeper SendInterval { get; set; }

        /// <summary>Internally used property to get some timestamp.</summary>
        /// <remarks>Could be replaced if more precision would be needed.</remarks>
        public static int Timestamp { get { return Environment.TickCount; } }


        /// <summary>Initializes the GameLogic for this demo (makes up a NickName, sets the AppId, etc.).</summary>
        /// <remarks>If you host a Photon Server, set GameLogic.MasterServerAddress.</remarks>
        public GameLogic()
        {
            this.LbClient = new LoadBalancingClient();
            this.LbClient.AddCallbackTarget(this);

            this.LbClient.NickName = "usr" + SupportClass.ThreadSafeRandom.Next() % 99;

            this.DispatchInterval = new TimeKeeper(10);
            this.SendInterval = new TimeKeeper(50);
            this.MoveInterval = new TimeKeeper(500);
            this.UpdateOthersInterval = new TimeKeeper(this.MoveInterval.Interval);
        }


        /// <summary>Connects with the given AppSettings (which contain the AppId, AppVersion, maybe a region and other settings).</summary>
        public void CallConnect(AppSettings appSettings)
        {
            bool couldConnect = this.LbClient.ConnectUsingSettings(appSettings);

            if (!couldConnect)
            {
                this.LbClient.DebugReturn(DebugLevel.ERROR, "Failed to connect. See logs for details. Increase logging level for more details.");
            }
        }


        /// <summary>This game loop should be called as often as possible. The demo will move the local ParticlePlayer and run other tasks in intervals.</summary>
        public void UpdateLoop()
        {
            // Dispatch means received messages are executed - one by one when you call dispatch.
            // You could also dispatch each frame!
            if (this.DispatchInterval.ShouldExecute)
            {
                while (this.LbClient.LoadBalancingPeer.DispatchIncomingCommands())
                {
                    // You could count dispatch calls to limit them to X, if they take too much time of a single frame
                }
                this.DispatchInterval.Reset();  // we dispatched, so reset the timer
            }

            // If the client is in a room, we might move our LocalPlayer and update others of our position
            if (this.LbClient.InRoom)
            {
                if (this.MoveInterval.ShouldExecute)
                {
                    this.LocalPlayer.MoveRandom();
                    this.MoveInterval.Reset();

                    this.UpdateOthersInterval.ShouldExecute = true; // we just moved. this should produce a update (in this demo)
                    this.UpdateVisuals = true;                         // update visuals to show new pos
                }

                // This demo sends updates in intervals and when the player was moved
                // In a game you could send ~10 times per second or only when the user did some input, too
                if (this.UpdateOthersInterval.ShouldExecute)
                {
                    this.SendPositionUpdate();
                    this.UpdateInterestGroups();

                    this.UpdateOthersInterval.Reset();
                }
            }

            // With the Photon API you can fine-control sending data, which allows the library to aggregate several messages into one package
            // Keep in mind that reliable messages from the server will need a reply (ack), so send more often than needed.
            // If nothing is waiting to be sent, SendOutgoingCommands won't do anything.
            if (this.SendInterval.ShouldExecute)
            {
                this.LbClient.LoadBalancingPeer.SendOutgoingCommands();
                this.SendInterval.Reset();
            }
        }


        /// <summary>Turns on/off usage of Interest Groups and subscribes to the relevant groups on the server.</summary>
        /// <remarks>OpChangeGroups sets this client's interests on the server (read that method's description).</remarks>
        /// <param name="useGroups">Set a specific value or toggle current value if null (default).</param>
        public void SetUseInterestGroups(bool? useGroups = null)
        {
            if (this.LbClient == null || !this.LbClient.InRoom)
            {
                return;
            }


            if (useGroups.HasValue)
            {
                this.UseInterestGroups = useGroups.Value;
            }
            else
            {
                this.UseInterestGroups = !this.UseInterestGroups;
            }


            if (!this.UseInterestGroups)
            {
                this.LbClient.OpChangeGroups(new byte[0], null);    // remove all group-"subscriptions"
                this.LocalPlayer.VisibleGroup = 0;                  // group 0 is never used for actual grouping, so we can "flag" this as as unused
            }
            else
            {
                this.UpdateInterestGroups();    // this method does what we need to subscribe to certain group(s)
            }
        }

        /// <summary>Takes care of Interest Group "subscriptions" for the local player.</summary>
        /// <remarks>In this demo, groups are based on position only but you can make up any rule to divide players into groups.</remarks>
        private void UpdateInterestGroups()
        {
            if (this.UseInterestGroups)
            {
                byte currentGroup = this.GetGroup(this.LocalPlayer);
                if (currentGroup != this.LocalPlayer.VisibleGroup)
                {
                    this.LbClient.OpChangeGroups(new byte[0], new byte[] { currentGroup });     // config the server to only send this group
                    this.LocalPlayer.VisibleGroup = currentGroup;                               // store which group we now are interested in (server side)
                }
            }
        }

        /// <summary>
        /// Gets the group for the specified position (in this case the quadrant).
        /// </summary>
        /// <remarks>
        /// For simplicity, this demo splits the grid into 4 quadrants, no matter which size the grid has.
        ///
        /// Groups can be used to split up a room into regions but also could be used to separate teams, etc.
        /// Groups use a byte as id which starts with 1 and goes up, depending on how many we use.
        /// Group 0 would be received by everyone, so we skip that.
        /// </remarks>
        /// <returns>The group a position is belonging to.</returns>
        public byte GetGroup(ParticlePlayer player)
        {
            int tilesPerGroup = this.GridSize/ Constants.InterestGroupsPerAxis;
            return (byte)(1 + (player.PosX / tilesPerGroup) + ((player.PosY / tilesPerGroup) * Constants.InterestGroupsPerAxis));
        }


        /// <summary>Makes use of the peer (connection to server) to send an Event containing our (local) position.</summary>
        /// <remarks>
        /// In Photon, by default, events go to everyone in the same Room. Outside of Rooms, you can't send events, usually.
        /// There is an option to use Interest Groups to send to just those players interested in a certain group.
        /// This can be used to reduce the number of events each player gets or to hide information from users not in the same group.
        /// </remarks>
        private void SendPositionUpdate()
        {
            if (this.UseInterestGroups)
            {
                // if groups are enabled for this player, we send to the group specific to our position only. note the group parameter
                byte playerGroup = this.GetGroup(this.LocalPlayer);
                this.LbClient.OpRaiseEvent(Constants.EvPosition, this.LocalPlayer.WriteEvMove(), new RaiseEventOptions() { InterestGroup = playerGroup }, new SendOptions() { Reliability = this.SendReliable });
            }
            else
            {
                // this overload of OpRaiseEvent sends to everyone in the room - even those who did not subscribe to any particular group
                this.LbClient.OpRaiseEvent(Constants.EvPosition, this.LocalPlayer.WriteEvMove(), new RaiseEventOptions(), new SendOptions() { Reliability = this.SendReliable });
            }
        }



        /// <summary>
        /// Implements IOnEventCallback. Called for any event received (including several defined by Photon Realtime e.g. Join).
        /// </summary>
        /// <remarks>
        /// Photon defined events have a code > 200.
        /// Custom events (used in demos and games) should start with code 1 or even 0.
        ///
        /// For demo-related events, this code looks up the corresponding ParticlePlayer instance and hands the event content over.
        /// </remarks>
        /// <param name="photonEvent">The event someone (or the server) sent.</param>
        public void OnEvent(EventData photonEvent)
        {
            // events sent from the server (not coming from a specific player) are sent with Sender = 0. this demo ignores those.
            if (photonEvent.Sender <= 0)
            {
                return;
            }

            // each Player in the Room should be represented by a ParticlePlayer, which can be found by associated ActorNumber / Sender
            ParticlePlayer origin = null;
            bool found = this.ParticlePlayers.TryGetValue(photonEvent.Sender, out origin);

            // this demo logic doesn't handle any events from the server (that is done in the base class) so we could return here
            if (!found || origin == null)
            {
                this.LbClient.DebugReturn(DebugLevel.WARNING, photonEvent.Code + " ev. We didn't find a originating player for actorId: " + photonEvent.Sender);
                return;
            }


            // this demo defines 2 events: Position and Color. additionally, a event is triggered when players join or leave
            switch (photonEvent.Code)
            {
                case Constants.EvPosition:
                    origin.ReadEvMove((Hashtable)photonEvent.CustomData);
                    break;
                case Constants.EvColor:
                    origin.ReadEvColor((Hashtable)photonEvent.CustomData);
                    break;
            }

            this.UpdateVisuals = true;
        }


        /// <summary>
        /// Changes the GridSize and stores the new value as room property in the server (synced with anyone in this room).
        /// </summary>
        /// <remarks>
        /// This is a sample of room properties being used.
        /// Simply put, custom room properties can be set via Room.SetCustomProperties and on creation of a room.
        /// When you join a room, you can't set them before the client is "in". Once joined, any client is allowed to change any.
        /// </remarks>
        public void ChangeGridSize()
        {
            int newGridSize = this.GridSize * 2;
            if (newGridSize > Constants.GridSizeMax)
            {
                newGridSize = 2;
            }

            Hashtable newGridSizeProp = new Hashtable() { { Constants.GridSizeProp, newGridSize } };
            this.LbClient.CurrentRoom.SetCustomProperties(newGridSizeProp);
        }

        /// <summary>
        /// Sends this player's color as a cached event for sake of using the cache. Read remarks for details.
        /// </summary>
        /// <remarks>
        /// We would actually recommend storing values for the player (color, character, loadout) as Custom Player Properties.
        /// This demo uses cached events for the sake of showing them at all.
        /// 
        /// The server can cache events on behalf of players, so that joining players get them on join.
        /// This has a similar effect as using Custom Player Properties with the distinction that a sequence of events can be stored this way.
        /// Cached events can e.g. be used to create an object, then update it.
        /// The downside is that each event adds some data that needs to be sent to joining players (which can amount to issues on join).
        /// Cached events can be removed from the cache, so longer running games should avoid the event cache or manage it carefully.
        /// This demo does not manage it's event cache.
        /// </remarks>
        public void ChangeLocalPlayerColor()
        {
            if (this.LocalPlayer != null)
            {
                this.LocalPlayer.RandomizeColor();
                this.LbClient.LoadBalancingPeer.OpRaiseEvent(Constants.EvColor, this.LocalPlayer.WriteEvColor(), new RaiseEventOptions() { CachingOption = EventCaching.AddToRoomCache }, new SendOptions() { Reliability = this.SendReliable });
            }
        }


        /// <summary>Called by the demo logic. Wraps up getting into a room by calling LoadBalancingClient.OpJoinRandomOrCreateRoom().</summary>
        /// <remarks>
        /// When calling OpJoinRandomOrCreateRoom(), the server will try to find a fitting match first and if that fails, immediately create a room.
        /// This is a very lean and convenient approach to matchmaking and gets players into a session quickly.
        ///
        /// When joining a random room, you can use Custom Room Properties to filter for specific properties of a room.
        /// For example, the player may want to join a room with a specific map.
        /// 
        /// As a room can have many custom properties, CustomRoomPropertiesForLobby defines the list of key/values available for matchmaking.
        /// This demo does not use properties for matchmaking but shows how to use them nonetheless.
        ///
        /// More about matchmaking:
        /// https://doc.photonengine.com/en-us/realtime/current/reference/matchmaking-and-lobby
        /// </remarks>
        /// <param name="maptype">Any value of Constants.MapType</param>
        /// <param name="gridSize"></param>
        private void JoinRandomOrCreateDemoRoom(Constants.MapType maptype, int gridSize)
        {
            // custom room properties to use when this client creates a room.
            Hashtable roomPropsForCreation = new Hashtable() { { Constants.MapProp, maptype.ToString() }, { Constants.GridSizeProp, gridSize } };

            EnterRoomParams enterRoomParams = new EnterRoomParams
            {
                RoomName = Guid.NewGuid().ToString().Substring(0, 6),
                RoomOptions = new RoomOptions
                {
                    CustomRoomProperties = roomPropsForCreation,                // if a new room gets created, these will be the custom props of it
                    CustomRoomPropertiesForLobby = Constants.RoomPropsInLobby   // if a new room gets creates, these will be available in the lobby / matchmaking
                }
            };

            this.LbClient.OpJoinRandomOrCreateRoom(null, enterRoomParams);
        }


        #region Callback implementations for Realtime. Call LoadBalancingClient.AddCallbackTarget(this) to actually get callbacks.


        public void OnConnected()
        {
        }

        /// <summary>Called when the client arrives on the Master Server, which provides matchmaking. Join a random room or create one.</summary>
        /// <remarks>Part of IConnectionCallbacks.</remarks>
        public void OnConnectedToMaster()
        {
            this.JoinRandomOrCreateDemoRoom(Constants.MapType.Forest, 16);
        }

        public void OnDisconnected(DisconnectCause cause)
        {
        }

        public void OnRegionListReceived(RegionHandler regionHandler)
        {
        }

        public void OnCustomAuthenticationResponse(Dictionary<string, object> data)
        {
        }

        public void OnCustomAuthenticationFailed(string debugMessage)
        {
        }

        /// <summary>Called when a remote player entered the room. This Player is already added to the room's player list.</summary>
        /// <remarks>
        /// This demo uses the callback to create a corresponding ParticlePlayer.
        /// Part of IInRoomCallbacks.
        /// </remarks>
        public void OnPlayerEnteredRoom(Player newPlayer)
        {
            this.AddParticlePlayer(newPlayer);
        }

        /// <summary>Called when a remote player left the room. This Player is already removed from the room's player list.</summary>
        /// <remarks>
        /// This demo uses the callback to remove the corresponding ParticlePlayer.
        /// Part of IInRoomCallbacks.
        /// </remarks>
        public void OnPlayerLeftRoom(Player otherPlayer)
        {
            this.RemoveParticlePlayer(otherPlayer);
        }

        public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
        }

        public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
        }

        public void OnMasterClientSwitched(Player newMasterClient)
        {
        }

        public void OnFriendListUpdate(List<FriendInfo> friendList)
        {
        }

        public void OnCreatedRoom()
        {
        }

        public void OnCreateRoomFailed(short returnCode, string message)
        {
        }

        /// <summary>Called when the LoadBalancingClient entered a room, no matter if this client created it or simply joined.</summary>
        /// <remarks>
        /// This is the place / time to create ParticlePlayers for anyone already in the room.
        /// Also, set up the local (controlled) ParticlePlayer now and randomize the position and color.
        ///
        /// Part of IMatchmakingCallbacks.
        /// </remarks>
        public void OnJoinedRoom()
        {
            foreach (Player player in this.LbClient.CurrentRoom.Players.Values)
            {
                ParticlePlayer particlePlayer = this.AddParticlePlayer(player);
                if (particlePlayer.IsLocal)
                {
                    this.LocalPlayer = particlePlayer;
                }
            }


            // no matter if we joined or created a game, when we arrived in state "Joined", we are on the game server in a room and
            // this client could start moving and update others of it's color
            this.LocalPlayer.RandomizePosition();
            this.LocalPlayer.RandomizeColor();

            //this.loadBalancingPeer.OpRaiseEvent(Constants.EvColor, this.LocalPlayer.WriteEvColor(), true, 0, null, EventCaching.AddToRoomCache);
            this.LbClient.LoadBalancingPeer.OpRaiseEvent(Constants.EvColor, this.LocalPlayer.WriteEvColor(), new RaiseEventOptions() { CachingOption = EventCaching.AddToRoomCache }, new SendOptions() { Reliability = this.SendReliable });
        }

        /// <summary>Called when a previous OpJoinRoom call failed on the server.</summary>
        /// <remarks>Part of IMatchmakingCallbacks.</remarks>
        public void OnJoinRoomFailed(short returnCode, string message)
        {
            this.JoinRandomOrCreateDemoRoom(Constants.MapType.Forest, 4);
        }

        /// <summary>Called when a previous OpJoinRandom / OpJoinRandomOrCreateRoom call failed on the server.</summary>
        /// <remarks>Part of IMatchmakingCallbacks.</remarks>
        public void OnJoinRandomFailed(short returnCode, string message)
        {
            this.JoinRandomOrCreateDemoRoom(Constants.MapType.Forest, 4);
        }

        public void OnLeftRoom()
        {
        }

        #endregion
    }

}