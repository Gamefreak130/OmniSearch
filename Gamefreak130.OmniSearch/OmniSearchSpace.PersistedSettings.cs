namespace Gamefreak130.OmniSearchSpace
{
    public static class PersistedSettings
    {
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

        [Tunable, TunableComment("True/False: Whether or not search bars will be collapsed by default when first loading the game")]
        public static bool kCollapseSearchBarByDefault = false;

        [Tunable, TunableComment("True/False: Whether or not a search bar should appear in the special merchant dialog")]
        public static bool kEnableAdventureShopDialogSearch = true;

        [Tunable, TunableComment("True/False: Whether or not a search bar should appear in Blueprint Mode")]
        public static bool kEnableBlueprintSearch = true;

        [Tunable, TunableComment("True/False: Whether or not a search bar should appear in Build Mode")]
        public static bool kEnableBuildSearch = true;

        [Tunable, TunableComment("True/False: Whether or not a search bar should appear in Buy Mode")]
        public static bool kEnableBuySearch = true;

        [Tunable, TunableComment("True/False: Whether or not a search bar should appear in Create a Style")]
        public static bool kEnableCASTSearch = true;

        [Tunable, TunableComment("True/False: Whether or not a search bar should appear in Edit Town mode")]
        public static bool kEnableEditTownSearch = true;

        [Tunable, TunableComment("True/False: Whether or not a search bar should appear in the Redeem Festival Tickets dialog")]
        public static bool kEnableFestivalDialogSearch = true;

        [Tunable, TunableComment("True/False: Whether or not a search bar should appear in inventory panels")]
        public static bool kEnableInventorySearch = true;

        [Tunable, TunableComment("True/False: Whether or not a search bar should appear in the main menu")]
        public static bool kEnableMainMenuSearch = true;

        [Tunable, TunableComment("True/False: Whether or not a search bar should appear in the household selection panel when starting a new game")]
        public static bool kEnableNewGameSearch = true;

        [Tunable, TunableComment("True/False: Whether or not a search bar should appear in the relationship panel")]
        public static bool kEnableRelationshipPanelSearch = true;

        [Tunable, TunableComment("True/False: Whether or not a search bar should appear in the grocery store and register purchase dialogs")]
        public static bool kEnableShoppingSearch = true;

        [Tunable, TunableComment("True/False: Whether or not a search bar should appear in the 'simple purchase dialog' used by concession stands, food trucks, etc.")]
        public static bool kEnableSimplePurchaseDialogSearch = true;

        [Tunable, TunableComment("True/False: Whether or not a search bar should appear in the trait selection dialog")]
        public static bool kEnableTraitsPickerDialogSearch = true;

        [Tunable, TunableComment("True/False: Whether or not a search bar should appear in the lifetime wish selection dialog")]
        public static bool kEnableWishPickerDialogSearch = true;
    }
}
