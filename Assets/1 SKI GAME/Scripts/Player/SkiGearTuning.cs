using System;
using UnityEngine;

[Serializable]
public struct SkiGearTuning
{
    // Core “feel” multipliers (1 = no change)

    [Tooltip("Downhill acceleration multiplier while grounded.\n" +
             "Higher = faster gravity-driven pickup on slopes.\n" +
             "Applied to SkiController's grounded slope/gravity acceleration behavior.")]
    [Min(0f)] public float downhillAccelMul;

    [Tooltip("Forward friction multiplier (glide / speed bleed).\n" +
             "Higher = more speed loss / slower top-end.\n" +
             "Lower = more glide / better speed retention.")]
    [Min(0f)] public float forwardFrictionMul;

    [Tooltip("Side friction multiplier (edge hold / lateral drift resistance).\n" +
             "Higher = more grip / less sideslip.\n" +
             "Lower = looser feel / more drift.")]
    [Min(0f)] public float sideFrictionMul;

    [Tooltip("Multiplier for tuck effect (forward-lean friction reduction).\n" +
             "Higher = stronger tuck benefit (more speed retention while leaning forward).")]
    [Min(0f)] public float tuckEffectMul;

    [Tooltip("Multiplier for brake effect (back-lean friction increase).\n" +
             "Higher = stronger braking when leaning back.")]
    [Min(0f)] public float brakeEffectMul;

    [Tooltip("Turn speed multiplier while grounded.\n" +
             "Higher = faster heading changes / snappier steering.\n" +
             "Lower = slower, more stable steering.")]
    [Min(0f)] public float turnSpeedMul;

    [Tooltip("Carve steer strength multiplier.\n" +
             "Higher = stronger 'carve lock' at speed (velocity aligns to ski direction more aggressively).\n" +
             "Lower = easier to smear/drift out of a carve.")]
    [Min(0f)] public float carveSteerMul;

    [Tooltip("Quick-stop strength multiplier.\n" +
             "Higher = sharper, more effective quick-stops.\n" +
             "Lower = softer stops / more slide-through.")]
    [Min(0f)] public float quickStopMul;

    [Tooltip("Traverse/hold strength multiplier.\n" +
             "Higher = better ability to hold a traverse line across slope.\n" +
             "Lower = more tendency to slip downhill while traversing.")]
    [Min(0f)] public float traverseHoldMul;

    [Tooltip("Skate push impulse multiplier.\n" +
             "Higher = stronger leg push acceleration at low speeds.\n" +
             "Lower = weaker skate propulsion.")]
    [Min(0f)] public float skateImpulseMul;

    [Tooltip("Pole push impulse multiplier.\n" +
             "Higher = stronger pole propulsion.\n" +
             "Lower = weaker poles.")]
    [Min(0f)] public float poleImpulseMul;

    // Grinding tuning

    [Tooltip("Minimum planar speed threshold multiplier for engaging/maintaining grind.\n" +
             "Higher = requires more speed to grind.\n" +
             "Lower = easier to stay on rails at low speed.")]
    [Min(0f)] public float grindMinSpeedMul;

    [Tooltip("Grind capture radius multiplier.\n" +
             "Higher = easier to snap onto rails (more forgiving).\n" +
             "Lower = requires more precise alignment.")]
    [Min(0f)] public float grindCaptureRadiusMul;

    [Tooltip("Grind spring multiplier (how strongly you are pulled toward the grind line/rail).\n" +
             "Higher = tighter lock.\n" +
             "Lower = looser attachment.")]
    [Min(0f)] public float grindSpringMul;

    [Tooltip("Grind response multiplier (how quickly grind strength ramps up/down).\n" +
             "Higher = snappier engagement/disengagement.\n" +
             "Lower = smoother, slower transitions.")]
    [Min(0f)] public float grindResponseMul;

    [Tooltip("Grind drive multiplier (forward drive/assistance while grinding, if your system uses it).\n" +
             "Higher = stronger drive along the grind direction.\n" +
             "Lower = more neutral/slippery grind.")]
    [Min(0f)] public float grindDriveMul;

    public static SkiGearTuning Default => new SkiGearTuning
    {
        downhillAccelMul = 1f,
        forwardFrictionMul = 1f,
        sideFrictionMul = 1f,
        tuckEffectMul = 1f,
        brakeEffectMul = 1f,
        turnSpeedMul = 1f,
        carveSteerMul = 1f,
        quickStopMul = 1f,
        traverseHoldMul = 1f,
        skateImpulseMul = 1f,
        poleImpulseMul = 1f,
        grindMinSpeedMul = 1f,
        grindCaptureRadiusMul = 1f,
        grindSpringMul = 1f,
        grindResponseMul = 1f,
        grindDriveMul = 1f
    };

    public static SkiGearTuning Multiply(in SkiGearTuning a, in SkiGearTuning b)
    {
        // Merge by multiplication (null gear = Default = 1).
        return new SkiGearTuning
        {
            downhillAccelMul = a.downhillAccelMul * b.downhillAccelMul,
            forwardFrictionMul = a.forwardFrictionMul * b.forwardFrictionMul,
            sideFrictionMul = a.sideFrictionMul * b.sideFrictionMul,
            tuckEffectMul = a.tuckEffectMul * b.tuckEffectMul,
            brakeEffectMul = a.brakeEffectMul * b.brakeEffectMul,
            turnSpeedMul = a.turnSpeedMul * b.turnSpeedMul,
            carveSteerMul = a.carveSteerMul * b.carveSteerMul,
            quickStopMul = a.quickStopMul * b.quickStopMul,
            traverseHoldMul = a.traverseHoldMul * b.traverseHoldMul,
            skateImpulseMul = a.skateImpulseMul * b.skateImpulseMul,
            poleImpulseMul = a.poleImpulseMul * b.poleImpulseMul,
            grindMinSpeedMul = a.grindMinSpeedMul * b.grindMinSpeedMul,
            grindCaptureRadiusMul = a.grindCaptureRadiusMul * b.grindCaptureRadiusMul,
            grindSpringMul = a.grindSpringMul * b.grindSpringMul,
            grindResponseMul = a.grindResponseMul * b.grindResponseMul,
            grindDriveMul = a.grindDriveMul * b.grindDriveMul,
        };
    }
}
