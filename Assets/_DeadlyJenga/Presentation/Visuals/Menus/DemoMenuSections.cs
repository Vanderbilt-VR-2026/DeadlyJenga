namespace DeadlyJenga.CastlePrototype
{
    public static class DemoMenuSections
    {
        public static readonly string[] Tabs={"Personal","Bots","Match","Settings"};
        public static readonly string[] TabDescriptions={"Avatar and gear","Population and AI","Modes and maps","Input and audio"};
        public static CandyHudIcon Icon(string section)=>section switch
        {
            "Character" or "Survivors" or "Personal"=>CandyHudIcon.Survivor,
            "Giant hands"=>CandyHudIcon.Hand,
            "Bots" or "Enemies" or "Friends"=>CandyHudIcon.Friends,
            "Equipment" or "Bot equipment" or "Ground pickups"=>CandyHudIcon.Trigger,
            "Spectate"=>CandyHudIcon.Look,
            "Voice"=>CandyHudIcon.Sound,
            "Controls"=>CandyHudIcon.Stick,
            "Disasters"=>CandyHudIcon.Flight,
            "Modes" or "Match"=>CandyHudIcon.Play,
            "World" or "Maps"=>CandyHudIcon.Home,
            _=>CandyHudIcon.Settings
        };
        public static string Caption(string section)=>section switch
        {
            "Character"=>"Role, camera and hands",
            "Equipment"=>"Give yourself gear",
            "Spectate"=>"Watch other players",
            "Survivors"=>"Add or remove bots",
            "Giant hands"=>"Spawn flying hands",
            "Bot equipment"=>"Set the bot loadout",
            "Enemies"=>"Spawn wildlife or enemies",
            "Modes"=>"Objectives and rules",
            "Maps"=>"Browse map photos",
            "Disasters"=>"Launch and tune events",
            "World"=>"Rebuild or end round",
            "Ground pickups"=>"Place items in-world",
            "Powerup spawning"=>"Tune automatic drops",
            "Controls"=>"Bindings and gestures",
            "Settings"=>"HUD, audio and comfort",
            "Friends"=>"Room code and invites",
            "Voice"=>"Mic and bot voices",
            _=>""
        };
        public static readonly string[][] Sections={
            new[]{"Character","Equipment","Spectate"},
            new[]{"Survivors","Giant hands","Bot equipment","Enemies"},
            new[]{"Modes","Maps","Disasters","World","Ground pickups","Powerup spawning"},
            new[]{"Controls","Settings","Friends","Voice"}};
    }
}
