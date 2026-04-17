using System;

namespace SkiGame.Progression
{
    public sealed partial class PlayerStatsProfile
    {
        [Serializable]
        public sealed class ActiveResortRentalState
        {
            public string resortId;
            public double startGameHours;
            public double expiryGameHours;
            public double totalHoursPurchased;
        }

        [Serializable]
        public sealed class RegionReputationState
        {
            public string regionId;
            public int reputation;
        }
    }
}
