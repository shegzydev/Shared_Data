using System;
using FixedMath;

namespace PhysicsEngine
{
    public class MetaData
    {
        // All coordinates multiplied by 1000 (SCALE)
        internal static Vector2Fixed[] wallPoints =
        {
            new(Fixed64.FromDouble(-101657.6), Fixed64.FromDouble(41360.6758)),
            new(Fixed64.FromDouble(-101962.631), Fixed64.FromDouble(43692.8177)),
            new(Fixed64.FromDouble(-100708.855), Fixed64.FromDouble(46407.7072)),
            new(Fixed64.FromDouble(-98423.05), Fixed64.FromDouble(48131.9046)),
            new(Fixed64.FromDouble(-95929.29), Fixed64.FromDouble(48770.6947)),
            new(Fixed64.FromDouble(-93794.72), Fixed64.FromDouble(48428.89)),
            new(Fixed64.FromDouble(-90437.4), Fixed64.FromDouble(45654.6974)),
            new(Fixed64.FromDouble(-87139.15), Fixed64.FromDouble(42000.0)),
            new(Fixed64.FromDouble(-8523.499), Fixed64.FromDouble(42000.0)),
            new(Fixed64.FromDouble(-5828.77254), Fixed64.FromDouble(45789.46)),
            new(Fixed64.FromDouble(-3863.626), Fixed64.FromDouble(48852.46)),
            new(Fixed64.FromDouble(-6.10685349), Fixed64.FromDouble(50210.46)),
            new(Fixed64.FromDouble(3948.97127), Fixed64.FromDouble(48774.87)),
            new(Fixed64.FromDouble(6657.74536), Fixed64.FromDouble(45718.79)),
            new(Fixed64.FromDouble(9585.552), Fixed64.FromDouble(42000.0)),
            new(Fixed64.FromDouble(88133.55), Fixed64.FromDouble(42000.0)),
            new(Fixed64.FromDouble(92709.09), Fixed64.FromDouble(47706.2263)),
            new(Fixed64.FromDouble(94480.1254), Fixed64.FromDouble(48565.7768)),
            new(Fixed64.FromDouble(96538.6), Fixed64.FromDouble(48747.0)),
            new(Fixed64.FromDouble(98564.85), Fixed64.FromDouble(48110.775)),
            new(Fixed64.FromDouble(100359.077), Fixed64.FromDouble(46992.02)),
            new(Fixed64.FromDouble(101564.705), Fixed64.FromDouble(45128.47)),
            new(Fixed64.FromDouble(101929.84), Fixed64.FromDouble(43183.26)),
            new(Fixed64.FromDouble(101657.059), Fixed64.FromDouble(41122.3946)),
            new(Fixed64.FromDouble(100572.441), Fixed64.FromDouble(39065.5251)),
            new(Fixed64.FromDouble(95000.0), Fixed64.FromDouble(34297.5769)),
            new(Fixed64.FromDouble(95000.0), Fixed64.FromDouble(-34963.35)),
            new(Fixed64.FromDouble(99999.29), Fixed64.FromDouble(-37869.072)),
            new(Fixed64.FromDouble(101274.2), Fixed64.FromDouble(-39987.54)),
            new(Fixed64.FromDouble(101934.967), Fixed64.FromDouble(-41822.6128)),
            new(Fixed64.FromDouble(101659.073), Fixed64.FromDouble(-44048.5)),
            new(Fixed64.FromDouble(100761.917), Fixed64.FromDouble(-45822.8073)),
            new(Fixed64.FromDouble(98764.13), Fixed64.FromDouble(-47773.44)),
            new(Fixed64.FromDouble(96226.94), Fixed64.FromDouble(-48597.3473)),
            new(Fixed64.FromDouble(93604.8), Fixed64.FromDouble(-48019.8135)),
            new(Fixed64.FromDouble(91394.73), Fixed64.FromDouble(-46273.1171)),
            new(Fixed64.FromDouble(87329.4754), Fixed64.FromDouble(-42000.0)),
            new(Fixed64.FromDouble(8587.822), Fixed64.FromDouble(-42000.0)),
            new(Fixed64.FromDouble(6139.451), Fixed64.FromDouble(-45349.823)),
            new(Fixed64.FromDouble(3406.68464), Fixed64.FromDouble(-48693.4052)),
            new(Fixed64.FromDouble(17.8575516), Fixed64.FromDouble(-49889.61)),
            new(Fixed64.FromDouble(-3499.15981), Fixed64.FromDouble(-48801.6739)),
            new(Fixed64.FromDouble(-6609.7765), Fixed64.FromDouble(-45420.6352)),
            new(Fixed64.FromDouble(-9560.188), Fixed64.FromDouble(-42000.0)),
            new(Fixed64.FromDouble(-88120.45), Fixed64.FromDouble(-42000.0)),
            new(Fixed64.FromDouble(-91862.39), Fixed64.FromDouble(-46545.8679)),
            new(Fixed64.FromDouble(-93543.4), Fixed64.FromDouble(-47953.022)),
            new(Fixed64.FromDouble(-95618.66), Fixed64.FromDouble(-48610.1036)),
            new(Fixed64.FromDouble(-98407.87), Fixed64.FromDouble(-48182.16)),
            new(Fixed64.FromDouble(-100255.867), Fixed64.FromDouble(-46738.8954)),
            new(Fixed64.FromDouble(-101694.206), Fixed64.FromDouble(-44953.41)),
            new(Fixed64.FromDouble(-102218.666), Fixed64.FromDouble(-42996.5019)),
            new(Fixed64.FromDouble(-101847.176), Fixed64.FromDouble(-41045.002)),
            new(Fixed64.FromDouble(-100937.874), Fixed64.FromDouble(-38754.6349)),
            new(Fixed64.FromDouble(-95000.0), Fixed64.FromDouble(-34133.625)),
            new(Fixed64.FromDouble(-95000.0), Fixed64.FromDouble(35342.9565)),
            new(Fixed64.FromDouble(-99374.35), Fixed64.FromDouble(38253.1471)),
            new(Fixed64.FromDouble(-101704.132), Fixed64.FromDouble(41315.17)),
        };

