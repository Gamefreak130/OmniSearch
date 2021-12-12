using Sims3.SimIFace;

namespace Gamefreak130.OmniSearchSpace
{
    public static class PersistedSettings
    {
        [Tunable, TunableComment("Weight applied to the title/name of an object when searching")]
        public static float kTitleWeight = 0.6f;

        [Tunable, TunableComment("Weight applied to the description of an object when searching")]
        public static float kDescriptionWeight = 0.4f;

        [Tunable, TunableComment("Size of each champion list")]
        public static int kChampionListLength = 20;

        [Tunable, TunableComment("Range 0-1: Minimum fraction of query terms that an object must contain in its title or description to be returned as a result")]
        public static float kQuerySimilarityThreshold = 0.5f;
    }
}
