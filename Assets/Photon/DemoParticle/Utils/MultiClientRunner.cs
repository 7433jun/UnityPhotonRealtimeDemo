// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Photon Engine">Photon Engine 2024</copyright>
// <summary>Shows how to use the Photon features with minimal "game" code.</summary>
// <author>developer@photonengine.com</author>
// --------------------------------------------------------------------------------------------------------------------


using Photon.Realtime;


namespace DemoParticle
{
    using System.Collections.Generic;
    using UnityEngine;
    using Hashtable = ExitGames.Client.Photon.Hashtable;


    /// <summary>This component can handle a range of GameLogic instances to run multiple clients at the same time.</summary>
    /// <remarks>
    /// Important:
    /// This class is only useful for the purpose of a demo, as it is easier than running multiple builds.
    /// This way, the ActiveGameLogic (in DemoUI) quickly has company to receive other players moves, colors, etc.
    /// 
    /// The approach can be useful to test a game but usually you would run the build multiple times.
    /// </remarks>
    public class MultiClientRunner : MonoBehaviour
    {
        private List<GameLogic> clients = new List<GameLogic>();


        public void AddClient(AppSettings appSettings)
        {
            GameLogic ActiveGameLogic = new GameLogic();
            ActiveGameLogic.CallConnect(appSettings);
            this.clients.Add(ActiveGameLogic);
        }

        void Update()
        {
            foreach (GameLogic game in this.clients)
            {
                game.UpdateLoop();
            }
        }

        public void ToggleInterestGroupUsage(bool? value = null)
        {
            foreach (GameLogic game in this.clients)
            {
                game.SetUseInterestGroups(value);
            }
        }

        public void RemoveClient()
        {
            if (this.clients.Count > 0)
            {
                GameLogic toRemove = this.clients[0];
                this.clients.RemoveAt(0);

                toRemove.LbClient.Disconnect();
            }
        }
    }
}