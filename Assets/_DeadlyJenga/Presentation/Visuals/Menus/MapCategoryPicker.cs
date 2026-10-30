using System;
using System.Collections.Generic;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Shared presentation of the map options supplied by the active session.
    internal sealed class MapCategoryPicker
    {
        internal static readonly string[] Categories={"Castles", "Game-mode arenas", "Social & seasonal islands", "Festival cities", "Structure experiments", "Workshops", "Test & custom maps", "Other maps", "Epic connected cities"};
        internal sealed class Row
        {
            internal int category,count;
            internal LobbyOption map;
            internal bool current;
            internal bool collection,experimental;
        }
        readonly List<LobbyOption> snapshot=new List<LobbyOption>();
        readonly List<Row> rows=new List<Row>();
        string selected;
        readonly int[] open={-1,-1};
        readonly bool[] expanded={true,false};
        int anchorGroup,anchorCategory=-1;
        bool anchorCollection;
        bool initialized;
        internal IReadOnlyList<Row> Rows=>rows;
        internal void Update(IReadOnlyList<LobbyOption> options,string current)
        {
            bool changed=!initialized||selected!=current||snapshot.Count!=(options?.Count??0);
            if(!changed)for(int i=0;i<snapshot.Count;i++)if(!ReferenceEquals(snapshot[i],options[i])){changed=true;break;}
            if(!changed)return;
            snapshot.Clear();if(options!=null)for(int i=0;i<options.Count;i++)if(options[i]!=null)snapshot.Add(options[i]);
            if(!initialized||selected!=current)
            {
                var active=snapshot.Find(m=>m.id==current);
                if(active!=null)
                {
                    int group=MapBrowseStatus.Experimental(active)?1:0;
                    expanded[group]=true;open[group]=Category(active);
                    anchorGroup=group;anchorCategory=open[group];anchorCollection=false;
                }
            }
            selected=current;initialized=true;Rebuild();
        }
        internal void Toggle(Row row)
        {
            if(row==null||row.map!=null)return;
            int group=row.experimental?1:0;
            anchorGroup=group;anchorCategory=row.category;anchorCollection=row.collection;
            if(row.collection)expanded[group]=!expanded[group];
            else open[group]=open[group]==row.category?-1:row.category;
            Rebuild();
        }
        internal int HeaderIndex
        {
            get
            {
                for(int i=0;i<rows.Count;i++)if(rows[i].map==null&&rows[i].experimental==(anchorGroup==1)&&rows[i].collection==anchorCollection&&(anchorCollection||rows[i].category==anchorCategory))return i;
                return 0;
            }
        }
        internal string Title(Row row)
        {
            if(row.map!=null)return "        "+row.map.title+(row.map.id==selected?"  [current]":"")+(row.map.available?"":"  - coming soon");
            int group=row.experimental?1:0;
            if(row.collection)return (expanded[group]?"v  ":">  ")+(row.experimental?"EXPERIMENTAL / UNFINISHED":"MAIN MAPS")+" ("+row.count+")"+(row.current?"  [current]":"");
            return "    "+(open[group]==row.category?"v  ":">  ")+Categories[row.category]+" ("+row.count+")"+(row.current?"  [current]":"");
        }
        void Rebuild()
        {
            rows.Clear();
            var buckets=new List<LobbyOption>[2,Categories.Length];
            foreach(var map in snapshot)
            {
                int group=MapBrowseStatus.Experimental(map)?1:0,category=Category(map);
                if(buckets[group,category]==null)buckets[group,category]=new List<LobbyOption>();
                buckets[group,category].Add(map);
            }
            for(int group=0;group<2;group++)
            {
                bool experimental=group==1;int total=0;bool selectedHere=false;
                for(int c=0;c<Categories.Length;c++)if(buckets[group,c]!=null)
                {total+=buckets[group,c].Count;selectedHere|=buckets[group,c].Exists(map=>map.id==selected);}
                if(total==0)continue;
                rows.Add(new Row{category=-1,count=total,current=selectedHere,collection=true,experimental=experimental});
                if(!expanded[group])continue;
                for(int c=0;c<Categories.Length;c++)
                {
                    var maps=buckets[group,c];int count=maps?.Count??0;
                    if(count==0)continue;
                    bool current=maps.Exists(map=>map.id==selected);
                    rows.Add(new Row{category=c,count=count,current=current,experimental=experimental});
                    if(open[group]==c)foreach(var map in maps)rows.Add(new Row{category=c,map=map,experimental=experimental});
                }
            }
        }
        internal static int Category(LobbyOption map)
        {
            string id=(map.id??"").ToLowerInvariant();if(id.StartsWith("mode-",StringComparison.Ordinal))id=id.Substring(5);
            string title=(map.title??"").ToLowerInvariant();
            if(id.StartsWith("epic-",StringComparison.Ordinal))return 8;
            if(id.Contains("festival"))return 3;
            if(id.StartsWith("workshop-",StringComparison.Ordinal))return 5;
            if(id.StartsWith("sparse-",StringComparison.Ordinal)||id=="mayhem-foundry"||id=="city-tower-exhibition"||id=="tower-lab-island"||id=="megablock-tower")return 4;
            if(id.Contains("castle"))return 0;
            if(id.Contains("test")||id.Contains("drafter")||id=="andy")return 6;
            if(id.Contains("mystery")||id.Contains("social")||id.Contains("christmas")||id.Contains("cantina")||id.Contains("tiki")||id.Contains("breakwater")||title.Contains("murder")||title.Contains("christmas"))return 2;
            if(id=="ctf"||id=="infection"||id=="survival"||id=="territory"||id=="koth"||id=="tickets"||id=="buildup")return 1;
            return 7;
        }
    }
}