        internal static Vector2Fixed cueBallStart = new Vector2Fixed(-73000, 0);

        internal static Fixed64 headStringX = -73000;

        internal static Vector2Fixed[] rack = new Vector2Fixed[]
        {
            new(Fixed64.FromDouble(49300), Fixed64.FromDouble(0)),      // 1
            new(Fixed64.FromDouble(59426), Fixed64.FromDouble(-5850)),  // 2
            new(Fixed64.FromDouble(69552), Fixed64.FromDouble(5850)),   // 3
            new(Fixed64.FromDouble(64489), Fixed64.FromDouble(8775)),   // 4
            new(Fixed64.FromDouble(54363), Fixed64.FromDouble(2925)),   // 5
            new(Fixed64.FromDouble(69552), Fixed64.FromDouble(-11700)), // 6
            new(Fixed64.FromDouble(64489), Fixed64.FromDouble(-2925)),  // 7
            new(Fixed64.FromDouble(59426), Fixed64.FromDouble(0)),      // 8
            new(Fixed64.FromDouble(64489), Fixed64.FromDouble(-8775)),  // 9
            new(Fixed64.FromDouble(59426), Fixed64.FromDouble(5850)),   // 10
            new(Fixed64.FromDouble(54363), Fixed64.FromDouble(-2925)),  // 11
            new(Fixed64.FromDouble(69552), Fixed64.FromDouble(11700)),  // 12
            new(Fixed64.FromDouble(69552), Fixed64.FromDouble(0)),      // 13
            new(Fixed64.FromDouble(64489), Fixed64.FromDouble(2925)),   // 14
            new(Fixed64.FromDouble(69552), Fixed64.FromDouble(-5850)),  // 15
        };

        public static Vector2Fixed[] holesPositions = new Vector2Fixed[]
        {
            new(Fixed64.FromDouble(-91570), Fixed64.FromDouble(38890)),
            new(Fixed64.FromDouble(-91760), Fixed64.FromDouble(-38640)),
            new(Fixed64.FromDouble(91810), Fixed64.FromDouble(-38630)),
            new(Fixed64.FromDouble(92030), Fixed64.FromDouble(38560)),
            new(Fixed64.FromDouble(0), Fixed64.FromDouble(41000)),
            new(Fixed64.FromDouble(0), Fixed64.FromDouble(-41000))
        };

        internal static Vector2Fixed[] railPoints = new Vector2Fixed[]
        {
            new (Fixed64.FromDouble(120459), Fixed64.FromDouble(-31952)),
            new (Fixed64.FromDouble(120458), Fixed64.FromDouble(26585)),
            new (Fixed64.FromDouble(120094), Fixed64.FromDouble(27975)),
            new (Fixed64.FromDouble(120094), Fixed64.FromDouble(27975)),
            new (Fixed64.FromDouble(119380), Fixed64.FromDouble(29052)),
            new (Fixed64.FromDouble(118586), Fixed64.FromDouble(29823)),
            new (Fixed64.FromDouble(118216), Fixed64.FromDouble(30183)),
            new (Fixed64.FromDouble(116883), Fixed64.FromDouble(30976)),
            new (Fixed64.FromDouble(115476), Fixed64.FromDouble(31438)),
            new (Fixed64.FromDouble(106582), Fixed64.FromDouble(31444)),
            new (Fixed64.FromDouble(106605), Fixed64.FromDouble(23452)),
            new (Fixed64.FromDouble(112371), Fixed64.FromDouble(23035)),
            new (Fixed64.FromDouble(113148), Fixed64.FromDouble(22959)),
            new (Fixed64.FromDouble(113689), Fixed64.FromDouble(22581)),
            new (Fixed64.FromDouble(114012), Fixed64.FromDouble(22043)),
            new (Fixed64.FromDouble(114082), Fixed64.FromDouble(21178)),
            new (Fixed64.FromDouble(114159), Fixed64.FromDouble(-48886)),
            new (Fixed64.FromDouble(120427), Fixed64.FromDouble(-48865)),
            new (Fixed64.FromDouble(120481), Fixed64.FromDouble(-32176)),
        };

        public static Circle[] holes = new Circle[]
        {
            new Circle(Fixed64.FromDouble(-96000), Fixed64.FromDouble(43000), Fixed64.FromDouble(5700)),
            new Circle(Fixed64.FromDouble(96000), Fixed64.FromDouble(43000), Fixed64.FromDouble(5700)),
            new Circle(Fixed64.FromDouble(-96000), Fixed64.FromDouble(-42700), Fixed64.FromDouble(5700)),
            new Circle(Fixed64.FromDouble(96000), Fixed64.FromDouble(-42700), Fixed64.FromDouble(5700)),
            new Circle(Fixed64.FromDouble(0), Fixed64.FromDouble(44400), Fixed64.FromDouble(5700)),
            new Circle(Fixed64.FromDouble(0), Fixed64.FromDouble(-44100), Fixed64.FromDouble(5700)),
        };

        internal static Vector2Fixed dropPosition = new Vector2Fixed(111590, 26800);
    }
}