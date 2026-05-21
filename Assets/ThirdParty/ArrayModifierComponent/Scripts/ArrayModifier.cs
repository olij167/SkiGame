using System.Collections.Generic;
using UnityEngine;

namespace UnityArrayModifier
{
    [ExecuteInEditMode]
    public class ArrayModifier : MonoBehaviour
    {
        [HideInInspector] public FitType fitType = FitType.FixedCount;
        [HideInInspector] public int count = 1;
        [HideInInspector] public Vector3 constantOffset = Vector3.right;
        [HideInInspector] public Vector3 relativeOffset = Vector3.zero;
        [HideInInspector] public float fitLength = 5f;

        private Vector3 size = Vector3.one;
        private Collider col = null;
        private Collider2D col2D = null;
        private SpriteRenderer spriteRenderer = null;
        private MeshFilter meshFilter = null;
        private ArrayModifier previousArrayModifier = null;
        private Duplicator duplicator = null;
        [SerializeField] private List<Duplicate> duplicates = new List<Duplicate>();
        [SerializeField] [HideInInspector] private List<GameObject> instantiatedDuplicates = new List<GameObject>();

        public const float MIN_TRANSLATION_MAGNITUDE = 0.01f;

        // Getters
        public int Count => count;
        public float FitLength => fitLength;
        public Duplicator Duplicator
        {
            get 
            {
                if (duplicator == null)
                {
                    InitializeDuplicator();
                }

                return duplicator;
            }
        }
        public bool IsFirst => previousArrayModifier == null;
        public List<Duplicate> Duplicates => duplicates;
        public List<GameObject> InstantiatedDuplicates => instantiatedDuplicates;
        public bool IsScheduledToBeDestroyed { get; private set; } = false;
        public int Id { get; private set; } = 0;

        public void ClearDuplicates()
        {
            duplicates.Clear();

            for (int i = instantiatedDuplicates.Count - 1; i >= 0; --i)
            {
                DestroyImmediate(instantiatedDuplicates[i]);
            }

            instantiatedDuplicates.Clear();
        }

        public void Refresh()
        {
            GetPreviousArrayModifier();

            if (!IsFirst)
            {
                previousArrayModifier.Refresh();
                return;
            }

            ClearDuplicates();
            InitializeDuplicator();
            duplicates = DuplicatorUtility.CreateDuplicates(gameObject);
            InstantiateDuplicates();
        }

        private void InstantiateDuplicates() 
        {
            var prefab = Object.Instantiate(this.gameObject);
            var duplicateBehaviour = prefab.AddComponent<DuplicateBehaviour>();
            duplicateBehaviour.Initialize(++Id, this);
            DuplicatorUtility.RemoveArrayModifiersFrom(prefab);

            foreach (var duplicate in duplicates)
            {
                var duplicateGameObject = Object.Instantiate(prefab);
                duplicateGameObject.transform.position = duplicate.position;
                duplicateGameObject.transform.rotation = duplicate.rotation;
                duplicateGameObject.hideFlags = duplicate.hideFlags;

                instantiatedDuplicates.Add(duplicateGameObject);

                duplicate.gameObject = duplicateGameObject;
            }

            DestroyImmediate(prefab);
        }

        private void InitializeComponents()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (col == null) col = GetComponent<Collider>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (col2D == null) col2D = GetComponent<Collider2D>();
        }

        private void DetermineSize()
        {
            InitializeComponents();

            if (meshFilter?.sharedMesh != null)
            {
                size = meshFilter.sharedMesh.bounds.size;
            }
            else if (spriteRenderer != null)
            {
                size = spriteRenderer.bounds.size;
            }
            else if (col != null)
            {
                size = col.bounds.size;
            }
            else if (col2D != null)
            {
                size = col2D.bounds.size;
            }
            else
            {
                size = transform.localScale;
            }

            size = new Vector3(size.x * transform.localScale.x, size.y * transform.localScale.y, size.z * transform.localScale.z);
        }

        public Vector3 GetTranslation()
        {
            DetermineSize();

            if (size.x == 0f || size.y == 0f || size.z == 0f) return Vector3.zero;

            var relative = Vector3.zero;
            relative += transform.right * relativeOffset.x * size.x;
            relative += transform.up * relativeOffset.y * size.y;
            relative += transform.forward * relativeOffset.z * size.z;

            return constantOffset + relative;
        }

        private void OnDestroy()
        {
            IsScheduledToBeDestroyed = true;
            ClearDuplicates();

            if (previousArrayModifier != null)
            {
                previousArrayModifier.Refresh();
            }
        }

        private void Update()
        {
            if (Application.isEditor)
            {
                EditorUpdate();
            }
        }

        private void GetPreviousArrayModifier()
        {
            var arrayModifiers = GetComponents<ArrayModifier>();

            for (int i = 0; i < arrayModifiers.Length; ++i)
            {
                if (arrayModifiers[i] == this)
                {
                    if ((i - 1) >= 0) previousArrayModifier = arrayModifiers[i - 1];
                }
            }
        }

        public void InitializeDuplicator()
        {
            switch (fitType)
            {
                case FitType.FixedCount:
                    duplicator = new FixedCountDuplicator(this);
                    break;
                case FitType.Length:
                    duplicator = new LengthDuplicator(this);
                    break;
                default:
                    Debug.LogError("ArrayModifier Error: Unknown FitType");
                    break;
            }
        }

        private void EditorUpdate() 
        {
            if (transform.hasChanged)
            {
                Refresh();
                transform.hasChanged = false;
            }
        }
    }
}