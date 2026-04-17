using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Mighty
{
    [ExecuteInEditMode]
    public class MightySceneAnchor : MonoBehaviour
    {
        [SerializeField]
        private string _dataSetName;

        //        [field: SerializeField]
        public string DataSetName
        {
            get
            {
                if (_dataSetName == null || _dataSetName == "")
                    return "";
                return _dataSetName.Replace(".unity", "");
            }
            set
            {
                // Custom logic for the setter
                if (value != null)
                {
                    _dataSetName = value.Replace(".unity", "");
                }
                else
                {
                    _dataSetName = "";
                }
            }
        }

        void OnEnable()
        {
            if (DataSetName == null || DataSetName == "")
            {
                //Debug.Log("MightySceneAnchor.OnEnable DataSetName is null!");
#if UNITY_EDITOR
                DataSetName = $"{EditorSceneManager.GetActiveScene().name}___{EditorSceneManager.GetActiveScene().path.Replace("/", "_").Replace(".unity", "")}";
#else
                DataSetName = $"{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}";
#endif
            }
            //Debug.Log($"MightySceneAnchor.OnEnable DataSetName: {DataSetName}");
        }
    }
}