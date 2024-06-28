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
    using System.Collections;
    #if SUPPORTED_UNITY
    using Hashtable = ExitGames.Client.Photon.Hashtable;
    #endif


    /// <summary>Represents a Player in a Room to store the related values: Position and Color.</summary>
    /// <remarks>
    /// Instances are handled internally by the GameLogic (via AddParticlePlayer and RemoveParticlePlayer).
    /// The GameLogic.LocalPlayer represents this user's player (in the room).
    ///
    /// This class does not make use of networking directly. It gets updated by incoming events but
    /// the actual sending and receiving is handled in GameLogic.
    ///
    /// The WriteEv* and ReadEv* methods are simple ways to create event-content per player.
    /// 
    /// The LocalPlayer per client will actually send data. This is used to update the remote clients.
    /// The other instances are used to read and keep the state.
    /// Receiving clients identify the corresponding Player and call ReadEv* to update the (remote) player.
    /// 
    /// Read the remarks in WriteEvMove.
    /// </remarks>
    public class ParticlePlayer
    {
        public GameLogic GameLogic;
        private Player player;

        public int PosX { get; set; }
        public int PosY { get; set; }
        public int Color { get; set; }

        private int LastUpdateTimestamp { get; set; }
        public int UpdateAge { get { return GameLogic.Timestamp - this.LastUpdateTimestamp; } }


        public bool IsLocal
        {
            get { return this.player.IsLocal; }
        }

        public int ActorNumber
        {
            get { return this.player.ActorNumber; }
        }

        public string NickName
        {
            get { return this.player.NickName; }
        }


        /// <summary>
        /// Stores this client's "group interest currently set on server" of this player (not necessarily the current one).
        /// </summary>
        public byte VisibleGroup { get; set; }


        public ParticlePlayer(GameLogic gameLogic, Player p)
        {
            this.GameLogic = gameLogic;
            this.player = p;
        }

        /// <summary>Creates a random color by generating a new integer and setting the highest byte to 255.</summary>
        /// <remarks>RGB colors can be represented as integer (3 bytes, ignoring the first which represents alpha).</remarks>
        internal void RandomizeColor()
        {
            this.Color = (int)((uint)SupportClass.ThreadSafeRandom.Next() | 0xFF000000);
        }

        /// <summary>Randomizes position within the gridSize.</summary>
        internal void RandomizePosition()
        {
            if (this.GameLogic == null)
            {
                return;
            }

            this.PosX = SupportClass.ThreadSafeRandom.Next() % this.GameLogic.GridSize;
            this.PosY = SupportClass.ThreadSafeRandom.Next() % this.GameLogic.GridSize;
        }

        /// <summary>
        /// Simple method to make the "player" move even without input. This way, we get some
        /// movement even if one developer tests with many running clients.
        /// </summary>
        internal void MoveRandom()
        {
            this.PosX += (SupportClass.ThreadSafeRandom.Next() % 3) - 1;
            this.PosY += (SupportClass.ThreadSafeRandom.Next() % 3) - 1;
            this.ClampPosition();
        }


        /// <summary>Checks if a position is in the grid (still on the board) and corrects it if needed.</summary>
        public void ClampPosition()
        {
            if (this.GameLogic == null)
            {
                return;
            }


            if (this.PosX < 0)
            {
                this.PosX = 0;
            }

            if (this.PosX >= this.GameLogic.GridSize - 1)
            {
                this.PosX = this.GameLogic.GridSize - 1;
            }

            if (this.PosY < 0)
            {
                this.PosY = 0;
            }

            if (this.PosY > this.GameLogic.GridSize - 1)
            {
                this.PosY = this.GameLogic.GridSize - 1;
            }
        }

        /// <summary>
        /// Converts the player info into a string.
        /// </summary>
        /// <returns>String showing basic info about this player.</returns>
        public override string ToString()
        {
            return $"{this.NickName} ({this.ActorNumber})";
        }

        /// <summary>Creates a position update Hashtable to be used as content for a "move" event.</summary>
        /// <remarks>
        /// As with event codes, the content of this event is defined for the purpose of this demo.
        /// Your game (e.g.) could use floats as positions or you send a height and actions or state info.
        ///
        /// In this demo, we use Hashtables as event content. The way it is used here creates some garbage every time
        /// data is sent. To avoid this, a more advanced approach would be to send a byte[] directly.
        /// Look into using ByteArraySlice if you want to achieve "zero allocation" event sending and receiving.
        /// 
        /// When using Hashtables, it makes sense to use integer or byte keys, which use few bytes in serialized form.
        /// Of course this is not a requirement.
        ///
        /// The position can only go up to 128 in this demo, so a byte[] a good and lean choice here.
        /// </remarks>
        /// <returns>Hashtable for event "move" to update others</returns>
        public Hashtable WriteEvMove()
        {
            Hashtable evContent = new Hashtable();
            evContent[(byte)1] = new byte[] { (byte)this.PosX, (byte)this.PosY };
            return evContent;
        }

        /// <summary>Reads the Hashtable received as "move" event.</summary>
        public void ReadEvMove(Hashtable evContent)
        {
            if (evContent.ContainsKey((byte)1))
            {
                byte[] posArray = (byte[])evContent[(byte)1];
                this.PosX = posArray[0];
                this.PosY = posArray[1];
            }
            else if (evContent.ContainsKey("1"))
            {
                // js client event support (those can't send with byte-keys)
                var posArray = (object[])evContent["1"];   // NOTE: this is subject to change while we update the serialization in JS/Server
                this.PosX = System.Convert.ToByte(posArray[0]);
                this.PosY = System.Convert.ToByte(posArray[1]);
            }
            this.LastUpdateTimestamp = GameLogic.Timestamp;
        }

        /// <summary>Creates the "custom content" Hashtable sent as color update.</summary>
        /// <returns>Hashtable for event "color" to update others. See WriteEvMove for more remarks on event content.</returns>
        public Hashtable WriteEvColor()
        {
            Hashtable evContent = new Hashtable();
            evContent[(byte)1] = this.Color;
            return evContent;
        }

        /// <summary>Reads the "custom content" Hashtable received as color update.</summary>
        public void ReadEvColor(Hashtable evContent)
        {
            if (evContent.ContainsKey((byte)1))
            {
                this.Color = (int)evContent[(byte)1];
            }
            else if (evContent.ContainsKey("1"))
            {
                // js client event support (those can't send with byte-keys)
                this.Color = System.Convert.ToInt32(evContent["1"]);
            }
            this.LastUpdateTimestamp = GameLogic.Timestamp;
        }
    }
}
