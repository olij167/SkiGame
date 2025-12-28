using UnityEditor;
using UnityEditor.Macros;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditorInternal;
using UnityEditor.SceneManagement; //v4.1e
using UnityEngine.SceneManagement; //v4.1e

namespace Artngame.SKYMASTER {

	[CustomEditor(typeof(InfiniCLOUD))] 	
	public class InfiniCLOUDInspector : Editor {

		public Texture2D MainIcon1;

		//public FullVolumeCloudsSkyMaster cloudsScript;

		//v3.4.5
		//		SerializedProperty IntensityDiff;
		//		SerializedProperty IntensityFog;
		//		SerializedProperty IntensitySun;
		//		SerializedProperty contrastFogCurve;
		//		//v3.3e
		//		SerializedProperty SkyColorGrad;

		//Global Sky master control script
		private InfiniCLOUD script;
		void Awake()
		{
			script = (InfiniCLOUD)target;
		}

		public void OnEnable(){	
			//v3.3e
			//			SkyColorGrad = serializedObject.FindProperty ("SkyColorGrad");
			//
			//			//v3.4.5
			//			IntensityDiff= serializedObject.FindProperty ("IntensityDiff");
			//			IntensityFog= serializedObject.FindProperty ("IntensityFog");
			//			IntensitySun= serializedObject.FindProperty ("IntensitySun");
			//			contrastFogCurve = serializedObject.FindProperty ("contrastFogCurve");
		}

		public override void  OnInspectorGUI() {

			serializedObject.Update ();

			if (script != null && script.SkyManager != null) {
				Undo.RecordObject (script.SkyManager, "Sky Variabe Change");
			}

			//CHOOSE TAB BASED
			script.UseTabs = EditorGUILayout.Toggle ("Use tabs", script.UseTabs, GUILayout.MaxWidth (180.0f));

			//TABS
			if (script.UseTabs) {
				EditorGUILayout.BeginHorizontal ();
				if (GUILayout.Button ("Clouds")) {
					script.currentTab = 0;
				}
				if (GUILayout.Button ("Shadows")) {
					script.currentTab = 1;
				}
				if (GUILayout.Button ("Reflections")) {
					script.currentTab = 2;
				}
				if (GUILayout.Button ("Backlayer")) {
					script.currentTab = 3;
				}
				EditorGUILayout.EndHorizontal ();
				EditorGUILayout.BeginHorizontal ();
				if (GUILayout.Button ("Lightning")) {
					script.currentTab = 4;
				}
				if (GUILayout.Button ("Local Lights")) {
					script.currentTab = 5;
				}
				EditorGUILayout.EndHorizontal ();
			}

            //v3.4
            //float sliderWidth = 295.0f;  //v4.1e

            //TAB0
            if ((script.UseTabs && script.currentTab == 0) || !script.UseTabs) {
				////////////////////////////////////////////////////////////
				GUI.backgroundColor = Color.blue * 0.2f;			
				EditorGUILayout.BeginVertical ("box", GUILayout.MaxWidth (180.0f));
				GUI.backgroundColor = Color.white;			

				//GUILayout.Box("",GUILayout.Height(3),GUILayout.Width(410));	
				GUILayout.Label (MainIcon1, GUILayout.MaxWidth (410.0f));

				EditorGUILayout.LabelField ("Sky Options", EditorStyles.boldLabel);


				EditorGUILayout.BeginHorizontal (GUILayout.Width (200));
				GUILayout.Space (10);
				script.Clouds_folder = EditorGUILayout.Foldout (script.Clouds_folder, "Sky options");

				EditorGUILayout.EndHorizontal ();

				if (script.Clouds_folder) {					
					EditorGUILayout.HelpBox ("Setup Clouds", MessageType.None);

					if (GUILayout.Button ("Create Clouds Using Sky Master Volume Fog")) {

						GlobalFogSkyMaster volumeFog = Camera.main.gameObject.GetComponent<GlobalFogSkyMaster>();
						FullVolumeCloudsSkyMaster cloudsScript = Camera.main.gameObject.AddComponent<FullVolumeCloudsSkyMaster> ();
						cloudsScript.Sun = volumeFog.Sun;
						cloudsScript.SkyManager = volumeFog.SkyManager;

                        cloudsScript.initVariablesA();

						//cloudsScript._Altitude0 =   2300;
						//cloudsScript._Altitude1 =   4200;

						//cloudsScript._SampleCount0 =   1;
						//cloudsScript._SampleCount1 = 140;
						//cloudsScript._SampleCountL =   9;

						//cloudsScript._NoiseFreq1 = 5.1f;
						//cloudsScript._NoiseFreq2 = 49;

						//cloudsScript._NoiseAmp1 = 5.32f;
						//cloudsScript._NoiseAmp2 = 2.34f;
						//cloudsScript._NoiseBias = -3.8f;

						//cloudsScript.splitPerFrames = 1;
						//cloudsScript.downScale = true;
						//cloudsScript.downScaleFactor = 2;
						//cloudsScript._Scatter = 0.02f;

						//cloudsScript._Extinct = 0.01f;
						//cloudsScript._HGCoeff = -0.05f;

						//cloudsScript._BackShade = 0.4f;
						//cloudsScript._UndersideCurveFactor = 0.2f;
						//cloudsScript._FarDist = 442000;

						//cloudsScript.distanceFog = false;
						//cloudsScript._SunSize = 20;

						//cloudsScript._Scroll1 = new Vector3 (-0.25f,-0.14f,-0.25f);
						//cloudsScript._Scroll2 = new Vector3 (0.01f,0.05f,0.03f);

						//cloudsScript._InteractTexturePos = new Vector4 (0.06f,0.05f, -1222f,-1222f);
						//cloudsScript._InteractTextureAtr = new Vector4 (0.34f,0.991f, 0f,1);
						//cloudsScript._InteractTextureOffset = new Vector4 (1100,1100,0,0);

						//cloudsScript._HorizonYAdjust = 2000;

						//keep the clouds reference
						script.CloudsScript = cloudsScript;

						MoveComponentToBottom (Camera.main.gameObject);
					}	


					if (GUILayout.Button ("Create Reflections on Sky Master Water")) {
						if (script.CloudsScript != null) {
							script.CloudsScript.updateReflectionCamera = true;
							script.CloudsScript.updateReflections ();

							//script.CloudsScript.updateReflectionCamera
							script.CloudsScript.reflectClouds._HorizonYAdjust = -500;
							script.CloudsScript.reflectClouds._FarDist = script.CloudsScript._FarDist/2;
                            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); //EditorApplication.MarkSceneDirty(); //v4.1e
						} else {
							Debug.Log ("No Clouds");
						}
					}

					if (GUILayout.Button ("Setup Shadows")) {
						if (script.CloudsScript != null) {
							script.CloudsScript.setupShadows = true;
							script.CloudsScript.createShadowDome ();
							script.CloudsScript.shadowsUpdate ();
						}
					}

					if (GUILayout.Button ("Setup Back Layer")) {
						if (script.CloudsScript != null) {
							script.CloudsScript.setupDepth = true;
							script.CloudsScript.createDepthSetup ();
							script.CloudsScript.setupDepth = true;
							script.CloudsScript.blendBackground = true;
						}
					}

					if (GUILayout.Button ("Setup Lightning")) {
						if (script.CloudsScript != null) {
							script.CloudsScript.setupLightning = true;
							script.CloudsScript.createLightningBox ();
							//script.CloudsScript.shadowsUpdate ();
							script.CloudsScript.EnableLightning = true;
							script.CloudsScript.lightning_every = 5;
							script.CloudsScript.max_lightning_time = 9;
						}
					}

					if (GUILayout.Button ("Setup Local Light")) {
						if (script.CloudsScript != null && script.CloudsScript.EnableLightning) {
							script.CloudsScript.useLocalLightLightn = true;
							//create local light
							GameObject localLight = new GameObject();
							localLight.name = "Clouds Local Light";
							Light actuallight = localLight.AddComponent<Light> ();
							actuallight.type = LightType.Point;
							actuallight.range = 2000;
							localLight.transform.position = Camera.main.transform.forward * 1000 + new Vector3 (0, 2000, 0);
						}
					}


					if (GUILayout.Button ("Move Volume Fog to Top")) {

						MoveComponentToTop (Camera.main.gameObject);

					}	
					if (GUILayout.Button ("Move Volume Fog to Bottom")) {

						MoveComponentToBottom (Camera.main.gameObject);

					}
					EditorGUIUtility.wideMode = false;				
				}
				EditorGUILayout.EndVertical ();			
				////////////////////////////////////////////////////////////
			}//END TAB0

