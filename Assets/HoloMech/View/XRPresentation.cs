using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace HoloMech
{
    /// <summary>
    /// Mixed-reality presentation (milestone 5): the same panels on a world-space canvas — a board about 2 m wide in front
    /// of the user — with the beam hologram floating in front of the board's free centre area, as in the paper's
    /// HoloLens screenshots. UI input goes through XRI (tracked-device raycaster), so touch on a phone (screen-space ray
    /// interactor) and rays or pokes on a headset both work. The Pin button toggles XRI's <see cref="LazyFollow"/>:
    /// pinned, the board stays where it is; unpinned, it drifts to stay in front of the user.
    /// </summary>
    public class XRPresentation : MonoBehaviour
    {
        [Tooltip("Board width in metres (the 1920-px-wide canvas is scaled to this).")]
        [SerializeField] float boardWidth = 2.1f;

        public Vector3 StartOffset { get => startOffset; set => startOffset = value; }
        public float BeamLength { get => beamLength; set => beamLength = value; }

        [Tooltip("Where the board appears relative to the user's head on start: right, up, forward (m).")]
        [SerializeField] Vector3 startOffset = new Vector3(0f, -0.2f, 2.6f);

        [Tooltip("Beam hologram length in metres, whatever the real beam length.")]
        [SerializeField] float beamLength = 0.7f;

        Transform board;
        Canvas canvas;
        HoloMechBeamView beam;
        LazyFollow follow;
        bool pinned = true;

        public bool Pinned => pinned;
        public Transform Board => board;

        /// <summary>Turns the app's screen canvas into a world-space board and parents the beam to it.</summary>
        public void Setup(Canvas appCanvas, HoloMechBeamView beamView)
        {
            canvas = appCanvas;
            beam = beamView;

            var rt = (RectTransform)canvas.transform;
            if (canvas.renderMode == RenderMode.WorldSpace && rt.parent != null)
            {
                // Board prepared in the scene (Quest): its canvas already carries the headset's UI interaction.
                board = rt.parent;
                boardWidth = rt.sizeDelta.x * rt.localScale.x;
                PlaceInFrontOfUser();
            }
            else
            {
                board = new GameObject("HoloMech Board").transform;
                PlaceInFrontOfUser();

                canvas.renderMode = RenderMode.WorldSpace;
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null) scaler.enabled = false;
                rt.SetParent(board, false);
                rt.sizeDelta = new Vector2(1920, 1080);
                rt.localScale = Vector3.one * (boardWidth / 1920f);
                rt.localPosition = Vector3.zero;
                rt.localRotation = Quaternion.identity;
                if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null) canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            }

            canvas.worldCamera = Camera.main;

            // Beam: in front of the board, centred over the free area (upper middle of the board).
            var bt = beam.transform;
            bt.SetParent(board, false);
            float s = beamLength / beam.DisplayLength;
            bt.localScale = Vector3.one * s;
            float boardHeight = boardWidth * 1080f / 1920f;
            bt.localPosition = new Vector3(-beamLength / 2f, boardHeight * 0.17f, -0.25f);
            bt.localRotation = Quaternion.identity;

            follow = board.gameObject.AddComponent<LazyFollow>();
            follow.positionFollowMode = LazyFollow.PositionFollowMode.Follow;
            follow.rotationFollowMode = LazyFollow.RotationFollowMode.LookAtWithWorldUp;
            follow.targetOffset = startOffset;
            follow.enabled = !pinned;

            // LazyFollow snaps to its default offset when added, and XR Simulation / device tracking sets the head
            // pose during the first frames: place the board again once the head has settled.
            StartCoroutine(PlaceWhenTracked());
        }

        IEnumerator PlaceWhenTracked()
        {
            for (int i = 0; i < 15; i++) yield return null;
            PlaceInFrontOfUser();
        }

        /// <summary>Pin: stay put. Unpin: follow the user's head (lazily, as in MRTK's follow behaviour).</summary>
        public void TogglePin() => SetPinned(!pinned);

        /// <summary>Raised when Pin / Follow changes (also when grabbing the Quest window pins it).</summary>
        public event System.Action PinnedChanged;

        public void SetPinned(bool value)
        {
            if (pinned == value) return;
            pinned = value;
            if (follow != null) follow.enabled = !pinned;
            PinnedChanged?.Invoke();
        }

        /// <summary>Puts the board straight in front of the head, facing the user.</summary>
        public void PlaceInFrontOfUser()
        {
            var cam = Camera.main;
            if (board == null || cam == null) return;
            var head = cam.transform;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            board.position = head.position + right * startOffset.x + Vector3.up * startOffset.y + forward * startOffset.z;
            board.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
