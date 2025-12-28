using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TimeWeather
{
    public class LunarCycle : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        public Sprite[] moonPhases;

        public int currentPhase = 0;
        public int phaseLength = 2;
        public int daysUntilNextPhase = 2;

        private TimeController timeController;
        private int currentDate;

        private void Start()
        {
            timeController = TimeController.instance;
            currentDate = timeController.dayOfMonth;
            spriteRenderer = GetComponent<SpriteRenderer>();

            spriteRenderer.sprite = moonPhases[currentPhase];
        }

        private void Update()
        {
            if (currentDate != timeController.dayOfMonth && timeController.timeOfDay > 12f)
            {
                currentDate = timeController.dayOfMonth;
                daysUntilNextPhase -= 1;
            }

            if (daysUntilNextPhase <= 0 )
            {
                UpdateMoonPhase();
            }
        }

        public void UpdateMoonPhase()
        {
            if (currentPhase + 1 < moonPhases.Length)
            {
                currentPhase += 1;
            }
            else currentPhase = 0;

            spriteRenderer.sprite = moonPhases[currentPhase];

            daysUntilNextPhase = 2;
        }
    }
}
