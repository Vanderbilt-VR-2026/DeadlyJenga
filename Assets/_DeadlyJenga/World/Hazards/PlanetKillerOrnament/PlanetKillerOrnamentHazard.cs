using DeadlyJenga.Core;
namespace DeadlyJenga.World.Hazards
{
    public sealed class PlanetKillerOrnamentHazard : BasicDisaster
    {
        protected override string Kind => "PlanetKillerMeteor";
        protected override string FrameKind => "PlanetKillerOrnament";
        protected override bool FallingOrnaments => true;
        protected override DisasterParameter[] MakeParameters()
        {
            var parameters=base.MakeParameters();
            foreach(var p in parameters)
            {
                if(p.id=="size")p.initial=32;
                if(p.id=="frequency"){p.initial=2;p.label="Ornaments per second";}
                if(p.id=="warning")p.initial=4;
                if(p.id=="duration")p.initial=18;
            }
            return parameters;
        }
    }
}
