using UnityEngine;

namespace VRMovementTracker
{
    /// <summary>
    /// Simple component that displays a message in the Inspector.
    /// Used by the scene setup editor to leave instructions for the developer.
    /// Safe to remove once setup is complete.
    /// </summary>
    public class InfoTag : MonoBehaviour
    {
        [Header("SETUP INSTRUCTIONS (delete this component when done)")]
        [TextArea(5, 15)]
        public string message = "";
    }
}
