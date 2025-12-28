using System.Collections;
using System.Collections.Generic;
using UnityEngine;


    public class SetParentTransform : MonoBehaviour
    {
        public Transform parentTransform;

        private void Start()
        {
            SetParent();
        }

        public void SetParent()
        {
            if (parentTransform == null)
                parentTransform = Camera.main.transform;

            transform.parent = parentTransform;
            transform.position = parentTransform.position;


        }
    }

