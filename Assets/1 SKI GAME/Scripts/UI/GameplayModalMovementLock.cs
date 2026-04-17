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
            if (owner == null)
                return;

            if (locked)
            {
                if (!Owners.Add(owner))
                    return;

                if (Owners.Count == 1)
                    ApplyLock();

                return;
            }

            if (!Owners.Remove(owner))
                return;

            if (Owners.Count == 0)
                ReleaseLock();
        }

        private static void ApplyLock()
        {
            ResolvePlayerComponents();

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
                _playerRigidbody.linearVelocity = Vector3.zero;
                _playerRigidbody.angularVelocity = Vector3.zero;
            }
        }

        private static void ReleaseLock()
        {
            if (!_stateCaptured)
                return;

            ResolvePlayerComponents();

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
        }

        private static void ResolvePlayerComponents()
        {
            if (_walkingController == null)
                _walkingController = Object.FindObjectOfType<WalkingController>();

            if (_skiController == null)
                _skiController = Object.FindObjectOfType<SkiController>();

            if (_playerRigidbody == null)
            {
                if (_walkingController != null)
                    _playerRigidbody = _walkingController.GetComponent<Rigidbody>();
                else if (_skiController != null)
                    _playerRigidbody = _skiController.GetComponent<Rigidbody>();
            }
        }
    }
}
