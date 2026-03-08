//using UnityEngine;
//using UnityEngine.UIElements;

//namespace SkiGame.Progression
//{
//    [RequireComponent(typeof(UIDocument))]
//    public sealed class PhoneHudSettingsApplier : MonoBehaviour
//    {
//        private UIDocument _doc;

//        private void Awake()
//        {
//            _doc = GetComponent<UIDocument>();
//        }

//        private void OnEnable()
//        {
//            GameSettingsService.EnsureLoaded();
//            GameSettingsService.OnChanged += Apply;
//            Apply(GameSettingsService.Current);
//        }

//        private void OnDisable()
//        {
//            GameSettingsService.OnChanged -= Apply;
//        }

//        private void Apply(GameSettingsProfile s)
//        {
//            if (_doc == null) return;
//            var root = _doc.rootVisualElement;
//            if (root == null) return;

//            // UI Toolkit: scale the root. Works well for a “phone” UI.
//            float scale = Mathf.Clamp(s.uiScale, 0.8f, 1.3f);
//            root.transform.scale = new Vector3(scale, scale, 1f);
//        }
//    }
//}
