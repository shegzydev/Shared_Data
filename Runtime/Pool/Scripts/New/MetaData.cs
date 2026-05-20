using System;

namespace PhysicsEngine
{
    public class MetaData
    {
        // All coordinates multiplied by 1000 (SCALE)
        internal static Vector2[] wallPoints =
        {
            new(-101657.6, 41360.6758),
            new(-101962.631, 43692.8177),
            new(-100708.855, 46407.7072),
            new(-98423.05, 48131.9046),
            new(-95929.29, 48770.6947),
            new(-93794.72, 48428.89),
            new(-90437.4, 45654.6974),
            new(-87139.15, 42000.0),
            new(-8523.499, 42000.0),
            new(-5828.77254, 45789.46),
            new(-3863.626, 48852.46),
            new(-6.10685349, 50210.46),
            new(3948.97127, 48774.87),
            new(6657.74536, 45718.79),
            new(9585.552, 42000.0),
            new(88133.55, 42000.0),
            new(92709.09, 47706.2263),
            new(94480.1254, 48565.7768),
            new(96538.6, 48747.0),
            new(98564.85, 48110.775),
            new(100359.077, 46992.02),
            new(101564.705, 45128.47),
            new(101929.84, 43183.26),
            new(101657.059, 41122.3946),
            new(100572.441, 39065.5251),
            new(95000.0, 34297.5769),
            new(95000.0, -34963.35),
            new(99999.29, -37869.072),
            new(101274.2, -39987.54),
            new(101934.967, -41822.6128),
            new(101659.073, -44048.5),
            new(100761.917, -45822.8073),
            new(98764.13, -47773.44),
            new(96226.94, -48597.3473),
            new(93604.8, -48019.8135),
            new(91394.73, -46273.1171),
            new(87329.4754, -42000.0),
            new(8587.822, -42000.0),
            new(6139.451, -45349.823),
            new(3406.68464, -48693.4052),
            new(17.8575516, -49889.61),
            new(-3499.15981, -48801.6739),
            new(-6609.7765, -45420.6352),
            new(-9560.188, -42000.0),
            new(-88120.45, -42000.0),
            new(-91862.39, -46545.8679),
            new(-93543.4, -47953.022),
            new(-95618.66, -48610.1036),
            new(-98407.87, -48182.16),
            new(-100255.867, -46738.8954),
            new(-101694.206, -44953.41),
            new(-102218.666, -42996.5019),
            new(-101847.176, -41045.002),
            new(-100937.874, -38754.6349),
            new(-95000.0, -34133.625),
            new(-95000.0, 35342.9565),
            new(-99374.35, 38253.1471),
            new(-101704.132, 41315.17),
        };

        internal static Vector2 cueBallStart = new Vector2(-73000, 0);

        internal static double headStringX = -73000;

        internal static Vector2[] rack = new Vector2[]
        {
            new Vector2(49300, 0),      // 1
            new Vector2(59426, -5850),  // 2
            new Vector2(69552, 5850),   // 3
            new Vector2(64489, 8775),   // 4
            new Vector2(54363, 2925),   // 5
            new Vector2(69552, -11700), // 6
            new Vector2(64489, -2925),  // 7
            new Vector2(59426, 0),      // 8
            new Vector2(64489, -8775),  // 9
            new Vector2(59426, 5850),   // 10
            new Vector2(54363, -2925),  // 11
            new Vector2(69552, 11700),  // 12
            new Vector2(69552, 0),      // 13
            new Vector2(64489, 2925),   // 14
            new Vector2(69552, -5850),  // 15
        };

        public static Vector2[] holesPositions =
        {
            new(-91570, 38890),
            new(-91760, -38640),
            new(91810, -38630),
            new(92030, 38560),
            new(0, 41000),
            new(0, -41000)
        };

        internal static Vector2[] railPoints = new Vector2[]
        {
            new Vector2(120459, -31952),
            new Vector2(120458, 26585),
            new Vector2(120094, 27975),
            new Vector2(120094, 27975),
            new Vector2(119380, 29052),
            new Vector2(118586, 29823),
            new Vector2(118216, 30183),
            new Vector2(116883, 30976),
            new Vector2(115476, 31438),
            new Vector2(106582, 31444),
            new Vector2(106605, 23452),
            new Vector2(112371, 23035),
            new Vector2(113148, 22959),
            new Vector2(113689, 22581),
            new Vector2(114012, 22043),
            new Vector2(114082, 21178),
            new Vector2(114159, -48886),
            new Vector2(120427, -48865),
            new Vector2(120481, -32176),
        };

        public static Circle[] holes = new Circle[]
        {
            new Circle(-96000, 43000, 5700),
            new Circle(96000, 43000, 5700),
            new Circle(-96000, -42700, 5700),
            new Circle(96000, -42700, 5700),
            new Circle(0, 44400, 5700),
            new Circle(0, -44100, 5700),
        };

        internal static Vector2 dropPosition = new Vector2(111590, 26800);

        // Helper to convert from real-world units to scaled
        public static Vector2 FromReal(double x, double y) => new Vector2(x * PhysicsParameters.SCALE, y * PhysicsParameters.SCALE);
        public static Vector2 ToReal(Vector2 v) => new Vector2(v.X / PhysicsParameters.SCALE, v.Y / PhysicsParameters.SCALE);
    }
}