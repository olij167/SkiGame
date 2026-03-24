using System.Collections;
using UnityEngine;

namespace SkiGame.Progression
{
    [DisallowMultipleComponent]
    public sealed class SkiLessonCrashCue : MonoBehaviour
    {
        [SerializeField] private SkiController playerSkiController;

        [Header("Spawning")]
        [SerializeField] private float behindCameraDistance = 18f;
        [SerializeField] private float minBehindCameraDistance = 12f;
        [SerializeField] private float lateralSpawnJitter = 1.75f;
        [SerializeField] private float spawnHeightOffset = 0.2f;
        [SerializeField] private float searchRadius = 160f;

        [Header("Impact")]
        [SerializeField] private float preLaunchDelaySeconds = 0.05f;
        [SerializeField] private float impactSpeed = 65f;
        [SerializeField] private float steeringStrength = 60f;
        [SerializeField] private float maxCueDuration = 0.75f;
        [SerializeField] private float guaranteedHitDistance = 2.2f;
        [SerializeField] private float forcedImpactSeverity = 1f;

        [Header("Restore")]
        [SerializeField] private float restoreDelaySeconds = 2.5f;

        private bool _cueActive;

        public bool IsCueActive => _cueActive;

        public bool TryTriggerCue()
        {
            if (_cueActive)
                return false;

            if (playerSkiController == null)
                playerSkiController = FindObjectOfType<SkiController>();

            if (playerSkiController == null)
                return false;

            NpcSkierBrain npc = FindBestActivePooledNpc();
            if (npc == null)
                return false;

            StartCoroutine(RunCrashCue(npc));
            return true;
        }

        private NpcSkierBrain FindBestActivePooledNpc()
        {
            // In the currently uploaded code, I do not see a spawner pool API exposed here.
            // So the safest reliable option is to re-use an already active NPC that is part of the live spawned population.
            var npcs = FindObjectsOfType<NpcSkierBrain>(includeInactive: false);

            NpcSkierBrain best = null;
            float bestDistSq = float.PositiveInfinity;
            Vector3 playerPos = playerSkiController.transform.position;

            for (int i = 0; i < npcs.Length; i++)
            {
                var npc = npcs[i];
                if (npc == null || !npc.isActiveAndEnabled)
                    continue;

                var npcSki = npc.GetComponent<SkiController>();
                var npcRb = npc.GetComponent<Rigidbody>();
                if (npcSki == null || npcRb == null || npcSki.IsStacked)
                    continue;

                float dSq = (npc.transform.position - playerPos).sqrMagnitude;
                if (dSq > searchRadius * searchRadius)
                    continue;

                if (dSq < bestDistSq)
                {
                    best = npc;
                    bestDistSq = dSq;
                }
            }

            return best;
        }

        private void ForceImpactNow(NpcSkierBrain npc, SkiController npcSki, Rigidbody npcRb)
        {
            if (playerSkiController == null || npcSki == null)
                return;

            Vector3 playerPos = playerSkiController.transform.position;
            Vector3 playerForwardFlat = Vector3.ProjectOnPlane(playerSkiController.transform.forward, Vector3.up).normalized;
            if (playerForwardFlat.sqrMagnitude < 0.001f)
                playerForwardFlat = Vector3.forward;

            Vector3 impactPoint = playerPos - playerForwardFlat * 0.35f + Vector3.up * 0.35f;
            Vector3 incomingVelocity = playerForwardFlat * impactSpeed;

            // Place the NPC right into the player's back path to make the collision visually line up.
            npcSki.TeleportToSpawn(
                playerPos - playerForwardFlat * 0.65f + Vector3.up * 0.1f,
                Quaternion.LookRotation(playerForwardFlat, Vector3.up),
                snapToGround: true);

            if (npcRb != null)
            {
                npcRb.linearVelocity = playerForwardFlat * impactSpeed;
                npcRb.angularVelocity = Vector3.zero;
            }

            // Guaranteed lesson outcome even if the physics contact is missed by a frame.
            playerSkiController.TriggerImpactStackFromPoint(impactPoint, incomingVelocity, forcedImpactSeverity, "TutorialSorenessCrash");

            var soreness = playerSkiController.GetComponent<SorenessMeter>();
            if (soreness != null)
                soreness.AddImpact(forcedImpactSeverity);
        }

