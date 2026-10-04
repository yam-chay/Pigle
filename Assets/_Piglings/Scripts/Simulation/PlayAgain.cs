using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Piglings.Simulation
{
    /// <summary>
    /// Once the night is decided (NightState.Ended), the next click reloads the active scene —
    /// a fresh NightSession, a fresh night. NightEndView shows "Click to play again" off the same flag.
    ///
    /// Why a scene reload: everything a night owns lives in the scene (no statics, no
    /// DontDestroyOnLoad), so reloading is the whole reset — nothing to clear by hand.
    /// Lives in Simulation because it changes what's running; views only read.
    ///
    /// Night.unity only. In a campaign scene NightFlow owns restarting (Retry / Next night save first, then reload), so
    /// this stands down: left in, it reloaded on the PRESS of the Next night button — before the button's click (on
    /// release) could save the next night — and the player never advanced.
    /// </summary>
    public sealed class PlayAgain : MonoBehaviour
    {
        [SerializeField] private NightSession session;

        [Tooltip("Seconds after the night ends during which clicks are ignored, so a click meant as a throw " +
                 "(or spammed while the last chain lands) doesn't skip the result.")]
        [SerializeField, Min(0f)] private float ignoreClicksFor = 0.75f;

        private float _endedAt = -1f;   // Time.time when we first saw the night end; -1 = still running

        private void Start()
        {
            if (session.IsCampaign)
                Debug.LogWarning("PlayAgain: this is a campaign scene — NightFlow's Retry / Next night restart the night, so " +
                                 "PlayAgain does nothing here. Remove it from this scene.", this);
        }

        private void Update()
        {
            if (session.IsCampaign) return;   // the campaign flow restarts (see the summary)
            if (!session.State.Ended) return;
            if (_endedAt < 0f) _endedAt = Time.time;
            if (Time.time < _endedAt + ignoreClicksFor) return;

            // A fresh press, not a release: a button held down since the night ended doesn't count.
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
