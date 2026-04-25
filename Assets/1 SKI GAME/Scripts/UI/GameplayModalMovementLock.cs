using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.UI
{
    public static class GameplayModalMovementLock
    {
        private static readonly HashSet<object> Owners = new();

        private static WalkingController _walkingController;
        private static SkiController _skiController;
        private static Rigidbody _playerRigidbody;

        private static bool _capturedWalkingEnabled;
        private static bool _capturedSkiEnabled;
        private static bool _stateCaptured;

        public static void SetLocked(object owner, bool locked)
        {
            SetLocked(owner, locked, null);
        }

        public static void SetLocked(object owner, bool locked, GameObject playerRoot)
        {
            if (owner == null)
                return;

            if (locked)
            {
                if (!Owners.Add(owner))
                    return;

                if (Owners.Count == 1)
                    ApplyLock(playerRoot);

                return;
            }

            if (!Owners.Remove(owner))
                return;

            if (Owners.Count == 0)
                ReleaseLock();
        }

        public static bool IsLocked => Owners.Count > 0;

        private static void ApplyLock(GameObject playerRoot)
        {
            ResolvePlayerComponents(playerRoot);

            if (_walkingController == null && _skiController == null)
                return;

            _capturedWalkingEnabled = _walkingController != null && _walkingController.ControlsEnabled;
            _capturedSkiEnabled = _skiController != null && _skiController.enabled;
            _stateCaptured = true;

            _walkingController?.ClearExternalMove();

            if (_walkingController != null)
                _walkingController.ControlsEnabled = false;

            if (_skiController != null)
                _skiController.enabled = false;

            if (_playerRigidbody != null)
            {
                _playerRigidbody.isKinematic = false;
                _playerRigidbody.linearVelocity = Vector3.zero;
                _playerRigidbody.angularVelocity = Vector3.zero;
            }
        }

        private static void ReleaseLock()
        {
            if (!_stateCaptured)
            {
                ClearCapturedReferences();
                return;
            }

            if (_walkingController != null)
                _walkingController.ControlsEnabled = _capturedWalkingEnabled;

            if (_skiController != null)
            {
                bool shouldEnableSki = _capturedSkiEnabled;

                if (_walkingController != null)
                    shouldEnableSki &= _walkingController.SkisOn;

                _skiController.enabled = shouldEnableSki;
            }

            _stateCaptured = false;
            ClearCapturedReferences();
        }

        private static void ResolvePlayerComponents(GameObject playerRoot)
        {
            _walkingController = null;
            _skiController = null;
            _playerRigidbody = null;

            if (playerRoot != null)
            {
                _walkingController = FindOnRoot<WalkingController>(playerRoot);
                _skiController = FindOnRoot<SkiController>(playerRoot);
                _playerRigidbody = FindOnRoot<Rigidbody>(playerRoot);
            }

            if (_walkingController == null)
                _walkingController = Object.FindObjectOfType<WalkingController>();

            if (_skiController == null)
                _skiController = Object.FindObjectOfType<SkiController>();

            if (_playerRigidbody == null)
            {
                if (_walkingController != null)
                    _playerRigidbody = _walkingController.GetComponentInParent<Rigidbody>();

                if (_playerRigidbody == null && _skiController != null)
                    _playerRigidbody = _skiController.GetComponentInParent<Rigidbody>();
            }
        }

        private static T FindOnRoot<T>(GameObject root) where T : Component
        {
            if (root == null)
                return null;

            T component = root.GetComponent<T>();
            if (component != null)
                return component;

            component = root.GetComponentInParent<T>();
            if (component != null)
                return component;

            return root.GetComponentInChildren<T>(true);
        }

        private static void ClearCapturedReferences()
        {
            _walkingController = null;
            _skiController = null;
            _playerRigidbody = null;
            _capturedWalkingEnabled = false;
            _capturedSkiEnabled = false;
        }
    }
}