        private IEnumerator RunCrashCue(NpcSkierBrain npc)
        {
            _cueActive = true;

            SkiController npcSki = npc.GetComponent<SkiController>();
            Rigidbody npcRb = npc.GetComponent<Rigidbody>();
            NpcSkierRunFollower follower = npc.GetComponent<NpcSkierRunFollower>();
            WalkingController walking = npc.GetComponent<WalkingController>();

            if (npcSki == null || npcRb == null || playerSkiController == null)
            {
                _cueActive = false;
                yield break;
            }

            if (follower != null)
                follower.SetInputEnabled(false);

            if (walking != null)
                walking.ClearExternalMove();

            npc.enabled = false;

            Camera cam = Camera.main;
            Transform camTransform = cam != null ? cam.transform : playerSkiController.transform;

            Vector3 playerPos = playerSkiController.transform.position;
            Vector3 camForwardFlat = Vector3.ProjectOnPlane(camTransform.forward, Vector3.up).normalized;
            if (camForwardFlat.sqrMagnitude < 0.001f)
                camForwardFlat = Vector3.ProjectOnPlane(playerSkiController.transform.forward, Vector3.up).normalized;

            Vector3 camRightFlat = Vector3.Cross(Vector3.up, camForwardFlat).normalized;

            float spawnDistance = Mathf.Max(minBehindCameraDistance, behindCameraDistance);
            float lateral = Random.Range(-lateralSpawnJitter, lateralSpawnJitter);

            Vector3 spawnPos =
                playerPos
                - camForwardFlat * spawnDistance
                + camRightFlat * lateral
                + Vector3.up * spawnHeightOffset;

            Vector3 initialLook = Vector3.ProjectOnPlane(playerPos - spawnPos, Vector3.up).normalized;
            if (initialLook.sqrMagnitude < 0.001f)
                initialLook = camForwardFlat;

            npcSki.TeleportToSpawn(spawnPos, Quaternion.LookRotation(initialLook, Vector3.up), snapToGround: true);
            npcRb.linearVelocity = Vector3.zero;
            npcRb.angularVelocity = Vector3.zero;

            yield return new WaitForSeconds(preLaunchDelaySeconds);

            float timer = 0f;
            bool impactResolved = false;

            while (timer < maxCueDuration && npc != null && npcRb != null && playerSkiController != null)
            {
                Vector3 targetPos = playerSkiController.transform.position + playerSkiController.Velocity * 0.08f;
                Vector3 toPlayer = Vector3.ProjectOnPlane(targetPos - npc.transform.position, Vector3.up);
                float dist = toPlayer.magnitude;

                Vector3 dir = dist > 0.001f ? toPlayer / dist : initialLook;

                npc.transform.rotation = Quaternion.Slerp(
                    npc.transform.rotation,
                    Quaternion.LookRotation(dir, Vector3.up),
                    Time.deltaTime * steeringStrength);

                npcRb.linearVelocity = dir * impactSpeed;

                if (dist <= guaranteedHitDistance)
                {
                    ForceImpactNow(npc, npcSki, npcRb);
                    impactResolved = true;
                    break;
                }

                timer += Time.deltaTime;
                yield return null;
            }

            if (!impactResolved && npc != null && npcSki != null && npcRb != null && playerSkiController != null)
            {
                ForceImpactNow(npc, npcSki, npcRb);
                yield return new WaitForSeconds(0.1f);
            }

            yield return new WaitForSeconds(restoreDelaySeconds);

            if (follower != null)
                follower.SetInputEnabled(true);

            if (npc != null)
                npc.enabled = true;

            _cueActive = false;
        }
    }
}