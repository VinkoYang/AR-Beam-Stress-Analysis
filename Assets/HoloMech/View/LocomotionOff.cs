using Oculus.Interaction;
using Oculus.Interaction.Locomotion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HoloMech
{
    /// <summary>
    /// Switches off the Interactions Rig's teleport and turning in every loaded scene. They listen to the thumbsticks,
    /// which <see cref="GrabStick"/> uses while something is grabbed, and in passthrough a snap turn or teleport moves
    /// all virtual content against the room, which breaks an on-site alignment. <c>_velocityDisabled</c>
    /// (HoloMechQuestSceneBuilder.DisableLocomotion) only stops sliding and gravity, not these events.
    /// Disabling a <see cref="LocomotionEventsConnection"/> unsubscribes it from its broadcasters; the interactors are
    /// disabled too so no teleport arc or turn arrow is drawn.
    /// </summary>
    public static class LocomotionOff
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

        /// <summary>Disables every locomotion connection, teleport interactor and turner interactor; returns how many.</summary>
        public static int Apply()
        {
            int n = 0;
            n += Disable<LocomotionEventsConnection>();
            n += Disable<TeleportInteractor>();
            n += Disable<LocomotionTurnerInteractor>();
            n += Disable<LocomotionAxisTurnerInteractor>();
            if (n > 0) Debug.Log($"[LocomotionOff] disabled {n} locomotion components (teleport / turn)");
            return n;
        }

        static int Disable<T>() where T : MonoBehaviour
        {
            int n = 0;
            foreach (var c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!c.enabled) continue;
                c.enabled = false;
                n++;
            }
            return n;
        }
    }
}
