namespace NOT_Lonely.Weatherade.Examples
{
    using System.Collections;
    using NOT_Lonely.Weatherade;
    using UnityEngine;

    public class RealtimeRainChange : MonoBehaviour
    {
        [SerializeField] private bool changeOnStart = true;
        [SerializeField] private float changeTime = 60;
        
        [Header("RAIN PARTICLE SYSTEM")]
        [SerializeField] private SRS_ParticleSystem srsParticleSystem;
        [SerializeField, Range(0, 1)] private float startEmissionRate = 0;
        [SerializeField, Range(0, 1)] private float endEmissionRate = 1;
        [SerializeField, Range(1, 5)] private float rateChangeSpeedMul = 4;
        
        [Header("WETNESS")]
        [SerializeField] private float startWetness = 0;
        [SerializeField] private float endWetness = 0.3f;
        [SerializeField, Range(1, 5)] private float wetnessChangeSpeedMul = 2;
        
        [Header("PUDDLES AMOUNT")]
        [SerializeField] private float startPuddlesAmount = 0;
        [SerializeField] private float endPuddlesAmount = 1;
        [SerializeField, Range(1, 5)] private float puddlesChangeSpeedMul = 1;
        
        [Header("RIPPLES AMOUNT")]
        [SerializeField, Range(0, 15)] private int startRipplesAmount = 0;
        [SerializeField, Range(0, 15)] private int endRipplesAmount = 4;
        [SerializeField, Range(1, 5)] private float ripplesAmountChangeSpeedMul = 1;
        
        [Header("RIPPLES INTENSITY")]
        [SerializeField] private float startRipplesIntensity = 0;
        [SerializeField] private float endRipplesIntensity = 0.5f;
        [SerializeField, Range(1, 5)] private float ripplesIntensityChangeSpeedMul = 1;
        
        [Header("SPOTS INTENSITY")]
        [SerializeField] private float startSpotsIntensity = 0;
        [SerializeField] private float endSpotsIntensity = 5;
        [SerializeField, Range(1, 5)] private float spotsChangeSpeedMul = 1;
        
        [Header("DRIPS INTENSITY")]
        [SerializeField] private float startDripsIntensity = 0;
        [SerializeField] private float endDripsIntensity = 5;
        [SerializeField, Range(1, 5)] private float dripsChangeSpeedMul = 1;

        private float changeSpeed => 1 / changeTime;

        private RainCoverage rainCoverage;

        void Start()
        {
            Initiate();
            
            if (changeOnStart)
                StartCoroutine(ChangeRipplesAmountGradually());
        }

        public void Initiate()
        {
            rainCoverage = (RainCoverage)CoverageBase.instance;
        }

        private IEnumerator ChangeRipplesAmountGradually()
        {
            float t = 0;
            float lerpVal = 0;

            yield return null; //skip one frame to wait while all the systems are initiated and ready
            
            while (t < 1)
            {
                t += Time.deltaTime * changeSpeed;

                if (srsParticleSystem && !Mathf.Approximately(startEmissionRate, endEmissionRate))
                {
                    lerpVal = Mathf.Clamp01(t * rateChangeSpeedMul);
                    float emissionRate = Mathf.Lerp(startEmissionRate, endEmissionRate, lerpVal);
                    SetEmissionRateInternal(emissionRate);
                }
                
                if (!Mathf.Approximately(startPuddlesAmount, endPuddlesAmount))
                {
                    lerpVal = Mathf.Clamp01(t * puddlesChangeSpeedMul);
                    float puddlesAmount = Mathf.Lerp(startPuddlesAmount, endPuddlesAmount, lerpVal);
                    SetPuddlesAmountInternal(puddlesAmount);
                }

                if (startRipplesAmount != endRipplesAmount)
                {
                    lerpVal = Mathf.Clamp01(t * ripplesAmountChangeSpeedMul);
                    int val = Mathf.RoundToInt(Mathf.Lerp(startRipplesAmount, endRipplesAmount, lerpVal));
                    SetRipplesAmountInternal(val);
                }

                if (!Mathf.Approximately(startRipplesIntensity, endRipplesIntensity))
                {
                    lerpVal = Mathf.Clamp01(t * ripplesIntensityChangeSpeedMul);
                    float ripplesIntensity = Mathf.Lerp(startRipplesIntensity, endRipplesIntensity, lerpVal);
                    SetRipplesIntensityInternal(ripplesIntensity);
                }
                
                if (!Mathf.Approximately(startSpotsIntensity, endSpotsIntensity))
                {
                    lerpVal = Mathf.Clamp01(t * spotsChangeSpeedMul);
                    float spotsIntensity = Mathf.Lerp(startSpotsIntensity, endSpotsIntensity, lerpVal);
                    SetSpotsIntensityInternal(spotsIntensity);
                }
                
                if (!Mathf.Approximately(startWetness, endWetness))
                {
                    lerpVal = Mathf.Clamp01(t * wetnessChangeSpeedMul);
                    float wetness = Mathf.Lerp(startWetness, endWetness, lerpVal);
                    SetWetnessInternal(wetness);
                }
                
                if (!Mathf.Approximately(startDripsIntensity, endDripsIntensity))
                {
                    lerpVal = Mathf.Clamp01(t * dripsChangeSpeedMul);
                    float dripsIntensity = Mathf.Lerp(startDripsIntensity, endDripsIntensity, lerpVal);
                    SetDripsIntensityInternal(dripsIntensity);
                }
                
                rainCoverage.UpdateCoverageMaterials(); //Update materials once all values changed
                
                yield return null;
            }

            SetPuddlesAmountInternal(endPuddlesAmount);
            SetRipplesIntensityInternal(endRipplesIntensity);
            SetSpotsIntensityInternal(endSpotsIntensity);
            SetRipplesAmountInternal(endRipplesAmount);
            SetWetnessInternal(endWetness);
            SetDripsIntensityInternal(endDripsIntensity);
            
            rainCoverage.UpdateCoverageMaterials(); //Update materials once all values changed
        }
        
