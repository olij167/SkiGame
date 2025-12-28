using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Artngame.SKYMASTER {

[ExecuteInEditMode]
public class InfiniCLOUD : MonoBehaviour {

		//v3.4.5
		public FullVolumeCloudsSkyMaster CloudsScript;

		//v3.3e
		public bool UseTabs = true;//enable tab based presentation
		public int currentTab = 0;

//		//v3.4.5
//		public AnimationCurve IntensityDiff = new AnimationCurve();
//		public AnimationCurve IntensityFog = new AnimationCurve();
//		public AnimationCurve IntensitySun = new AnimationCurve();
//		[SerializeField]
//		public AnimationCurve contrastFogCurve = new AnimationCurve();

//		[SerializeField]
//		public Gradient SkyColorGrad;

	public SkyMasterManager SkyManager;
	
	public WaterHandlerSM WaterManager;

	public bool Clouds_folder = true;
	public bool Shadows_folder = false; // (soft shadows + shadow dome)	
	public bool Reflections_folder = false;
	public bool Backlayer_folder = false;
	public bool Lightning_folder = false;
	public bool LocalLight_folder = false;
	
	// Use this for initialization
	void Start () {
	}
	
	// Update is called once per frame
	void Update () {			
	}
}
}