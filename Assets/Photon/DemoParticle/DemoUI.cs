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
    using System;
    using System.Linq;
    using Photon.Realtime;
    using System.Collections.Generic;
    using UnityEngine;
    using TMPro;
    using System.Collections;
    #if SUPPORTED_UNITY
    using Hashtable = ExitGames.Client.Photon.Hashtable;
    #endif


    /// <summaryIntegrates the GameLogic into Unity.</summary>
    /// <remarks>
    /// As the GameLogic itself is independent from Unity, this integrates the logic into the engine's main loop.
    /// The UI handles events from buttons/toggles and updates the visual representation of where players are.
    ///
    /// When Interest Groups are being used, the corresponding quad gets a different material.
    /// </remarks>
    public class DemoUI : MonoBehaviour
    {
        public AppSettings PhotonAppSettings = new AppSettings();   // set in inspector

        public GameLogic ActiveGameLogic { get; private set; }

        public GameObject[] GroundQuads; // set via inspector
        public Material GroundGridMat;
        public Material GroundGridInterestMat;
        public Material CubeMaterial;

        public TextMeshPro DemoTitle;
        public GameObject PlayerPrefab;

        private readonly Dictionary<int, GameObject> cubes = new Dictionary<int, GameObject>();

        public static bool ShowUserInfo { get; private set; }


        private float inputRepeatTimeout;
        private const float inputRepeatTimeoutSetting = 0.1f; // every how often do we apply input to movement?

        private bool roomNameSet;


        private MultiClientRunner BackgroundClientRunner;

        void Start()
        {
            ShowUserInfo = true;

            if (string.IsNullOrEmpty(this.PhotonAppSettings.AppIdRealtime))
            {
                Debug.LogError("Can not run without AppId setup correctly. Configure it in Demo UI component in the scene.");
                return;
            }

            this.ActiveGameLogic = new GameLogic();
            this.ActiveGameLogic.CallConnect(this.PhotonAppSettings);

            this.DemoTitle.alignment = TextAlignmentOptions.Bottom;
        }


        /// <summary>As precaution in the Unity Editor, you should always do a proper disconnect. Else, the Editor might become unresponsive.</summary>
        public void OnApplicationQuit()
        {
            if (this.ActiveGameLogic != null)
            {
                this.ActiveGameLogic.LbClient.Disconnect();
            }
        }


        #region handling of UI events (buttons and toggles)


        public void ToggleAutoMove(bool change)
        {
            this.ActiveGameLogic.MoveInterval.IsEnabled = change;
        }

        public void ToggleShowUserInfo(bool change)
        {
            ShowUserInfo = change;

            if (this.ActiveGameLogic == null || !this.ActiveGameLogic.LbClient.InRoom)
            {
                return;
            }

            foreach (GameObject cube in this.cubes.Values)
            {
                TMP_Text txt = cube.GetComponentInChildren<TextMeshPro>();
                txt.enabled = ShowUserInfo;
            }
        }

        public void ToggleUseInterestGroups(bool change)
        {
            this.ActiveGameLogic.SetUseInterestGroups(change);

            if (this.BackgroundClientRunner != null)
            {
                this.BackgroundClientRunner.ToggleInterestGroupUsage(change);
            }
        }

        public void ButtonChangeColor()
        {
            this.ActiveGameLogic.ChangeLocalPlayerColor();
        }

        public void ButtonChangeGridSize()
        {
            this.ActiveGameLogic.ChangeGridSize();
        }

        public void ButtonAddClient()
        {

            if (this.BackgroundClientRunner == null)
            {
                this.BackgroundClientRunner = this.gameObject.AddComponent<MultiClientRunner>();
            }

            this.BackgroundClientRunner.AddClient(this.PhotonAppSettings);
        }

        public void ButtonRemoveClient()
        {
            this.BackgroundClientRunner.RemoveClient();
        }


        #endregion


        public void Update()
        {
            // update the local game logic and visuals
            if (this.ActiveGameLogic != null)
            {
                this.ActiveGameLogic.UpdateLoop();

                if (!this.ActiveGameLogic.LbClient.InRoom)
                {
                    this.DemoTitle.text = this.ActiveGameLogic.LbClient.State.ToString();
                }
                else
                {
                    if (!this.roomNameSet)
                    {
                        this.roomNameSet = true;
                        this.DemoTitle.text = this.ActiveGameLogic.LbClient.CurrentRoom.ToString();
                    }
                }

                this.InputForControlledCube();
                this.RenderPlayers();
            }


            // Exit the application when 'Back' button is pressed
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Application.Quit();
            }
        }


        // must be connected and in a room (because this uses logic.ActiveGameLogic.LocalPlayer).
        private void InputForControlledCube()
        {
            if (this.ActiveGameLogic == null || !this.ActiveGameLogic.LbClient.InRoom)
            {
                return;
            }

            if (this.inputRepeatTimeout > 0)
            {
                this.inputRepeatTimeout -= Time.deltaTime;
                return;
            }


            float x = Input.GetAxis("Horizontal");
            float y = Input.GetAxis("Vertical");

            bool movedX = Math.Abs(x) > 0.2f;
            bool movedY = Math.Abs(y) > 0.2f;
            if (!movedX && !movedY)
            {
                return;
            }


            // applying the actual movement
            if (movedX)
            {
                this.ActiveGameLogic.LocalPlayer.PosX += (x < 0) ? -1 : 1;
            }
            if (movedY)
            {
                this.ActiveGameLogic.LocalPlayer.PosY += (y < 0) ? -1 : 1;
            }

            // this demo just clamps the position of the player to the grid
            this.ActiveGameLogic.LocalPlayer.ClampPosition();


            // set a brief delay before movement input can be repeated and reset auto move ("nice to have" things)
            this.ActiveGameLogic.MoveInterval.Reset();
            this.inputRepeatTimeout += inputRepeatTimeoutSetting;
        }



        // Set the playground texture
        // Current texture depends on interest management setting
        public void SetPlaygroundTexture()
        {
            int textureScale = this.ActiveGameLogic.GridSize / 2; // the ground is split into 2x2 quads, each representing one of the 4 interest groups (when used)

            this.GroundGridMat.mainTextureScale = new Vector2(textureScale, textureScale);
            this.GroundGridInterestMat.mainTextureScale = new Vector2(textureScale, textureScale);

            for (int i = 0; i < this.GroundQuads.Length; i++)
            {
                GameObject quad = this.GroundQuads[i];
                Renderer renderer = quad.GetComponent<Renderer>();

                renderer.material = this.ActiveGameLogic.UseInterestGroups && i + 1 == this.ActiveGameLogic.LocalPlayer.VisibleGroup ? this.GroundGridInterestMat : this.GroundGridMat;
            }
        }



        /// <summary>
        /// Render cubes onto the scene
        /// </summary>
        void RenderPlayers()
        {
            if (this.ActiveGameLogic == null || !this.ActiveGameLogic.LbClient.InRoom)
            {
                return;
            }


            this.SetPlaygroundTexture();


            lock (this.ActiveGameLogic)
            {
                int currentScale = Constants.GridSizeMax / this.ActiveGameLogic.GridSize;
                Vector3 scale = new Vector3(currentScale, currentScale, currentScale);

                foreach (ParticlePlayer p in this.ActiveGameLogic.ParticlePlayers.Values)
                {
                    GameObject cube = null;
                    bool found = this.cubes.TryGetValue(p.ActorNumber, out cube);

                    if (!found)
                    {
                        cube = Instantiate(this.PlayerPrefab);
                        TMP_Text txt = cube.GetComponentInChildren<TextMeshPro>();
                        txt.text = p.NickName;
                        txt.enabled = ShowUserInfo;
                        this.cubes.Add(p.ActorNumber, cube);
                    }


                    float alpha = 1.0f;
                    if (!p.IsLocal && p.UpdateAge > 500)
                    {
                        alpha = (p.UpdateAge > 1000) ? 0.3f : 0.8f;
                    }

                    Color cubeColor = IntToColor(p.Color);
                    cube.GetComponent<Renderer>().material.color = new Color(cubeColor.r, cubeColor.g, cubeColor.b, alpha);


                    cube.transform.localScale = scale;
                    cube.transform.position = new Vector3(p.PosX * scale.x + scale.x / 2, scale.z / 2, p.PosY * scale.y + scale.y / 2);
                }


                // check if there are more cubes than players. if so, find the cubes of players who are no longer around (by actorNumber).
                // as this UI deliberately has no access to the callbacks of the GameLogic, this gets solved by looping through the cubes.
                // the benefit is we can run the GameLogic independent from any visuals and Unity itself.
                if (this.cubes.Count != this.ActiveGameLogic.ParticlePlayers.Count)
                {
                    HashSet<int> keysToRemove = new HashSet<int>();
                    foreach (int cubesKey in this.cubes.Keys)
                    {
                        if (!this.ActiveGameLogic.ParticlePlayers.Keys.Contains(cubesKey))
                        {
                            keysToRemove.Add(cubesKey);
                        }
                    }

                    foreach (int i in keysToRemove)
                    {
                        Destroy(this.cubes[i].gameObject);
                        this.cubes.Remove(i);
                    }
                }
            }
        }



        /// <summary>
        /// Convert integer value to Color
        /// </summary>
        public static Color IntToColor(int colorValue)
        {
            float r = (byte)(colorValue >> 16) / 255.0f;
            float g = (byte)(colorValue >> 8) / 255.0f;
            float b = (byte)colorValue / 255.0f;

            return new Color(r, g, b);
        }
    }
}