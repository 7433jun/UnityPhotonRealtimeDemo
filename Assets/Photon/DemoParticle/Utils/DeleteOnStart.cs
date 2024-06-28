// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Photon Engine">Photon Engine 2024</copyright>
// <summary></summary>
// <author>developer@photonengine.com</author>
// --------------------------------------------------------------------------------------------------------------------


namespace Photon.Utils
{
    using UnityEngine;


    /// <summary>In Start() this destroys the GameObject it is attached to. Use case: remove components that should be visible in-Editor only.</summary>
    public class DeleteOnStart : MonoBehaviour
    {
        public void Start()
        {
            Destroy(this.gameObject);
        }
    }
}
