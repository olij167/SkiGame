using UnityEngine;
using SkiGame.Tricks;

namespace SkiGame.Progression
{
    [DisallowMultipleComponent]
    public sealed class QuestTrickSignalSource : MonoBehaviour
    {
        [SerializeField] private QuestSignalBus signalBus;
        [SerializeField] private SkierTrickTracker trickTracker;

        private void OnEnable()
        {
            ResolveReferences();
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Update()
        {
            if (trickTracker == null)
            {
                ResolveReferences();
                Bind();
            }
        }

        private void HandleLiveTrickUpdated(SkierTrickTracker.TrickLiveState state)
        {
            if (signalBus == null)
                return;

            signalBus.SetState("trick.active", state.active);
            signalBus.SetStat("trick.air_time_live", state.airtime);
            signalBus.SetText("trick.name_live", state.displayName ?? string.Empty);
        }

        private void HandleTrickResolved(SkierTrickTracker.TrickResult result)
        {
            if (signalBus == null)
                return;

            TrickDescriptor descriptor = TrickActivityRules.CreateDescriptor(result);
            QuestSignalData eventData = QuestSignalData.Create()
                .WithTag("success", result.success ? "true" : "false")
                .WithTag("name", result.displayName ?? string.Empty)
                .WithTag("usedTuck", result.usedTuck ? "true" : "false")
                .WithTag("usedGrind", result.usedGrind ? "true" : "false")
                .WithTag("usedSlide", result.usedSlide ? "true" : "false")
                .WithTag("usedStyle", result.usedStyle ? "true" : "false")
                .WithTag("usedValidPose", result.usedValidPose ? "true" : "false")
                .WithTag("poseOnly", result.poseOnly ? "true" : "false")
                .WithTag("poseRotationCombo", result.poseRotationCombo ? "true" : "false")
                .WithTag("primaryPoseLabel", result.primaryPoseLabel ?? string.Empty)
                .WithTag("poseLabels", TrickActivityRules.JoinList(result.poseLabels))
                .WithTag("primaryPoseFamily", result.primaryPoseFamily.ToString())
                .WithTag("poseFamilies", TrickActivityRules.JoinList(result.poseFamilies))
                .WithTag("primaryPoseShape", result.primaryPoseShape.ToString())
                .WithTag("poseShapes", TrickActivityRules.JoinList(result.poseShapes))
                .WithTag("primaryOrientation", result.primaryOrientationModifier.ToString())
                .WithTag("orientations", TrickActivityRules.JoinList(result.orientationModifiers))
                .WithTag("spinDegrees", result.spinDegrees.ToString())
                .WithTag("spinDirection", result.spinDirection.ToString())
                .WithTag("flipCount", result.flipCount.ToString())
                .WithTag("frontflip", result.frontflip ? "true" : "false")
                .WithTag("backflip", result.backflip ? "true" : "false")
                .WithTag("switchLanding", result.switchLanding ? "true" : "false")
                .WithTag("noseLanding", result.noseLanding ? "true" : "false")
                .WithTag("tailLanding", result.tailLanding ? "true" : "false")
                .WithTag("hadBounce", result.hadBounceFollowup ? "true" : "false")
                .WithTag("hadTipBounce", result.hadTipBounceFollowup ? "true" : "false")
                .WithTag("comboVariety", result.comboVariety.ToString())
                .WithTag("comboScore", result.comboScore.ToString())
                .WithTag("zoneId", result.zoneId ?? string.Empty)
                .WithTag("signature", string.IsNullOrWhiteSpace(result.canonicalSignature) ? TrickActivityRules.BuildCanonicalSignature(descriptor) : result.canonicalSignature)
                .WithTag("progressionTier", result.progressionTier.ToString())
                .WithNumeric(result.airtime);

            signalBus.RaiseEvent(result.success ? "trick.landed" : "trick.failed", eventData);
            signalBus.AddStat("trick.score.total", result.comboScore);
            signalBus.SetStat("trick.score.last", result.comboScore);

            if (result.success && !string.IsNullOrWhiteSpace(result.displayName))
            {
                signalBus.RaiseEvent(
                    "trick.named",
                    QuestSignalData.Create()
                        .WithTag("success", "true")
                        .WithTag("name", !string.IsNullOrWhiteSpace(result.primaryPoseLabel) ? result.primaryPoseLabel : result.displayName)
                        .WithTag("primaryPoseLabel", result.primaryPoseLabel ?? string.Empty)
                        .WithTag("poseLabels", TrickActivityRules.JoinList(result.poseLabels))
                        .WithTag("primaryPoseFamily", result.primaryPoseFamily.ToString())
                        .WithTag("poseFamilies", TrickActivityRules.JoinList(result.poseFamilies))
                        .WithTag("primaryPoseShape", result.primaryPoseShape.ToString())
                        .WithTag("poseShapes", TrickActivityRules.JoinList(result.poseShapes))
                        .WithTag("primaryOrientation", result.primaryOrientationModifier.ToString())
                        .WithTag("orientations", TrickActivityRules.JoinList(result.orientationModifiers))
                        .WithTag("spinDegrees", result.spinDegrees.ToString())
                        .WithTag("spinDirection", result.spinDirection.ToString())
                        .WithTag("flipCount", result.flipCount.ToString())
                        .WithTag("frontflip", result.frontflip ? "true" : "false")
                        .WithTag("backflip", result.backflip ? "true" : "false")
                        .WithTag("usedTuck", result.usedTuck ? "true" : "false")
                        .WithTag("usedGrind", result.usedGrind ? "true" : "false")
                        .WithTag("usedSlide", result.usedSlide ? "true" : "false")
                        .WithTag("usedStyle", result.usedStyle ? "true" : "false")
                        .WithTag("usedValidPose", result.usedValidPose ? "true" : "false")
                        .WithTag("poseOnly", result.poseOnly ? "true" : "false")
                        .WithTag("poseRotationCombo", result.poseRotationCombo ? "true" : "false")
                        .WithTag("switchLanding", result.switchLanding ? "true" : "false")
                        .WithTag("noseLanding", result.noseLanding ? "true" : "false")
                        .WithTag("tailLanding", result.tailLanding ? "true" : "false")
                        .WithTag("comboVariety", result.comboVariety.ToString())
                        .WithTag("zoneId", result.zoneId ?? string.Empty)
                        .WithTag("signature", string.IsNullOrWhiteSpace(result.canonicalSignature) ? TrickActivityRules.BuildCanonicalSignature(descriptor) : result.canonicalSignature));
            }

            if (result.success && result.usedValidPose)
            {
                signalBus.RaiseEvent(
                    "trick.pose_landed",
                    QuestSignalData.Create()
                        .WithTag("name", result.primaryPoseLabel ?? string.Empty)
                        .WithTag("poseFamily", result.primaryPoseFamily.ToString())
                        .WithTag("poseShape", result.primaryPoseShape.ToString())
                        .WithTag("orientation", result.primaryOrientationModifier.ToString())
                        .WithTag("usedTuck", result.usedTuck ? "true" : "false")
                        .WithTag("usedGrind", result.usedGrind ? "true" : "false")
                        .WithTag("usedSlide", result.usedSlide ? "true" : "false")
                        .WithTag("usedStyle", result.usedStyle ? "true" : "false"));
            }
        }

        private void ResolveReferences()
        {
            if (signalBus == null)
                signalBus = FindObjectOfType<QuestSignalBus>();

            if (trickTracker == null)
                trickTracker = FindObjectOfType<SkierTrickTracker>();
        }

        private void Bind()
        {
            if (trickTracker == null)
                return;

            Unbind();
            trickTracker.OnLiveTrickUpdated += HandleLiveTrickUpdated;
            trickTracker.OnTrickResolved += HandleTrickResolved;
        }

        private void Unbind()
        {
            if (trickTracker == null)
                return;

            trickTracker.OnLiveTrickUpdated -= HandleLiveTrickUpdated;
            trickTracker.OnTrickResolved -= HandleTrickResolved;
        }
    }
}
