using System;
using FixedMath;

namespace PhysicsEngine
{
    public class MetaData
    {
        // All coordinates multiplied by 1000 (SCALE)
        internal static Vector2Fixed[] wallPoints =
        {
            new(Fixed64.FromDouble(-101.6576), Fixed64.FromDouble(41.3606758)),
            new(Fixed64.FromDouble(-101.962631), Fixed64.FromDouble(43.6928177)),
            new(Fixed64.FromDouble(-100.708855), Fixed64.FromDouble(46.4077072)),
            new(Fixed64.FromDouble(-98.42305), Fixed64.FromDouble(48.1319046)),
            new(Fixed64.FromDouble(-95.92929), Fixed64.FromDouble(48.7706947)),
            new(Fixed64.FromDouble(-93.79472), Fixed64.FromDouble(48.42889)),
            new(Fixed64.FromDouble(-90.4374), Fixed64.FromDouble(45.6546974)),
            new(Fixed64.FromDouble(-87.13915), Fixed64.FromDouble(42.0000)),
            new(Fixed64.FromDouble(-8.523499), Fixed64.FromDouble(42.0000)),
            new(Fixed64.FromDouble(-5.82877254), Fixed64.FromDouble(45.78946)),
            new(Fixed64.FromDouble(-3.863626), Fixed64.FromDouble(48.85246)),
            new(Fixed64.FromDouble(-0.00610685349), Fixed64.FromDouble(50.21046)),
            new(Fixed64.FromDouble(3.94897127), Fixed64.FromDouble(48.77487)),
            new(Fixed64.FromDouble(6.65774536), Fixed64.FromDouble(45.71879)),
            new(Fixed64.FromDouble(9.585552), Fixed64.FromDouble(42.0000)),
            new(Fixed64.FromDouble(88.13355), Fixed64.FromDouble(42.0000)),
            new(Fixed64.FromDouble(92.70909), Fixed64.FromDouble(47.7062263)),
            new(Fixed64.FromDouble(94.4801254), Fixed64.FromDouble(48.5657768)),
            new(Fixed64.FromDouble(96.5386), Fixed64.FromDouble(48.7470)),
            new(Fixed64.FromDouble(98.56485), Fixed64.FromDouble(48.110775)),
            new(Fixed64.FromDouble(100.359077), Fixed64.FromDouble(46.99202)),
            new(Fixed64.FromDouble(101.564705), Fixed64.FromDouble(45.12847)),
            new(Fixed64.FromDouble(101.92984), Fixed64.FromDouble(43.18326)),
            new(Fixed64.FromDouble(101.657059), Fixed64.FromDouble(41.1223946)),
            new(Fixed64.FromDouble(100.572441), Fixed64.FromDouble(39.0655251)),
            new(Fixed64.FromDouble(95.0000), Fixed64.FromDouble(34.2975769)),
            new(Fixed64.FromDouble(95.0000), Fixed64.FromDouble(-34.96335)),
            new(Fixed64.FromDouble(99.99929), Fixed64.FromDouble(-37.869072)),
            new(Fixed64.FromDouble(101.2742), Fixed64.FromDouble(-39.98754)),
            new(Fixed64.FromDouble(101.934967), Fixed64.FromDouble(-41.8226128)),
            new(Fixed64.FromDouble(101.659073), Fixed64.FromDouble(-44.0485)),
            new(Fixed64.FromDouble(100.761917), Fixed64.FromDouble(-45.8228073)),
            new(Fixed64.FromDouble(98.76413), Fixed64.FromDouble(-47.77344)),
            new(Fixed64.FromDouble(96.22694), Fixed64.FromDouble(-48.5973473)),
            new(Fixed64.FromDouble(93.6048), Fixed64.FromDouble(-48.0198135)),
            new(Fixed64.FromDouble(91.39473), Fixed64.FromDouble(-46.2731171)),
            new(Fixed64.FromDouble(87.3294754), Fixed64.FromDouble(-42.0000)),
            new(Fixed64.FromDouble(8.587822), Fixed64.FromDouble(-42.0000)),
            new(Fixed64.FromDouble(6.139451), Fixed64.FromDouble(-45.349823)),
            new(Fixed64.FromDouble(3.40668464), Fixed64.FromDouble(-48.6934052)),
            new(Fixed64.FromDouble(0.0178575516), Fixed64.FromDouble(-49.88961)),
            new(Fixed64.FromDouble(-3.49915981), Fixed64.FromDouble(-48.8016739)),
            new(Fixed64.FromDouble(-6.6097765), Fixed64.FromDouble(-45.4206352)),
            new(Fixed64.FromDouble(-9.560188), Fixed64.FromDouble(-42.0000)),
            new(Fixed64.FromDouble(-88.12045), Fixed64.FromDouble(-42.0000)),
            new(Fixed64.FromDouble(-91.86239), Fixed64.FromDouble(-46.5458679)),
            new(Fixed64.FromDouble(-93.5434), Fixed64.FromDouble(-47.953022)),
            new(Fixed64.FromDouble(-95.61866), Fixed64.FromDouble(-48.6101036)),
            new(Fixed64.FromDouble(-98.40787), Fixed64.FromDouble(-48.18216)),
            new(Fixed64.FromDouble(-100.255867), Fixed64.FromDouble(-46.7388954)),
            new(Fixed64.FromDouble(-101.694206), Fixed64.FromDouble(-44.95341)),
            new(Fixed64.FromDouble(-102.218666), Fixed64.FromDouble(-42.9965019)),
            new(Fixed64.FromDouble(-101.847176), Fixed64.FromDouble(-41.045002)),
            new(Fixed64.FromDouble(-100.937874), Fixed64.FromDouble(-38.7546349)),
            new(Fixed64.FromDouble(-95.0000), Fixed64.FromDouble(-34.133625)),
            new(Fixed64.FromDouble(-95.0000), Fixed64.FromDouble(35.3429565)),
            new(Fixed64.FromDouble(-99.37435), Fixed64.FromDouble(38.2531471)),
            new(Fixed64.FromDouble(-101.704132), Fixed64.FromDouble(41.31517)),
        };

