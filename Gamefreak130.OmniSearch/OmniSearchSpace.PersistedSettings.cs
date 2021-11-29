using Sims3.SimIFace;

namespace Gamefreak130.OmniSearchSpace
{
    public static class PersistedSettings
    {
        [Tunable, TunableComment("Weight applied to the title/name of an object when searching")]
        public static float kTitleWeight = 1f;

        [Tunable, TunableComment("Weight applied to the description of an object when searching")]
        public static float kDescriptionWeight = 0.5f;
    }
}
