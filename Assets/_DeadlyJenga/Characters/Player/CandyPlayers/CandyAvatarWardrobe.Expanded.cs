using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyAvatarWardrobe
    {
        static bool OneOf(AvatarSkin skin,params AvatarSkin[] kinds)=>System.Array.IndexOf(kinds,skin)>=0;
        static Color ExpandedSkinColor(AvatarSkin skin)
        {
            if(OneOf(skin,AvatarSkin.Fox,AvatarSkin.Leopard,AvatarSkin.Clownfish,AvatarSkin.Cat,AvatarSkin.TRex))return new Color(.95f,.39f,.07f);
            if(OneOf(skin,AvatarSkin.Koala,AvatarSkin.Elephant,AvatarSkin.Gargoyle,AvatarSkin.Walrus))return new Color(.43f,.48f,.54f);
            if(OneOf(skin,AvatarSkin.Panda,AvatarSkin.Husky,AvatarSkin.Ghost,AvatarSkin.Mummy,AvatarSkin.Zebra,AvatarSkin.ScaryClown,AvatarSkin.FriendlyClown))return Color.white;
            if(OneOf(skin,AvatarSkin.GoldenRetriever,AvatarSkin.HoneyBear,AvatarSkin.Lion,AvatarSkin.Banana,AvatarSkin.Pineapple))return new Color(1,.72f,.13f);
            if(OneOf(skin,AvatarSkin.Dragon,AvatarSkin.Alligator,AvatarSkin.Snake,AvatarSkin.Turtle,AvatarSkin.Elf,AvatarSkin.RobinHood))return new Color(.25f,.67f,.20f);
            if(skin==AvatarSkin.RagDoll)return new Color(1,.79f,.66f);
            if(skin==AvatarSkin.Gorilla)return new Color(.16f,.17f,.19f);
            if(skin==AvatarSkin.Fish)return new Color(.08f,.65f,.88f);
            if(OneOf(skin,AvatarSkin.CyclopsMonster,AvatarSkin.Axolotl,AvatarSkin.DiscoAlien))return new Color(.95f,.27f,.63f);
            if(OneOf(skin,AvatarSkin.Santa,AvatarSkin.MrsClaus,AvatarSkin.CandyCane,AvatarSkin.ScaryClown,AvatarSkin.Parrot))return new Color(.88f,.035f,.07f);
            if(OneOf(skin,AvatarSkin.Astronaut,AvatarSkin.NeonRobot,AvatarSkin.DeepSeaDiver,AvatarSkin.CrystalKnight,AvatarSkin.StormWizard))return new Color(.12f,.65f,.88f);
            return new Color(.50f,.29f,.13f);
        }
        void BuildExpandedSkin(CandyPlayerRig rig)
        {
            var skin=appearance.skin;if(skin<=AvatarSkin.Vampire||skin==AvatarSkin.MushroomSprite)return;string id=skin.ToString();
            bool animal=OneOf(skin,AvatarSkin.Fox,AvatarSkin.Koala,AvatarSkin.Panda,AvatarSkin.GoldenRetriever,AvatarSkin.Husky,AvatarSkin.Leopard,AvatarSkin.Elephant,AvatarSkin.HoneyBear,AvatarSkin.Werewolf,AvatarSkin.Reindeer,AvatarSkin.Horse,AvatarSkin.Bulldog,AvatarSkin.Gorilla,AvatarSkin.Monkey,AvatarSkin.Rabbit,AvatarSkin.Squirrel,AvatarSkin.Cat,AvatarSkin.Raccoon,AvatarSkin.Otter,AvatarSkin.Wolf,AvatarSkin.Lion,AvatarSkin.Walrus,AvatarSkin.Zebra);
            if(animal)
            {
                bool pointed=OneOf(skin,AvatarSkin.Fox,AvatarSkin.Husky,AvatarSkin.Werewolf,AvatarSkin.Cat,AvatarSkin.Wolf);
                bool floppy=OneOf(skin,AvatarSkin.GoldenRetriever,AvatarSkin.Bulldog,AvatarSkin.Horse);
                for(int side=-1;side<=1;side+=2)
                {
                    int s=side;
                    if(pointed)Add(rig,id+" pointed ear "+s,1,Tone.Primary,b=>b.Cone(new Vector3(s*.16f,.14f,0),new Vector3(s*.22f,.39f,-.025f),.095f,0,4));
                    else Ball(rig,id+" ear "+s,1,Tone.Primary,new Vector3(s*(skin==AvatarSkin.Elephant?.27f:.20f),floppy?0:.19f,-.02f),new Vector3(skin==AvatarSkin.Elephant?.17f:.10f,floppy?.20f:.11f,.06f));
                    Ball(rig,id+" inner ear "+s,1,Tone.Dark,new Vector3(s*.21f,floppy?-.01f:.21f,.035f),new Vector3(.052f,floppy?.12f:.058f,.012f));
                }
                bool longSnout=OneOf(skin,AvatarSkin.Fox,AvatarSkin.Husky,AvatarSkin.Wolf,AvatarSkin.Werewolf,AvatarSkin.Horse,AvatarSkin.GoldenRetriever);
                Ball(rig,id+" muzzle",1,skin==AvatarSkin.HoneyBear?Tone.Primary:Tone.White,new Vector3(0,-.07f,longSnout?.25f:.19f),new Vector3(.14f,.095f,longSnout?.18f:.10f));
                Ball(rig,id+" nose",1,Tone.Black,new Vector3(0,-.025f,longSnout?.42f:.285f),new Vector3(.055f,.035f,.025f));
                Ball(rig,id+" belly",0,skin==AvatarSkin.HoneyBear?Tone.Primary:Tone.White,new Vector3(0,.98f,.13f),new Vector3(.16f,.25f,.075f));FaceEyes(rig,id+" eyes",Tone.Black);
                if(OneOf(skin,AvatarSkin.Fox,AvatarSkin.Squirrel,AvatarSkin.Cat,AvatarSkin.Leopard,AvatarSkin.Monkey,AvatarSkin.Lion,AvatarSkin.Raccoon,AvatarSkin.Otter,AvatarSkin.Wolf,AvatarSkin.Werewolf))
                    Add(rig,id+" curved tail",0,Tone.Primary,b=>{Vector3 last=new Vector3(0,.65f,-.13f);for(int i=1;i<=6;i++){var next=new Vector3(Mathf.Sin(i*.45f)*.18f,.65f+i*.065f,-.13f-i*.085f);b.Cone(last,next,skin==AvatarSkin.Squirrel?.13f:.05f,skin==AvatarSkin.Squirrel?.11f:.045f,10);last=next;}b.Sphere(last,Vector3.one*.06f);});
                if(skin==AvatarSkin.Elephant)Add(rig,"elephant trunk",1,Tone.Primary,b=>{for(int i=0;i<7;i++)b.Sphere(new Vector3(0,-.02f-i*.05f,.23f+Mathf.Sin(i*.4f)*.09f),Vector3.one*(.065f-i*.004f));});
                if(skin==AvatarSkin.Lion)Add(rig,"lion mane",1,Tone.Brown,b=>{for(int i=0;i<16;i++){float a=i*Mathf.PI/8;b.Sphere(new Vector3(Mathf.Cos(a)*.24f,Mathf.Sin(a)*.24f,-.03f),new Vector3(.10f,.10f,.09f));}});
                if(skin==AvatarSkin.Rabbit)for(int side=-1;side<=1;side+=2)Ball(rig,"rabbit tall ear"+side,1,Tone.Primary,new Vector3(side*.11f,.43f,0),new Vector3(.07f,.29f,.05f));
                if(skin==AvatarSkin.Walrus)for(int side=-1;side<=1;side+=2){int s=side;Add(rig,"walrus tusk"+s,1,Tone.White,b=>b.Cone(new Vector3(s*.10f,-.10f,.29f),new Vector3(s*.085f,-.38f,.32f),.025f,0,10));}
                if(skin==AvatarSkin.Panda||skin==AvatarSkin.Raccoon)for(int side=-1;side<=1;side+=2)Ball(rig,id+" eye patch"+side,1,Tone.Black,new Vector3(side*.10f,.04f,.19f),new Vector3(.075f,.08f,.025f));
                if(skin==AvatarSkin.Koala)Ball(rig,"koala broad nose",1,Tone.Black,new Vector3(0,0,.225f),new Vector3(.075f,.095f,.035f));
                if(skin==AvatarSkin.Reindeer)Ball(rig,"rudolph red nose",1,Tone.Red,new Vector3(0,-.025f,.30f),Vector3.one*.065f);
                if(skin==AvatarSkin.Zebra)Spots(rig,id,Tone.Black,true);
                if(skin==AvatarSkin.Leopard)Spots(rig,id,Tone.Black,false);
                if(skin==AvatarSkin.HoneyBear){Jacket(rig,"honey bear red shirt",Tone.Red);}
                if(skin==AvatarSkin.Reindeer)for(int side=-1;side<=1;side+=2){int s=side;Add(rig,"reindeer antler"+s,1,Tone.Brown,b=>{b.Beam(new Vector3(s*.13f,.16f,0),new Vector3(s*.25f,.50f,-.04f),.022f);for(int n=0;n<3;n++)b.Beam(new Vector3(s*(.17f+n*.035f),.26f+n*.085f,0),new Vector3(s*(.29f+n*.035f),.32f+n*.085f,.02f),.015f);});}
                return;
            }
            if(skin!=AvatarSkin.CyclopsMonster&&skin!=AvatarSkin.DiscoAlien)FaceEyes(rig,id+" eyes",Tone.Black);
            if(OneOf(skin,AvatarSkin.Fish,AvatarSkin.Clownfish,AvatarSkin.Owl,AvatarSkin.Parrot))
            {
                Ball(rig,id+" broad breast",0,Tone.Primary,new Vector3(0,.96f,0),new Vector3(.29f,.36f,.20f));
                Add(rig,id+" beak or mouth",1,skin==AvatarSkin.Parrot?Tone.Black:Tone.Yellow,b=>b.Cone(new Vector3(0,-.035f,.18f),new Vector3(0,-.09f,.39f),.09f,0,12));
                for(int side=-1;side<=1;side+=2){int s=side;Add(rig,id+" fin or wing"+s,s<0?2:5,Tone.Primary,b=>b.Wedge(new Vector3(-.06f,0,0),new Vector3(.06f,0,0),new Vector3(-.20f,.32f,-.03f),new Vector3(.20f,.32f,-.03f),.04f));}
                if(skin==AvatarSkin.Parrot){for(int side=0;side<2;side++)Ball(rig,"parrot blue wing"+side,side==0?2:5,Tone.Blue,new Vector3(0,.15f,-.025f),new Vector3(.09f,.23f,.065f));}
                if(skin==AvatarSkin.Clownfish)Spots(rig,id,Tone.White,true);
                if(skin==AvatarSkin.Owl)for(int side=-1;side<=1;side+=2)Ball(rig,"owl facial disc"+side,1,Tone.White,new Vector3(side*.10f,.04f,.17f),new Vector3(.11f,.12f,.045f));return;
            }
            if(OneOf(skin,AvatarSkin.Dragon,AvatarSkin.TRex,AvatarSkin.Snake,AvatarSkin.Alligator,AvatarSkin.Turtle,AvatarSkin.Axolotl))
            {
                Ball(rig,id+" snout",1,Tone.Primary,new Vector3(0,-.07f,.23f),new Vector3(.17f,.09f,.21f));
                Add(rig,id+" dorsal crest",0,Tone.Dark,b=>{for(int i=0;i<6;i++)b.Cone(new Vector3(0,.65f+i*.12f,-.15f),new Vector3(0,.68f+i*.12f,-.29f),.06f,0,5);});
                if(skin==AvatarSkin.Dragon||skin==AvatarSkin.Alligator||skin==AvatarSkin.TRex)Add(rig,id+" teeth",1,Tone.White,b=>{for(int side=-1;side<=1;side+=2)for(int tooth=0;tooth<5;tooth++)b.Cone(new Vector3(side*.12f,-.07f,.19f+tooth*.045f),new Vector3(side*.12f,-.15f,.19f+tooth*.045f),.014f,0,6);});
                if(skin==AvatarSkin.Turtle)Ball(rig,"turtle shell",0,Tone.Brown,new Vector3(0,.95f,-.16f),new Vector3(.31f,.37f,.18f));
                if(skin==AvatarSkin.Dragon){Cape(rig,"dragon wing membrane",Tone.Dark);Add(rig,"dragon swept horns",1,Tone.Yellow,b=>{for(int side=-1;side<=1;side+=2)b.Cone(new Vector3(side*.14f,.15f,-.03f),new Vector3(side*.24f,.39f,-.16f),.055f,0,10);});}
                if(skin==AvatarSkin.Axolotl)for(int side=-1;side<=1;side+=2)for(int i=0;i<3;i++){int s=side,n=i;Add(rig,"axolotl gill"+s+n,1,Tone.Red,b=>b.Beam(new Vector3(s*.17f,.10f-n*.07f,0),new Vector3(s*.38f,.19f-n*.12f,0),.022f));}
                return;
            }
            if(skin==AvatarSkin.CyclopsMonster||skin==AvatarSkin.DiscoAlien)
            {Ball(rig,id+" single eye",1,Tone.White,new Vector3(0,.02f,.20f),new Vector3(.14f,.14f,.045f));Ball(rig,id+" pupil",1,Tone.Blue,new Vector3(0,.02f,.246f),new Vector3(.065f,.075f,.012f));Hair(rig,AvatarHair.Spikes);Spots(rig,id,Tone.Blue,false);return;}
            if(skin==AvatarSkin.Zebra){Spots(rig,id,Tone.Black,true);Hair(rig,AvatarHair.Spikes);return;}
            if(skin==AvatarSkin.Ghost){Robe(rig,"ghost floating sheet",Tone.White);Ball(rig,"ghost mouth",1,Tone.Black,new Vector3(0,-.10f,.205f),new Vector3(.045f,.07f,.018f));return;}
            if(skin==AvatarSkin.Mummy){Spots(rig,"mummy bandages",Tone.White,true);return;}
            if(skin==AvatarSkin.Banana||skin==AvatarSkin.Pineapple||skin==AvatarSkin.CandyCorn||skin==AvatarSkin.CandyCane||skin==AvatarSkin.Gingerbread)
            {
                if(skin==AvatarSkin.Pineapple){Ball(rig,"pineapple textured body",0,Tone.Yellow,new Vector3(0,.99f,0),new Vector3(.27f,.39f,.22f));Add(rig,"pineapple leaf crown",1,Tone.Green,b=>{for(int i=0;i<9;i++){float a=i*2.399963f;var edge=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));b.Wedge(edge*.055f+Vector3.up*.17f,edge*.13f+Vector3.up*.18f,edge*.25f+Vector3.up*(.48f+i%3*.10f),edge*.25f+Vector3.up*(.48f+i%3*.10f),.016f);}});Spots(rig,"pineapple diamond eyes",Tone.Brown,false);}
                if(skin==AvatarSkin.Banana)Add(rig,"banana peel crown",1,Tone.Yellow,b=>{for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;var tip=new Vector3(Mathf.Cos(a)*.31f,.39f,Mathf.Sin(a)*.31f);b.Cone(new Vector3(0,.16f,0),tip,.065f,0,8);}});
                if(skin==AvatarSkin.CandyCorn){Robe(rig,"candy corn orange base",Tone.Orange);Hat(rig,"candy corn white tip",Tone.White,true);}
                if(skin==AvatarSkin.CandyCane)Spots(rig,"candy cane stripe",Tone.White,true);
                if(skin==AvatarSkin.Gingerbread){for(int i=0;i<3;i++)Ball(rig,"gingerbread gumdrop"+i,0,Tone.Glow,new Vector3(0,.85f+i*.14f,.17f),Vector3.one*.035f);Hair(rig,AvatarHair.Curls);}return;
            }
            bool festive=OneOf(skin,AvatarSkin.Santa,AvatarSkin.MrsClaus,AvatarSkin.Elf);
            if(festive)
            {Jacket(rig,id+" coat",skin==AvatarSkin.Elf?Tone.Primary:Tone.Red);Add(rig,id+" soft festive cap",1,Tone.Red,b=>{b.Cone(new Vector3(0,.17f,0),new Vector3(.03f,.43f,0),.21f,.09f,20);b.Cone(new Vector3(.03f,.43f,0),new Vector3(.23f,.35f,0),.09f,.02f,14);});Add(rig,id+" hat fur",1,Tone.White,b=>{b.Ring(new Vector3(0,.18f,0),.21f,.035f,Quaternion.Euler(90,0,0));b.Sphere(new Vector3(.23f,.35f,0),Vector3.one*.06f);});Pants(rig,id+" trousers",Tone.Dark);Add(rig,id+" fluffy trim",0,Tone.White,b=>{b.Ring(new Vector3(0,.75f,0),.21f,.035f,Quaternion.Euler(90,0,0));b.Box(new Vector3(0,1.03f,.17f),new Vector3(.055f,.48f,.04f));});if(skin==AvatarSkin.Santa)Ball(rig,id+" beard",1,Tone.White,new Vector3(0,-.15f,.14f),new Vector3(.18f,.13f,.10f));return;}
            if(skin==AvatarSkin.ScaryClown||skin==AvatarSkin.FriendlyClown||skin==AvatarSkin.RagDoll)
            {Hair(rig,skin==AvatarSkin.RagDoll?AvatarHair.Braids:AvatarHair.Afro);Ball(rig,id+" red nose",1,Tone.Red,new Vector3(0,0,.24f),Vector3.one*.05f);Jacket(rig,id+" costume",Tone.Purple);Spots(rig,id,Tone.White,false);return;}
            if(skin==AvatarSkin.HeadlessHorseman){Cape(rig,id,Tone.Dark,true);Pumpkin(rig,"horseman pumpkin",Tone.Orange);Pants(rig,id,Tone.Dark);return;}
            if(skin==AvatarSkin.Gargoyle){Cape(rig,"gargoyle stone wings",Tone.Primary);Add(rig,"gargoyle swept horns",1,Tone.Dark,b=>{for(int side=-1;side<=1;side+=2){b.Cone(new Vector3(side*.14f,.15f,0),new Vector3(side*.24f,.38f,-.06f),.065f,.025f,10);b.Cone(new Vector3(side*.24f,.38f,-.06f),new Vector3(side*.16f,.45f,-.1f),.025f,0,10);}});return;}
            if(skin==AvatarSkin.Sailor){for(int side=0;side<2;side++)Ball(rig,"sailor strong forearm"+side,side==0?3:6,Tone.Primary,new Vector3(0,.16f,0),new Vector3(.105f,.14f,.10f));Add(rig,"sailor pipe",1,Tone.Brown,b=>{b.Beam(new Vector3(.04f,-.10f,.21f),new Vector3(.20f,-.12f,.32f),.009f);b.Cone(new Vector3(.20f,-.13f,.32f),new Vector3(.20f,-.06f,.32f),.025f,.034f,10);});}
            if(skin==AvatarSkin.Inmate||skin==AvatarSkin.Sailor){Jacket(rig,id+" shirt",Tone.White);Pants(rig,id+" pants",Tone.Dark);Spots(rig,id,Tone.Black,true);Hat(rig,id+" cap",Tone.White,false);return;}
            if(skin==AvatarSkin.RobinHood||skin==AvatarSkin.Scarecrow){Jacket(rig,id+" tunic",Tone.Primary);Hat(rig,id+" hat",Tone.Brown,true);Pants(rig,id,Tone.Brown);return;}
            if(skin==AvatarSkin.StormWizard){Robe(rig,"storm wizard gown",Tone.Blue);Hat(rig,"storm wizard hat",Tone.Blue,true);Cape(rig,"storm stars cape",Tone.Purple);return;}
            if(skin==AvatarSkin.MushroomSprite){Ball(rig,"mushroom sprite cap",1,Tone.Red,new Vector3(0,.23f,0),new Vector3(.40f,.14f,.35f));Add(rig,"mushroom white cap spots",1,Tone.White,b=>{for(int i=0;i<18;i++){float a=i*2.399963f,y=.24f+(i%3)*.04f,r=.36f*(1-(i%3)*.19f);b.Sphere(new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r*.86f),Vector3.one*.035f);}});return;}
            if(skin==AvatarSkin.SpacePirate){Hat(rig,"space pirate hat",Tone.Dark,false);Jacket(rig,"space pirate jacket",Tone.Red);Cape(rig,"space pirate mantle",Tone.Dark);return;}
            BuildArmor(rig);
            if(skin==AvatarSkin.Astronaut){Ball(rig,"astronaut panoramic visor",1,Tone.Blue,new Vector3(0,.015f,.18f),new Vector3(.20f,.15f,.085f));Add(rig,"astronaut life support",0,Tone.White,b=>b.Box(new Vector3(0,1.02f,-.23f),new Vector3(.33f,.45f,.20f)));}
            if(skin==AvatarSkin.NeonRobot){Add(rig,"robot glowing face bars",1,Tone.Glow,b=>{b.Box(new Vector3(0,.04f,.228f),new Vector3(.30f,.025f,.018f));b.Box(new Vector3(0,-.08f,.22f),new Vector3(.14f,.018f,.018f));});}
            if(skin==AvatarSkin.CandyKnight){Hat(rig,"candy knight icing crest",Tone.White,false);Add(rig,"candy knight candy plume",1,Tone.Red,b=>{for(int i=0;i<7;i++)b.Sphere(new Vector3(0,.30f+i*.035f,-i*.035f),Vector3.one*.055f);});}

            if(skin==AvatarSkin.DeepSeaDiver){Ball(rig,"diver brass helmet",1,Tone.Yellow,Vector3.zero,Vector3.one*.25f);Ball(rig,"diver glass viewport",1,Tone.Blue,new Vector3(0,0,.225f),new Vector3(.17f,.17f,.045f));Add(rig,"diver porthole rim",1,Tone.Silver,b=>b.Ring(new Vector3(0,0,.26f),.18f,.018f,Quaternion.identity));}
            if(skin==AvatarSkin.CyberSamurai)Hat(rig,"samurai crest",Tone.Red,true);
            if(skin==AvatarSkin.CrystalKnight)Spots(rig,"crystal facets",Tone.Glow,false);
        }
        void Spots(CandyPlayerRig rig,string id,Tone tone,bool stripes)
        {
            Add(rig,id+" pattern",0,tone,b=>{for(int i=0;i<(stripes?8:24);i++){float a=i*2.399963f;float y=.73f+(i%8)*.065f;if(stripes)b.Ring(new Vector3(0,y,0),.20f,.012f,Quaternion.Euler(90,0,0));else b.Sphere(new Vector3(Mathf.Cos(a)*.19f,y,Mathf.Sin(a)*.15f),new Vector3(.025f,.03f,.012f));}});
        }
    }
}