        private void SetPuddlesAmountInternal(float amount)
        {
            rainCoverage.puddlesAmount = amount;
        }

        private void SetRipplesAmountInternal(int amount)
        {
            rainCoverage.ripplesAmount = amount;
        }

        private void SetRipplesIntensityInternal(float intensity)
        {
            rainCoverage.ripplesIntensity = intensity;
        }

        private void SetSpotsIntensityInternal(float intensity)
        {
            rainCoverage.spotsIntensity = intensity;
        }
        
        private void SetWetnessInternal(float wetness)
        {
            rainCoverage.wetnessAmount = wetness;
        }
        
        private void SetDripsIntensityInternal(float intensity)
        {
            rainCoverage.dripsIntensity = intensity;
        }

        private void SetEmissionRateInternal(float rate)
        {
            if(srsParticleSystem) srsParticleSystem.ChangeEmissionRate(rate);
        }
        
        /// <summary>
        /// Set the wetness value of rain surfaces.
        /// </summary>
        /// <param name="wetness">Representation of the wetness 'Amount' value of the Rain Coverage Instance. 0-1 float range is used.</param>
        public void SetWetness(float wetness)
        {
            SetWetnessInternal(wetness);
            rainCoverage.UpdateCoverageMaterials();
        }
        
        /// <summary>
        /// Set the amount of puddles on the surfaces.
        /// </summary>
        /// <param name="amount">Representation of the puddles 'Amount' value of the Rain Coverage Instance. 0-1 float range is used.</param>
        public void SetPuddlesAmount(float amount)
        {
            SetPuddlesAmountInternal(amount);
            rainCoverage.UpdateCoverageMaterials();
        }

        /// <summary>
        /// Set the amount of rain ripples (and spots) on the surfaces.
        /// </summary>
        /// <param name="amount">Representation of the 'Ripples Amount' value of the Rain Coverage Instance. 0-15 int range is used.</param>
        public void SetRipplesAmount(int amount)
        {
            SetRipplesAmountInternal(amount);
            rainCoverage.UpdateCoverageMaterials();
        }
        
        /// <summary>
        /// Set the intensity of rain ripples on the puddle surfaces.
        /// </summary>
        /// <param name="intensity">Representation of the 'Ripples Intensity' value of the Rain Coverage Instance.</param>
        public void SetRipplesIntensity(float intensity)
        {
            SetRipplesIntensityInternal(intensity);
            rainCoverage.UpdateCoverageMaterials();
        }
        
        /// <summary>
        /// Set the intensity of rain spots on the surfaces.
        /// </summary>
        /// <param name="intensity">Representation of the 'Spots Intensity' value of the Rain Coverage Instance. 0-5 float range is used.</param>
        public void SetSpotsIntensity(float intensity)
        {
            SetSpotsIntensityInternal(intensity);
            rainCoverage.UpdateCoverageMaterials();
        }

        /// <summary>
        /// Set the intensity of rain drips on the vertical surfaces.
        /// </summary>
        /// <param name="intensity">Representation of the 'Drips Intensity' value of the Rain Coverage Instance. 0-5 float range is used.</param>
        public void SetDripsIntensity(float intensity)
        {
            SetDripsIntensityInternal(intensity);
            rainCoverage.UpdateCoverageMaterials();
        }

        /// <summary>
        /// Set all the rain values at a time. Using this method is faster than setting every single value separately.
        /// </summary>
        /// <param name="particlesEmissionRate">Representation of the 'Emission Rate' value of the SRS Particles Emitter. 0-1 float range is used.</param>
        /// <param name="wetness">Representation of the wetness 'Amount' value of the Rain Coverage Instance. 0-1 float range is used.</param>
        /// <param name="puddlesAmount">Representation of the puddles 'Amount' value of the Rain Coverage Instance. 0-1 float range is used.</param>
        /// <param name="ripplesAmount">Representation of the 'Ripples Amount' value of the Rain Coverage Instance. 0-15 int range is used.</param>
        /// <param name="ripplesIntensity">Representation of the 'Ripples Intensity' value of the Rain Coverage Instance.</param>
        /// <param name="spotsIntensity">Representation of the 'Spots Intensity' value of the Rain Coverage Instance. 0-5 float range is used.</param>
        /// <param name="dripsIntensity">Representation of the 'Drips Intensity' value of the Rain Coverage Instance. 0-5 float range is used.</param>
        public void SetAllValues(float particlesEmissionRate, float wetness, float puddlesAmount, int ripplesAmount, float ripplesIntensity, float spotsIntensity, float dripsIntensity)
        {
            SetEmissionRateInternal(particlesEmissionRate);
            SetWetnessInternal(wetness);
            SetPuddlesAmountInternal(puddlesAmount);
            SetRipplesAmountInternal(ripplesAmount);
            SetRipplesIntensityInternal(ripplesIntensity);
            SetSpotsIntensityInternal(spotsIntensity);
            SetDripsIntensityInternal(dripsIntensity);
            
            rainCoverage.UpdateCoverageMaterials();
        }
    }
}
