using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyWeaponVisual
    {
        sealed class TemplatePart {public Mesh mesh;public Color color;public bool moving;}
        sealed class Template {public bool moving;public Vector3 pivot;public TemplatePart[] parts;}
        sealed class MeshGroup {public Color color;public bool moving;public readonly List<CombineInstance> meshes=new List<CombineInstance>();}
        static readonly Dictionary<EquipmentKind,Template> templates=new Dictionary<EquipmentKind,Template>();
        void BuildArsenal(Transform owner,EquipmentKind value)
        {
            if(templates.TryGetValue(value,out var template))
            {
                rotor=null;if(template.moving){rotor=new GameObject("Animated weapon assembly").transform;rotor.SetParent(owner,false);rotor.localPosition=template.pivot;}
                foreach(var part in template.parts)MeshPart(part.moving?rotor:owner,part.mesh.name,part.mesh,part.color);return;
            }
            BuildArsenalPieces(owner,value);var groups=new Dictionary<string,MeshGroup>();
            var renderers=owner.GetComponentsInChildren<MeshRenderer>();
            foreach(var renderer in renderers)
            {
                var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);var color=properties.GetColor("_BaseColor");bool moving=rotor&&renderer.transform.IsChildOf(rotor);var parent=moving?rotor:owner;string key=(moving?"motor":"body")+((Color32)color).ToString();
                if(!groups.TryGetValue(key,out var group)){group=new MeshGroup{color=color,moving=moving};groups[key]=group;}
                group.meshes.Add(new CombineInstance{mesh=renderer.GetComponent<MeshFilter>().sharedMesh,transform=parent.worldToLocalMatrix*renderer.transform.localToWorldMatrix});
            }
            foreach(var renderer in renderers){renderer.enabled=false;Object.Destroy(renderer.gameObject);}
            var parts=new List<TemplatePart>();
            foreach(var pair in groups)
            {
                var group=pair.Value;var mesh=new Mesh{name=(group.moving&&(value==EquipmentKind.Chainsaw||value==EquipmentKind.SawStaff)?"Serrated saw wheel ":"Candy weapon ")+value+" "+pair.Key};mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(group.meshes.ToArray(),true,true);mesh.RecalculateBounds();
                MeshPart(group.moving?rotor:owner,mesh.name,mesh,group.color);parts.Add(new TemplatePart{mesh=mesh,color=group.color,moving=group.moving});
            }
            templates[value]=new Template{moving=rotor,pivot=rotor?rotor.localPosition:Vector3.zero,parts=parts.ToArray()};
        }
    }
}
