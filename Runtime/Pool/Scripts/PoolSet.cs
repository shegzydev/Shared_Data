namespace PhysicsEngine
{
    class PoolSet
    {
        Vector2[] wallPoints = new Vector2[]
        {
            new(-101658,  41361),
            new(-101963,  43693),
            new(-100709,  46408),
            new( -98423,  48132),
            new( -95929,  48771),
            new( -93795,  48429),
            new( -90437,  45655),
            new( -87139,  42000),
            new(  -8523,  42000),
            new(  -5829,  45789),
            new(  -3864,  48852),
            new(     -6,  50210),
            new(   3949,  48775),
            new(   6658,  45719),
            new(   9586,  42000),
            new(  88134,  42000),
            new(  92709,  47706),
            new(  94480,  48566),
            new(  96539,  48747),
            new(  98565,  48111),
            new( 100359,  46992),
            new( 101565,  45128),
            new( 101930,  43183),
            new( 101657,  41122),
            new( 100572,  39066),
            new(  95000,  34298),
            new(  95000, -34963),
            new(  99999, -37869),
            new( 101274, -39988),
            new( 101935, -41823),
            new( 101659, -44049),
            new( 100762, -45823),
            new(  98764, -47773),
            new(  96227, -48597),
            new(  93605, -48020),
            new(  91395, -46273),
            new(  87329, -42000),
            new(   8588, -42000),
            new(   6139, -45350),
            new(   3407, -48693),
            new(     18, -49890),
            new(  -3499, -48802),
            new(  -6610, -45421),
            new(  -9560, -42000),
            new( -88120, -42000),
            new( -91862, -46546),
            new( -93543, -47953),
            new( -95619, -48610),
            new( -98408, -48182),
            new(-100256, -46739),
            new(-101694, -44953),
            new(-102219, -42997),
            new(-101847, -41045),
            new(-100938, -38755),
            new( -95000, -34134),
            new( -95000,  35343),
            new( -99374,  38253),
            new(-101704,  41315),
        };

        Vector2[] ballPoints = new Vector2[]
        {
            new(-100000,  42000),
            new( -90000,  42000),
            new( -80000,  42000),
            new( -70000,  42000),
            new( -60000,  42000),
            new( -50000,  42000),
            new( -40000,  42000),
            new( -30000,  42000),
            new( -20000,  42000),
            new( -10000,  42000),
            new(      0,  42000),
            new(   10000,  42000),
            new(   20000,  42000),
            new(   30000,  42000),
            new(   40000,  42000),
            new(   50000,  42000),
            new(   60000,  42000),
            new(   70000,  42000),
            new(   80000,  42000),
            new(   90000,  42000),
        };

        PoolPhysics physics = new PoolPhysics();

        public PoolSet()
        {
            physics = new PoolPhysics();

            for (int i = 0; i < wallPoints.Length; i++)
            {
                var edge = new Edge
                {
                    P1 = wallPoints[i],
                    P2 = wallPoints[(i + 1) % wallPoints.Length]
                };
                physics.AddEdge(edge);
            }

            for (int i = 0; i < ballPoints.Length; i++)
            {
                var c = new Circle
                {
                    PrevCenter = ballPoints[i],
                    Center = ballPoints[i],
                    Radius = (long)(2.925 * PhysicsParameters.scale)
                };
                physics.AddCircle(c);
            }
        }
    }
}