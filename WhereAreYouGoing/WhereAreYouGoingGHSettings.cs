using System.Numerics;
using GameHelper.Plugin;

namespace WhereAreYouGoingGH
{
    public sealed class WhereAreYouGoingGHSettings : IPSettings
    {
        public bool Enable = true;

        // --- Common / Global ---
        public bool DrawInTown = false;
        public bool DrawInHideout = false;
        public bool DrawWhenGameInBackground = false;

        public int MaxCircleDrawDistance = 120;
        public int ProjectionMs = 700;
        public float MinSpeedToProject = 0.35f;

        // Visuals
        public int CircleThickness = 4;
        public int LineThickness = 4;
        public bool DrawLine = true;
        public bool DrawEndpointCircle = true;

        // Do not draw a circle at the origin entity by default.
        public bool DrawOriginCircle = false;

        public bool DrawBoundingBoxInstead = false;
        public bool DrawFilledCircles = false;

        public int MaxProjectedScreenLength = 700;

        // --- Smoothing ---
        public float VelocitySmoothing = 0.60f;  // 0..1
        public float ScreenSmoothing = 0.25f;  // 0..1

        // --- Grouping (monsters by rarity) ---
        public bool GroupByRarity = true;
        public float GroupRadiusPx = 120f;
        public int GroupMinCount = 2;

        // Cluster visuals
        public bool ShowClusterMembers = true;
        public float MemberDotRadius = 4f;
        public int MemberSpokeThickness = 1;
        public float MemberSpokeAlpha = 0.35f;
        public bool ShowClusterCount = true;

        // Defaults: disabled for Self/Players/Friendly/Normal per your request
        public WAYGUnitConfig Normal = WAYGUnitConfig.MakeDefault(RarityColor.White, enabled: false);
        public WAYGUnitConfig Magic = WAYGUnitConfig.MakeDefault(RarityColor.Blue, enabled: true);
        public WAYGUnitConfig Rare = WAYGUnitConfig.MakeDefault(RarityColor.Yellow, enabled: true);
        public WAYGUnitConfig Unique = WAYGUnitConfig.MakeDefault(RarityColor.Orange, enabled: true);
        public WAYGUnitConfig Friendly = WAYGUnitConfig.MakeDefault(new Vector4(0.86f, 0.29f, 1f, 1f), enabled: false);
        public WAYGUnitConfig Players = WAYGUnitConfig.MakeDefault(new Vector4(0.14f, 0.76f, 0.18f, 0.76f), enabled: false);
        public WAYGUnitConfig Self = WAYGUnitConfig.MakeDefault(new Vector4(0.14f, 0.76f, 0.18f, 0.76f), enabled: false);

        public sealed class WAYGUnitConfig
        {
            public bool Enable = true;
            public Vector4 Color = Vector4.One;  // unified color

            public static WAYGUnitConfig MakeDefault(Vector4 color, bool enabled)
            {
                return new WAYGUnitConfig
                {
                    Enable = enabled,
                    Color = color
                };
            }
        }

        public static class RarityColor
        {
            public static readonly Vector4 White = new(1f, 1f, 1f, 0.37f);
            public static readonly Vector4 Blue = new(0.17f, 0.47f, 1f, 0.69f);
            public static readonly Vector4 Yellow = new(0.88f, 0.82f, 0.07f, 1f);
            public static readonly Vector4 Orange = new(0.89f, 0.48f, 0.13f, 1f);
        }
    }
}