			//TAB1
			//			if (script.UseTabs && script.currentTab == 1) {				
			//				if (script.SkyManager == null) {
			//					EditorGUILayout.BeginVertical ("box", GUILayout.MaxWidth (410.0f));
			//					EditorGUILayout.HelpBox ("Please add Sky to enable Cloud options", MessageType.None);
			//					EditorGUILayout.EndVertical ();
			//				}
			//			}

			if (script.SkyManager != null) {
				////////////////////// VOLUMETRIC CLOUDS //////////////////////////	
				//TAB1
				if ((script.UseTabs && script.currentTab == 1) | !script.UseTabs) {
				}//END TAB1
			}//END CHECK SKYMANAGER EXISTS

			serializedObject.ApplyModifiedProperties ();

			if(GUI.changed){//v3.4.5
				//EditorUtility.SetDirty (script);//v3.4.5
			}
		}

		private void SceneGUI(SceneView sceneview)
		{			
		}


		private void MoveComponentToTop(GameObject GameObj)//(MenuCommand menuCommand)
		{
			//Component c = (Component)menuCommand.context;
			Component[] allComponents = GameObj.GetComponents<Component>();
			int iOffset = 0;
			for(int i=0; i < allComponents.Length; i++)
			{
				if (allComponents[i] is GlobalFogSkyMaster) //if(allComponents[i] == c)
				{
					iOffset = i-4;
					for(int j =0; j < iOffset -1; j++)
					{
						UnityEditorInternal.ComponentUtility.MoveComponentUp(allComponents[i]);
					}
					break;
				}
			}

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); //EditorApplication.MarkSceneDirty(); //v4.1e
        }
		//[MenuItem("CONTEXT/Component/Move To Bottom")]
		private void MoveComponentToBottom(GameObject GameObj)//(MenuCommand menuCommand)
		{
			//Component c = (Component)menuCommand.context;
			Component[] allComponents = GameObj.GetComponents<Component>();
			int iOffset = 0;
			for (int i = 0; i < allComponents.Length; i++)
			{
				if (allComponents[i] is GlobalFogSkyMaster) //if (allComponents[i] == c)
				{
					iOffset = i;
					for (; iOffset < allComponents.Length; iOffset++)
					{
						UnityEditorInternal.ComponentUtility.MoveComponentDown(allComponents[i]);
					}
					break;
				}
			}

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); //EditorApplication.MarkSceneDirty(); //v4.1e
        }

	}
}