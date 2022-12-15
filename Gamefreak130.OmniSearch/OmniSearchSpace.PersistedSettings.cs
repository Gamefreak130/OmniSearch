namespace Gamefreak130.OmniSearchSpace
{
    public static class PersistedSettings
    {// TODO add xml
        [Tunable, TunableComment("Weight applied to the title/name of an object when searching; the higher the ratio of title weight to description weight, the more search results will prioritize titles over descriptions")]
        public static float kTitleWeight = 0.6f;

        [Tunable, TunableComment("Weight applied to the description of an object when searching; the higher the ratio of description weight to title weight, the more search results will prioritize descriptions over titles")]
        public static float kDescriptionWeight = 0.4f;

        [Tunable, TunableComment("Size of each champion list; higher values may result in more accurate search results at the cost of lower performance")]
        public static int kChampionListLength = 20;

        [Tunable, TunableComment("Range 0-1: Minimum fraction of query terms that an object must contain in its title or description to be returned as a result")]
        public static float kQuerySimilarityThreshold = 0.5f;

        [Tunable, TunableComment("Tick rate of the model preprocessing function. Set to your monitor's refresh rate for best results.")]
        public static int kPreprocessingTickRate = 60;

        [Tunable, TunableComment("True/False: Whether or not search bars will be collapsed by default when first loading the game.")]
        public static bool kCollapseSearchBarByDefault = false;
    }
}
