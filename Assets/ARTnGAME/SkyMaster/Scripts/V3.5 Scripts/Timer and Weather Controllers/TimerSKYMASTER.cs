using UnityEngine;
using System.Collections;
//using Artngame.SKYMASTER;

namespace Artngame.SKYMASTER {
public class TimerSKYMASTER : MonoBehaviour {

		SkyMasterManager skyManager;
		public bool realWorldTime = false;	
		public bool staticReference = false;
		public bool startWithRealTime = false;

		public int startYear=2016;
		public int startMonth=6;
		public int startDay=6;
		public int startHour=12;

        public int currentYear = 2016;
        public int currentMonth = 6;
        public int currentDay = 6;
        //public int currenttHour = 12;

        float shiftTime=0;
		//float secondsCounter=0;

		//int currentGameDay;
		//int currentGameMonth;
		//int currentGameYear;

		public bool enableGUI = false;

	// Use this for initialization
	void Start () {
			skyManager = this.GetComponent<SkyMasterManager> ();
			float hour = System.DateTime.Now.Hour * 3600;
			float minutes = System.DateTime.Now.Minute * 60;
			float secs = System.DateTime.Now.Second;
            float msecs = System.DateTime.Now.Millisecond; //v5.1.5c
            float seconds = hour + minutes + secs + (msecs / 1000.0f); //v5.1.5c
            //float seconds = hour + minutes + secs;
            //float fraction = seconds / 3600;

            //get real world seconds when game starts
            shiftTime = seconds;
			skyManager.Current_Time = startHour;
            previousTime = skyManager.Current_Time;

            startGameTime = new System.DateTime (startYear, startMonth, startDay, startHour, 0, 0);

			startRealTime = new System.DateTime (System.DateTime.Now.Year, System.DateTime.Now.Month, System.DateTime.Now.Day, System.DateTime.Now.Hour, System.DateTime.Now.Minute, System.DateTime.Now.Second, System.DateTime.Now.Millisecond);

			//currentGameDay = startDay;
			//currentGameMonth = startMonth;
			//currentGameYear = startYear;
	}

    float previousTime;

	System.DateTime	startGameTime;
	System.DateTime	startRealTime;

	System.DateTime	currentGameTime;
	System.DateTime	currentRealTime;

	bool startedWithRealTime = false;

        //v5.1.5c
        [Tooltip ("Game hours that correspond to one real world hour")]
        public float timeScaling = 10;

        // Update is called once per frame
        void Update () {

			if (!startedWithRealTime && startWithRealTime) {				
				startGameTime = new System.DateTime (System.DateTime.Now.Year, System.DateTime.Now.Month, System.DateTime.Now.Day, System.DateTime.Now.Hour, System.DateTime.Now.Minute, System.DateTime.Now.Second, System.DateTime.Now.Millisecond);
				startedWithRealTime = true;
			}

			float hour = System.DateTime.Now.Hour * 3600;
			float minutes = System.DateTime.Now.Minute * 60;
			float secs = System.DateTime.Now.Second;
            float msecs = System.DateTime.Now.Millisecond; //v5.1.5c
            float seconds = hour + minutes + secs + (msecs / 1000.0f); //v5.1.5c

            //Debug.Log(System.DateTime.Now.Hour + ", " + System.DateTime.Now.Minute + "," + secs + ", ALL = " + (seconds / 3600));

            if (realWorldTime){
				skyManager.Auto_Cycle_Sky = true;
				skyManager.SPEED = 0.0001f * timeScaling;
				float fraction = seconds / (3600.0f / timeScaling);
				//Debug.Log ("hour="+System.DateTime.Now.Hour + " minute="+System.DateTime.Now.Minute + " secs="+System.DateTime.Now.Second  );
				//Debug.Log (fraction);
				skyManager.Current_Time = fraction % 24;//System.DateTime.Now.TimeOfDay.TotalSeconds

                //v5.1.5c
                currentGameTime = System.DateTime.Now;


            }
            else{
				//override skymaster cycling to reduce problems from deltatime inconsitencies
				skyManager.Auto_Cycle_Sky = true;
				skyManager.SPEED = 0.0001f * timeScaling;

				if (!staticReference) {
					//float fraction = (seconds-shiftTime) / 3600;
					float secsDiff = (seconds - shiftTime) / (3600.0f / timeScaling);
					skyManager.Current_Time = (skyManager.Current_Time + secsDiff) % 24;

					shiftTime = seconds;
				} else {
					//use static initial time references for both skymaster and real world times to avoid incremental method errors
					currentRealTime = new System.DateTime (System.DateTime.Now.Year, System.DateTime.Now.Month, System.DateTime.Now.Day, System.DateTime.Now.Hour, System.DateTime.Now.Minute, System.DateTime.Now.Second, System.DateTime.Now.Millisecond);
					float secsDiff = (float)(currentRealTime - startRealTime).TotalSeconds;
					currentGameTime = startGameTime;
					currentGameTime = currentGameTime.AddSeconds (secsDiff);

					float hour1 = currentGameTime.Hour * 3600;
					float minutes1 = currentGameTime.Minute * 60;
					float secs1 = currentGameTime.Second;
                    float msecs1 = currentGameTime.Millisecond;
                    float seconds1 = hour1 + minutes1 + secs1 + (msecs1 / 1000.0f);
					float fraction = seconds1 / (3600.0f / timeScaling);
					skyManager.Current_Time = fraction % 24;

					//currentGameDay = currentGameTime.Day;

					//Debug.Log ("startGameTime  =" + startGameTime.ToLongDateString() + " ..." + startGameTime.ToLongTimeString());
					//Debug.Log ("currentGameTime=" + currentGameTime.ToLongDateString() + " ..." + currentGameTime.ToLongTimeString());
				}
			}

            //SENSE DAY PASSING
            if (skyManager.Current_Time < previousTime)
            {
                currentDay++;
            }
            currentMonth = (currentDay / 30) % 12;
            currentYear = (int)((currentDay / 30) / 12);
            previousTime = skyManager.Current_Time;
        }

		void OnGUI(){
			if (enableGUI) {
				GUI.TextField (new Rect (500, 400, 400, 22), "Game Date ="+currentGameTime.ToLongDateString());
				GUI.TextField (new Rect (500, 400+22, 400, 22), "Game Time ="+currentGameTime.ToLongTimeString());
                GUI.TextField(new Rect(500, 400 + 22 + 22, 400, 22), "Sky Master Day Time =" + skyManager.Current_Time);
                GUI.TextField(new Rect(500, 400 + 22 + 22 + 22, 400, 22), "Sky Master Day = " + (1 + (currentDay%30)) + ", Month: " + (currentMonth+1) + ", Year:"+(currentYear+1) );

            }
		}
}
}
