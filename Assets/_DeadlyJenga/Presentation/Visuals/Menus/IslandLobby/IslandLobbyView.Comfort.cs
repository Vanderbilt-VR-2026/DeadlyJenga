using DeadlyJenga.Presentation;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        partial void BuildComfortControls()
        {
            var choice=Add(candyJoinContent,"local-controls",()=>"CONTROLS",300,
                ()=>CandyComfortPanel.Open(session),()=>true,330,300,50);
            CandyStyle(choice);
            CandyText(choice,0,0,270,44,24);
        }
    }
}
