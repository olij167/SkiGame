using UnityEngine;

namespace PungentFunk.Utilities.EnvironmentSimulation
{
    [CreateAssetMenu(menuName = "PungentFunk/Environment Simulation/Clock Sky Profile", fileName = "Pungent Clock Sky Profile")]
    public sealed class PungentClockSkyProfile : ScriptableObject
    {
        public Gradient ambientColor = DefaultGradient(new Color(0.08f, 0.10f, 0.14f), new Color(0.78f, 0.82f, 0.88f));
        public Gradient fogColor = DefaultGradient(new Color(0.05f, 0.07f, 0.10f), new Color(0.70f, 0.76f, 0.82f));
        public Gradient sunColor = DefaultGradient(new Color(1f, 0.65f, 0.42f), Color.white);
        public AnimationCurve sunIntensity = AnimationCurve.EaseInOut(0f, 0f, 0.5f, 1f);
        public AnimationCurve moonIntensity = AnimationCurve.EaseInOut(0f, 0.35f, 0.5f, 0f);

        public void Apply(PungentClockSnapshot snapshot, Light sun, Light moon)
        {
            float t = Mathf.Clamp01(snapshot.dayPercent);
            RenderSettings.ambientLight = ambientColor.Evaluate(t);
            RenderSettings.fogColor = fogColor.Evaluate(t);

            if (sun != null)
            {
                sun.color = sunColor.Evaluate(t);
                sun.intensity = Mathf.Max(0f, sunIntensity.Evaluate(t));
                if (sun.type == LightType.Directional)
                    RenderSettings.sun = sun;
            }

            if (moon != null)
                moon.intensity = Mathf.Max(0f, moonIntensity.Evaluate(t));
        }

        private static Gradient DefaultGradient(Color night, Color day)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(night, 0f),
                    new GradientColorKey(day, 0.5f),
                    new GradientColorKey(night, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }
    }
}