        internal static Vector2Fixed cueBallStart = new Vector2Fixed(-73.000, 0);

        internal static Fixed64 headStringX = (Fixed64)(-73.000);

        internal static Vector2Fixed[] rack = new Vector2Fixed[]
        {
            new(Fixed64.FromDouble(49.300), Fixed64.FromDouble(0)),      // 1
            new(Fixed64.FromDouble(59.426), Fixed64.FromDouble(-5.850)),  // 2
            new(Fixed64.FromDouble(69.552), Fixed64.FromDouble(5.850)),   // 3
            new(Fixed64.FromDouble(64.489), Fixed64.FromDouble(8.775)),   // 4
            new(Fixed64.FromDouble(54.363), Fixed64.FromDouble(2.925)),   // 5
            new(Fixed64.FromDouble(69.552), Fixed64.FromDouble(-11.700)), // 6
            new(Fixed64.FromDouble(64.489), Fixed64.FromDouble(-2.925)),  // 7
            new(Fixed64.FromDouble(59.426), Fixed64.FromDouble(0)),      // 8
            new(Fixed64.FromDouble(64.489), Fixed64.FromDouble(-8.775)),  // 9
            new(Fixed64.FromDouble(59.426), Fixed64.FromDouble(5.850)),   // 10
            new(Fixed64.FromDouble(54.363), Fixed64.FromDouble(-2.925)),  // 11
            new(Fixed64.FromDouble(69.552), Fixed64.FromDouble(11.700)),  // 12
            new(Fixed64.FromDouble(69.552), Fixed64.FromDouble(0)),      // 13
            new(Fixed64.FromDouble(64.489), Fixed64.FromDouble(2.925)),   // 14
            new(Fixed64.FromDouble(69.552), Fixed64.FromDouble(-5.850)),  // 15
        };

        public static Vector2Fixed[] holesPositions = new Vector2Fixed[]
        {
            new(Fixed64.FromDouble(-91.570), Fixed64.FromDouble(38.890)),
            new(Fixed64.FromDouble(-91.760), Fixed64.FromDouble(-38.640)),
            new(Fixed64.FromDouble(91.810), Fixed64.FromDouble(-38.630)),
            new(Fixed64.FromDouble(92.030), Fixed64.FromDouble(38.560)),
            new(Fixed64.FromDouble(0), Fixed64.FromDouble(41.000)),
            new(Fixed64.FromDouble(0), Fixed64.FromDouble(-41.000))
        };

        internal static Vector2Fixed[] railPoints = new Vector2Fixed[]
        {
            new (Fixed64.FromDouble(120.459), Fixed64.FromDouble(-31.952)),
            new (Fixed64.FromDouble(120.458), Fixed64.FromDouble(26.585)),
            new (Fixed64.FromDouble(120.094), Fixed64.FromDouble(27.975)),
            new (Fixed64.FromDouble(120.094), Fixed64.FromDouble(27.975)),
            new (Fixed64.FromDouble(119.380), Fixed64.FromDouble(29.052)),
            new (Fixed64.FromDouble(118.586), Fixed64.FromDouble(29.823)),
            new (Fixed64.FromDouble(118.216), Fixed64.FromDouble(30.183)),
            new (Fixed64.FromDouble(116.883), Fixed64.FromDouble(30.976)),
            new (Fixed64.FromDouble(115.476), Fixed64.FromDouble(31.438)),
            new (Fixed64.FromDouble(106.582), Fixed64.FromDouble(31.444)),
            new (Fixed64.FromDouble(106.605), Fixed64.FromDouble(23.452)),
            new (Fixed64.FromDouble(112.371), Fixed64.FromDouble(23.035)),
            new (Fixed64.FromDouble(113.148), Fixed64.FromDouble(22.959)),
            new (Fixed64.FromDouble(113.689), Fixed64.FromDouble(22.581)),
            new (Fixed64.FromDouble(114.012), Fixed64.FromDouble(22.043)),
            new (Fixed64.FromDouble(114.082), Fixed64.FromDouble(21.178)),
            new (Fixed64.FromDouble(114.159), Fixed64.FromDouble(-48.886)),
            new (Fixed64.FromDouble(120.427), Fixed64.FromDouble(-48.865)),
            new (Fixed64.FromDouble(120.481), Fixed64.FromDouble(-32.176)),
        };

        public static Circle[] holes = new Circle[]
        {
            new Circle(Fixed64.FromDouble(-96.000), Fixed64.FromDouble(43.000), Fixed64.FromDouble(5.700)),
            new Circle(Fixed64.FromDouble(96.000), Fixed64.FromDouble(43.000), Fixed64.FromDouble(5.700)),
            new Circle(Fixed64.FromDouble(-96.000), Fixed64.FromDouble(-42.700), Fixed64.FromDouble(5.700)),
            new Circle(Fixed64.FromDouble(96.000), Fixed64.FromDouble(-42.700), Fixed64.FromDouble(5.700)),
            new Circle(Fixed64.FromDouble(0), Fixed64.FromDouble(44.400), Fixed64.FromDouble(5.700)),
            new Circle(Fixed64.FromDouble(0), Fixed64.FromDouble(-44.100), Fixed64.FromDouble(5.700)),
        };

        internal static Vector2Fixed dropPosition = new Vector2Fixed(111.5900, 26.800);
    }
}