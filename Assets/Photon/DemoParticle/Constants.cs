// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Photon Engine">Photon Engine 2024</copyright>
// <summary>Shows how to use the Photon features with minimal "game" code.</summary>
// <author>developer@photonengine.com</author>
// --------------------------------------------------------------------------------------------------------------------


namespace DemoParticle
{
    /// <summary>
    /// Class to define a few constants used in this demo (for event codes, properties and game constants).
    /// </summary>
    public static class Constants
    {
        /// <summary>(1) Messages the color of a player, using OpRaiseEvent.</summary>
        public const byte EvColor = 1;

        /// <summary>(2) Messages the position of a player, using OpRaiseEvent.</summary>
        public const byte EvPosition = 2;

        /// <summary>("s") Grid size currently used in this room. Used as key for a Custom Room Property.</summary>
        public const string GridSizeProp = "s";

        /// <summary>("m") Property map (map / level / scene) currently used in this room.</summary>
        public const string MapProp = "m";

        /// <summary>Types available as map / level / scene.</summary>
        public enum MapType { Forest, Town, Sea }

        ///<summary>Maximum number of cells in x and y direction.</summary>
        public const int GridSizeMax = 128;

        ///<summary>Default GridSize for this demo.</summary>
        public const int GridSizeDefault = 16;

        /// <summary>How many Interest Groups get used to divide each axis.</summary>
        public const int InterestGroupsPerAxis = 2;

        /// <summary>This is the list of custom room properties that we want listed in the lobby.</summary>
        public static readonly string[] RoomPropsInLobby = new string[] { Constants.MapProp, Constants.GridSizeProp };
    }
}