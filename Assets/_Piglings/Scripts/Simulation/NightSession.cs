using Piglings.Definitions;
using Piglings.Events;
using Piglings.Rules;
using Piglings.Runtime;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// Owner of one night. Lives in the Night scene and dies with it — no persistence,
    /// no DontDestroyOnLoad, no static access. Creates the engine-free layers
    /// (EventBus, NightState, Rules) and exposes them to scene objects that reference it.
    /// Scene objects get it through a serialized field, never through a lookup.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class NightSession : MonoBehaviour
    {
        [SerializeField] private NightDefinition night;

        public NightDefinition Night => night;
        public EventBus Bus { get; private set; }
        public NightState State { get; private set; }
        public IdAllocator Ids { get; private set; }

        private ChainTracker _chains;

        private void Awake()
        {
            Bus = new EventBus();
            State = new NightState();
            Ids = new IdAllocator();
            _chains = new ChainTracker(Bus, State);
        }

        private void OnDestroy() => _chains?.Dispose();
    }
}
