using UnityEngine;

namespace SkiGame.Progression
{
    [DisallowMultipleComponent]
    public sealed class QuestContextProvider : MonoBehaviour
    {
        [SerializeField] private QuestSignalBus signalBus;
        [SerializeField] private SkiController skiController;
        [SerializeField] private WalkingController walkingController;
        [SerializeField] private SorenessMeter sorenessMeter;
        [SerializeField] private SkiResortHoldInteractor skiResortInteractor;
        [SerializeField] private LiftRider liftRider;

        private void Awake()
        {
            ResolveReferences();
        }

        public float ReadFloat(string key)
        {
            ResolveReferences();
            string normalized = QuestSignalBus.NormalizeKey(key);

            switch (normalized)
            {
                case "player.speed":
                    if (skiController != null)
                    {
                        Vector3 normal = skiController.GroundNormal.sqrMagnitude > 0.0001f ? skiController.GroundNormal : Vector3.up;
                        return Vector3.ProjectOnPlane(skiController.Velocity, normal).magnitude;
                    }
                    break;

                case "player.soreness":
                    return sorenessMeter != null ? sorenessMeter.Soreness01 : 0f;
            }

            return signalBus != null ? signalBus.ReadStat(normalized) : 0f;
        }

        public bool ReadBool(string key)
        {
            ResolveReferences();
            string normalized = QuestSignalBus.NormalizeKey(key);

            switch (normalized)
            {
                case "player.airborne":
                    return skiController != null && skiController.IsAirborne;
                case "player.grounded":
                    return skiController != null && skiController.IsRiderGrounded;
                case "player.on_lift":
                    return liftRider != null && liftRider.IsAttached;
                case "player.in_resort":
                    return skiResortInteractor != null && skiResortInteractor.IsPlayerInsideResort;
                case "player.skis_equipped":
                    return walkingController != null ? walkingController.SkisOn : skiController != null && skiController.enabled;
                case "player.stacked":
                    return skiController != null && skiController.IsStacked;
            }

            return signalBus != null && signalBus.ReadState(normalized);
        }

        public string ReadString(string key)
        {
            ResolveReferences();
            string normalized = QuestSignalBus.NormalizeKey(key);
            return signalBus != null ? signalBus.ReadText(normalized) : string.Empty;
        }

        private void ResolveReferences()
        {
            if (signalBus == null)
                signalBus = FindObjectOfType<QuestSignalBus>();

            if (skiController == null)
                skiController = FindObjectOfType<SkiController>();

            if (walkingController == null)
                walkingController = FindObjectOfType<WalkingController>();

            if (sorenessMeter == null && skiController != null)
                sorenessMeter = skiController.GetComponent<SorenessMeter>();

            if (skiResortInteractor == null)
                skiResortInteractor = FindObjectOfType<SkiResortHoldInteractor>();

            if (liftRider == null)
                liftRider = FindObjectOfType<LiftRider>();
        }
    }
}
