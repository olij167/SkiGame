using UnityEngine;

namespace SkiGame.Progression
{
    [DisallowMultipleComponent]
    public sealed class QuestUiSignalSource : MonoBehaviour
    {
        [SerializeField] private QuestSignalBus signalBus;
        private bool _subscribed;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (signalBus == null)
                ResolveReferences();
        }

        private void Subscribe()
        {
            if (_subscribed)
                return;

            CustomizationShopRuntime.OnOpenChanged += HandleShopOpenChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
                return;

            CustomizationShopRuntime.OnOpenChanged -= HandleShopOpenChanged;
            _subscribed = false;
        }

        private void HandleShopOpenChanged(bool isOpen)
        {
            ResolveReferences();
            if (signalBus == null)
                return;

            if (isOpen)
                signalBus.RaiseEvent("ui.shop.opened");
        }

        private void ResolveReferences()
        {
            if (signalBus == null)
                signalBus = FindObjectOfType<QuestSignalBus>();
        }
    }
}