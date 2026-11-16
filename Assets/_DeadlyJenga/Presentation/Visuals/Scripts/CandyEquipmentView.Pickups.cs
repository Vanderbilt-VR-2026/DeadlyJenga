using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.UI;
namespace DeadlyJenga.Presentation
{
    public sealed partial class CandyEquipmentView
    {
        readonly struct PowerupLook
        {
            public readonly Color Upper, Lower;
            public readonly Vector3 Dimensions;
            public readonly Vector4 Flow;
            public readonly float Clearance, Lift, Facets;

            public PowerupLook(Color upper, Color lower, Vector3 dimensions, Vector4 flow,
                float clearance, float lift, float facets)
            {
                Upper = upper;
                Lower = lower;
                Dimensions = dimensions;
                Flow = flow;
                Clearance = clearance;
                Lift = lift;
                Facets = facets;
            }
        }

        static readonly PowerupLook SpeedLook = new(
            new Color(.28f, 1f, .54f), new Color(.83f, 1f, .18f),
            new Vector3(.88f, .36f, .46f), new Vector4(1, 0, .32f, 2.8f), .23f, .11f, 0);
        static readonly PowerupLook JumpLook = new(
            new Color(.92f, .56f, 1f), new Color(.61f, .27f, 1f),
            new Vector3(.49f, .84f, .49f), new Vector4(0, 1, 0, 1.48f), .42f, .29f, .82f);
        static readonly PowerupLook HandLook = new(
            new Color(.48f, .95f, 1f), new Color(.10f, .59f, 1f),
            new Vector3(.70f, .58f, .70f), new Vector4(.54f, .39f, .72f, .95f), .35f, .19f, 0);

        static PowerupLook GetPowerupLook(EquipmentKind kind) => kind switch
        {
            EquipmentKind.Speed => SpeedLook,
            EquipmentKind.Jump => JumpLook,
            _ => HandLook
        };
        static readonly Color DroppedColor = new Color(1f,.65f,.19f);
        static readonly Color ReadyColor = new Color(.25f,.90f,1f);
        static readonly Color FocusColor = new Color(.65f,1f,.43f);
        Material pickupMarkerMaterial;
        Material weaponOrbMaterial;
        Mesh weaponOrbMesh;
        Canvas pickupTag;
        RawImage pickupTagPanel, pickupTagPointer, pickupTagKeycap;
        Text pickupTagName, pickupTagAction, pickupTagKey, pickupTagSwap;
        Material pickupGlass, pickupKey, pickupPointer;
        Color defaultTagGlass, defaultTagMint, defaultTagLime;
        EquipmentKind styledTag = (EquipmentKind)(-1);
        const float TagWidth = 310f;
        const float TagHeight = 94f;
        const float DetailHeight = 26f;
        static readonly Color PickupInk = new Color32(96, 255, 154, 255);
        static readonly Color SwapInk = new Color32(255, 217, 127, 255);

        bool BuildPickupTag()
        {
            var style = Resources.Load<Material>("CandyPickupHologramStyle");
            if (!style) return false;

            var root = new GameObject("Focused pickup tag", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            pickupTag = root.GetComponent<Canvas>();
            pickupTag.renderMode = RenderMode.WorldSpace;
            pickupTag.sortingOrder = 36;

            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(TagWidth, TagHeight);
            var font = Resources.Load<Font>("Fonts/Fredoka-Bold");
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            defaultTagGlass = style.GetColor("_Glass");
            defaultTagMint = style.GetColor("_Mint");
            defaultTagLime = style.GetColor("_Lime");
            pickupPointer = new Material(style) { name = "Pickup hologram pointer" };
            pickupPointer.SetFloat("_Shape", 2);
            pickupGlass = new Material(style) { name = "Pickup hologram glass" };
            pickupKey = new Material(style) { name = "Pickup hologram keycap" };
            pickupKey.SetFloat("_Shape", 1);

            pickupTagPointer = TagSurface(rect, "Glass pointer", new Vector2(36, 22), pickupPointer);
            pickupTagPanel = TagSurface(rect, "Glass panel", new Vector2(TagWidth, TagHeight), pickupGlass);
            pickupTagKeycap = TagSurface(rect, "Gummy keycap", new Vector2(42, 34), pickupKey);
            pickupTagName = TagText(rect, font, "Item name", 29, 18, Color.white);
            pickupTagAction = TagText(rect, font, "Pickup action", 27, -17, PickupInk);
            pickupTagKey = TagText(rect, font, "Input key", 23, -17, Color.white);
            pickupTagSwap = TagText(rect, font, "Item details", 16, -46, SwapInk);
            return true;
        }

        static RawImage TagSurface(RectTransform parent, string name, Vector2 size, Material material)
        {
            var surface = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            surface.transform.SetParent(parent, false);
            surface.rectTransform.sizeDelta = size;
            surface.material = material;
            surface.raycastTarget = false;
            return surface;
        }

        static Text TagText(RectTransform parent, Font font, string name, int size, float y, Color color)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.rectTransform.sizeDelta = new Vector2(TagWidth - 30, 34);
            label.rectTransform.anchoredPosition = new Vector2(0, y);
            label.font = font;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.raycastTarget = false;
            label.supportRichText = false;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = size;
            return label;
        }

        bool PresentPickupTag(ISessionInfo session, EquipmentState current, EquipmentPickup pickup, PickupVisual view)
        {
            var point = pickup.position;
            var depth = eye.WorldToViewportPoint(point);
            if (depth.z < eye.nearClipPlane + .12f || depth.x < -.08f || depth.x > 1.08f ||
                depth.y < -.1f || depth.y > 1.06f) return false;

            var oldKind = current == null ? EquipmentKind.None : current.activePower != EquipmentKind.None ? current.activePower :
                current.builtIn ? EquipmentKind.None : current.kind;
            bool swap = oldKind != EquipmentKind.None;
            bool needsHolster = vr && current?.drawn == true && !current.builtIn && current.kind != EquipmentKind.Dagger;
            bool bothHandsBusy = needsHolster && (EquipmentSpecifications.Get(current.kind).twoHanded ||
                current.kind is EquipmentKind.DualUzi or EquipmentKind.Chainsaw or EquipmentKind.LaserClaws);

            // Use the target's depth so the panel stays in the world. Its desktop size tracks the view angle.
            float viewHeight = eye.orthographic ? 2 * eye.orthographicSize :
                2 * depth.z / Mathf.Abs(eye.projectionMatrix.m11);
            float scale = vr ? .00105f : Mathf.Clamp(viewHeight * .39f / TagWidth, .00075f, .004f);
            float rise = vr ? .23f : Mathf.Max(.21f, viewHeight * .15f);
            bool showDetails = swap || pickup.rounds >= 0;
            float extra = showDetails ? DetailHeight : 0;
            var position = point + eye.transform.up * (rise + extra * scale);
            if (view.orb)
            {
                float clearance = view.orbRadius + .04f + (TagHeight * .5f + 18 + extra) * scale;
                position = view.orb.position + eye.transform.up * clearance;
            }
            var viewport = eye.WorldToViewportPoint(position);
            float halfWidth = TagWidth * scale / (2 * viewHeight * eye.aspect);
            float shift = Mathf.Clamp(viewport.x, halfWidth + .015f, .985f - halfWidth) - viewport.x;
            if (viewport.y + TagHeight * scale / (2 * viewHeight) > 1.02f ||
                Mathf.Abs(shift) > halfWidth * .77f) return false;

            if (!pickupTag && !BuildPickupTag()) return false;
            position += eye.transform.right * (shift * viewHeight * eye.aspect);
            pickupTag.enabled = true;
            pickupTag.worldCamera = eye;
            pickupTag.transform.SetPositionAndRotation(position, eye.transform.rotation);
            pickupTag.transform.localScale = Vector3.one * scale;

            SetPickupTagPalette(pickup.kind);
            pickupTagName.text = Name(pickup.kind).ToUpperInvariant();
            string action = swap ? "Swap" : "Pick up";
            string input = SwapInputHint(session);
            bool power = EquipmentSpecifications.Get(pickup.kind).power;
            var ink = power ? GetPowerupLook(pickup.kind).Upper : PickupInk;
            SetPickupTagAction(input, action, swap, needsHolster, bothHandsBusy, ink);

            string detail = swap ? "Leaves " + Name(oldKind).ToUpperInvariant() + " here" : "";
            if (pickup.rounds >= 0)
                detail += (swap ? "  ·  " : "") + pickup.rounds + " rounds";
            pickupTagSwap.enabled = showDetails;
            pickupTagSwap.text = detail;
            pickupTagPanel.rectTransform.sizeDelta = new Vector2(TagWidth, TagHeight + extra);
            pickupTagPanel.rectTransform.anchoredPosition = new Vector2(0, -extra * .5f);
            pickupGlass.SetFloat("_Aspect", TagWidth / (TagHeight + extra));
            pickupTagPointer.rectTransform.anchoredPosition =
                new Vector2(-shift * viewHeight * eye.aspect / scale, -TagHeight * .5f - extra - 7);
            return true;
        }

        void SetPickupTagPalette(EquipmentKind kind)
        {
            if (styledTag == kind) return;
            styledTag = kind;
            bool power = EquipmentSpecifications.Get(kind).power;
            var look = GetPowerupLook(kind);
            var mint = power ? look.Upper : defaultTagMint;
            var lime = power ? look.Lower : defaultTagLime;
            var glass = power ? new Color(lime.r * .33f, lime.g * .33f, lime.b * .33f, defaultTagGlass.a) :
                defaultTagGlass;
            pickupGlass.SetColor("_Glass", glass);
            pickupGlass.SetColor("_Mint", mint);
            pickupGlass.SetColor("_Lime", lime);
            pickupPointer.SetColor("_Mint", mint);
            pickupPointer.SetColor("_Lime", lime);
            pickupKey.SetColor("_Lime", lime);
        }

        void SetPickupTagAction(string input, string action, bool swap, bool needsHolster, bool bothHandsBusy, Color ink)
        {
            var tint = swap ? SwapInk : ink;
            pickupTagAction.color = tint;
            pickupTagAction.text = bothHandsBusy ? "Holster to swap" : needsHolster ? action + " · Use free hand" :
                vr ? action + " · " + input : "· " + action.ToUpperInvariant();
            pickupTagKeycap.enabled = pickupTagKey.enabled = !vr;
            pickupTagKey.text = vr ? "" : input;
            float keyWidth = vr ? 0 : Mathf.Max(42, pickupTagKey.preferredWidth + 14);
            float actionWidth = vr ? TagWidth - 30 : pickupTagAction.preferredWidth + 4;
            pickupTagKeycap.rectTransform.sizeDelta = new Vector2(keyWidth, 34);
            pickupTagKeycap.rectTransform.anchoredPosition = new Vector2(-actionWidth * .5f - 4, -17);
            pickupTagKey.rectTransform.anchoredPosition = pickupTagKeycap.rectTransform.anchoredPosition;
            pickupTagKey.rectTransform.sizeDelta = new Vector2(keyWidth, 34);
            pickupKey.SetFloat("_Aspect", keyWidth / 34);
            pickupKey.SetFloat("_Swap", swap ? 1 : 0);
            pickupTagAction.rectTransform.sizeDelta = new Vector2(actionWidth, 34);
            pickupTagAction.rectTransform.anchoredPosition = new Vector2(vr ? 0 : keyWidth * .5f + 4, -17);
        }

        void HidePickupTag()
        {
            if (pickupTag) pickupTag.enabled = false;
        }

        void DestroyPickupTag()
        {
            if (pickupTag) Destroy(pickupTag.gameObject);
            if (pickupGlass) Destroy(pickupGlass);
            if (pickupKey) Destroy(pickupKey);
            if (pickupPointer) Destroy(pickupPointer);
        }

        PickupVisual CreatePickup(EquipmentKind kind,bool dropped)
        {
            var root=new GameObject("Visual - "+Name(kind)+" pickup").transform;
            root.SetParent(transform,false);
            var anchor=new GameObject("Pickup nameplate").transform;
            anchor.SetParent(root,false);
            anchor.localPosition=Vector3.up*.44f;
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var shadow=PickupLabel(anchor,font,"Pickup label shadow");
            shadow.transform.localPosition=new Vector3(.005f,-.006f,.005f);
            shadow.color=new Color(.035f,.045f,.07f,.96f);
            var label=PickupLabel(anchor,font,"Pickup label");

            if(!pickupMarkerMaterial)
            {
                var shader=Resources.Load<Shader>("CandyOrdnance");
                if(shader)
                {
                    pickupMarkerMaterial=new Material(shader){name="World loot markers"};
                    pickupMarkerMaterial.SetFloat("_Ribbon",1);
                }
            }
            var view=new PickupVisual{root=root,label=label,labelShadow=shadow,kind=kind,dropped=dropped};
            if (EquipmentSpecifications.Get(kind).power)
                BuildPowerupOrb(view);
            if(pickupMarkerMaterial)
            {
                view.ring=PickupLine(root,"Pickup ground ring",32,true);
                view.beam=PickupLine(root,"Pickup beacon",2,false);
            }
            return view;
        }

        static TextMesh PickupLabel(Transform parent,Font font,string name)
        {
            var label=new GameObject(name,typeof(TextMesh)).GetComponent<TextMesh>();
            label.transform.SetParent(parent,false);
            label.anchor=TextAnchor.LowerCenter;
            label.alignment=TextAlignment.Center;
            label.fontStyle=FontStyle.Bold;
            label.fontSize=48;
            label.characterSize=.007f;
            label.font=font;
            label.GetComponent<Renderer>().sharedMaterial=font.material;
            return label;
        }

        LineRenderer PickupLine(Transform root,string name,int count,bool loop)
        {
            var child=new GameObject(name).transform;
            child.SetParent(root,false);
            var line=child.gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace=false;
            line.loop=loop;
            line.positionCount=count;
            line.sharedMaterial=pickupMarkerMaterial;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows=false;
            if(loop)
                for(int i=0;i<count;i++)
                {
                    float a=i*Mathf.PI*2/count;
                    line.SetPosition(i,new Vector3(Mathf.Cos(a)*.25f,-.20f,Mathf.Sin(a)*.25f));
                }
            else
            {
                line.SetPosition(0,new Vector3(0,-.20f,0));
                line.SetPosition(1,new Vector3(0,.88f,0));
            }
            return line;
        }

        bool ShowPickup(PickupVisual view, EquipmentPickup pickup, ISessionInfo session, EquipmentState current, bool focused)
        {
            bool ready=pickup.availableIn<=0;
            bool resting=pickup.station&&!ready;
            if(!pickup.station&&!ready)view.dropped=true;
            view.root.position=pickup.position;
            float distance=eye?(eye.transform.position-pickup.position).sqrMagnitude:float.PositiveInfinity;
            bool power = EquipmentSpecifications.Get(view.kind).power;
            var look = GetPowerupLook(view.kind);
            var tint = power ? (focused ? Color.Lerp(look.Upper, Color.white, .25f) : look.Lower) :
                focused ? FocusColor : view.dropped ? DroppedColor : ReadyColor;
            if(view.ring)
            {
                view.ring.enabled=!resting&&distance<18*18;
                view.ring.transform.localScale=new Vector3(focused?1.24f:1,1,focused?1.24f:1);
                float width=focused?.041f:view.dropped?.029f:.022f;
                view.ring.startWidth=view.ring.endWidth=width;
                view.ring.startColor=view.ring.endColor=tint;
                view.beam.enabled=!view.orb&&!resting&&distance<16*16;
                view.beam.startWidth=focused?.12f:view.dropped?.095f:.055f;
                view.beam.endWidth=.014f;
                view.beam.startColor=new Color(tint.r,tint.g,tint.b,focused?.70f:view.dropped?.52f:.30f);
                view.beam.endColor=new Color(tint.r,tint.g,tint.b,0);
            }
            if (view.model || view.orb)
            {
                float settle = view.dropped && !ready ? Mathf.Clamp01((pickup.availableIn - .72f) / .28f) : 0;
                float time = Time.unscaledTime;
                float bob = .025f * Mathf.Sin(time * 2.4f + pickup.id * .27f);
                float lift = power ? look.Lift : view.orb ? view.orbRadius - .14f : 0;
                var position = Vector3.up * (lift + bob + .38f * settle * settle);
                if (view.model)
                {
                    view.model.gameObject.SetActive(!resting);
                    view.model.localPosition = position;
                    view.model.localRotation = view.usesPickupIcon && eye
                        ? Quaternion.Inverse(view.root.rotation) * eye.transform.rotation
                        : Quaternion.Euler(9 + settle * 45, time * 19 + pickup.id * 41, settle * 25);
                }
                if (view.orb)
                {
                    view.orb.gameObject.SetActive(!resting && distance < 24 * 24);
                    view.orb.localPosition = position;
                    float turn = time * 17 + pickup.id * 41;
                    if (view.kind == EquipmentKind.Speed)
                    {
                        var facing = eye ? Vector3.ProjectOnPlane(eye.transform.forward, Vector3.up) : Vector3.forward;
                        if (facing.sqrMagnitude > .01f)
                            view.orb.rotation = Quaternion.LookRotation(facing);
                        view.outerOrb.localRotation = Quaternion.Euler(0, 0, 12);
                        view.innerOrb.localRotation = Quaternion.Euler(0, 0, -8);
                    }
                    else
                    {
                        bool vertical = view.kind == EquipmentKind.Jump;
                        view.outerOrb.localRotation = vertical ? Quaternion.Euler(0, turn, 0) : Quaternion.Euler(15, turn, 8);
                        view.innerOrb.localRotation = vertical ? Quaternion.Euler(0, -turn * .63f, 0) :
                            Quaternion.Euler(-26, -turn * .63f, 34);
                    }
                }
            }
            bool hasTag = focused && PresentPickupTag(session, current, pickup, view);
            bool showLabel=eye&&!hasTag&&(focused||distance<3.5f*3.5f);
            var anchor=view.label.transform.parent;
            anchor.gameObject.SetActive(showLabel);
            if(!showLabel)return hasTag;
            if (view.orb && !resting)
                anchor.localPosition = view.orb.localPosition + Vector3.up * (view.orbRadius + .035f);
            else
                anchor.localPosition = Vector3.up * .44f;
            var facing=anchor.position-eye.transform.position;
            if(facing.sqrMagnitude>.0001f)anchor.rotation=Quaternion.LookRotation(facing);
            string detail=resting?"Ready in "+Mathf.CeilToInt(pickup.availableIn)+"s":
                !ready?"Dropped · ready shortly":view.dropped?"Dropped gear":pickup.station?"Weapon station":"";
            if(focused&&pickup.rounds>=0)detail=pickup.rounds+" rounds";
            string label=Name(pickup.kind)+(detail.Length>0?"\n"+detail:"");
            if(view.label.text!=label)view.label.text=view.labelShadow.text=label;
            view.label.color=resting?new Color(.70f,.74f,.79f):Color.Lerp(tint,Color.white,.30f);
            return hasTag;
        }

        bool EnsureOrbAssets()
        {
            if (!weaponOrbMaterial)
            {
                var shader = Resources.Load<Shader>("CandyWeaponPickupOrb");
                if (!shader) return false;
                weaponOrbMaterial = new Material(shader) { name = "Layered weapon pickup glow" };
            }

            if (!weaponOrbMesh)
            {
                var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
                weaponOrbMesh = Instantiate(sphere);
                weaponOrbMesh.name = "Scalloped pickup sphere";
                var vertices = weaponOrbMesh.vertices;
                var center = weaponOrbMesh.bounds.center;
                for (int index = 0; index < vertices.Length; index++)
                {
                    var direction = (vertices[index] - center).normalized;
                    float scallop = Mathf.Sin(direction.x * 16 + direction.y * 6) *
                        Mathf.Sin(direction.z * 13 - direction.y * 7);
                    float swell = Mathf.Sin(direction.y * 9 + direction.z * 4);
                    vertices[index] = direction * (.48f + .014f * scallop + .006f * swell);
                }
                weaponOrbMesh.vertices = vertices;
                weaponOrbMesh.RecalculateNormals();
                weaponOrbMesh.RecalculateBounds();
            }

            return true;
        }

        void BuildWeaponOrb(PickupVisual view, float modelRadius)
        {
            if (!EnsureOrbAssets()) return;
            view.orbRadius = Mathf.Max(.42f, (modelRadius + .025f) / .82f);
            view.orb = new GameObject("Layered weapon pickup orb").transform;
            view.orb.SetParent(view.root, false);
            view.outerOrb = CreateOrbShell(view.orb, "Outer pickup shell", Vector3.one * (view.orbRadius * 2), .78f, 0);
            view.innerOrb = CreateOrbShell(view.orb, "Inner pickup shell", Vector3.one * (view.orbRadius * 1.79f), .66f, 1);
            view.innerOrb.localPosition = new Vector3(.014f, -.012f, .009f);
        }

        void BuildPowerupOrb(PickupVisual view)
        {
            if (!EnsureOrbAssets()) return;
            var look = GetPowerupLook(view.kind);
            view.orbRadius = look.Clearance;
            view.orb = new GameObject(Name(view.kind) + " pickup aura").transform;
            view.orb.SetParent(view.root, false);
            view.outerOrb = CreateOrbShell(view.orb, "Outer powerup shell", look.Dimensions, .96f, 0, look);
            view.innerOrb = CreateOrbShell(view.orb, "Inner powerup shell", look.Dimensions * .81f, .80f, 1, look);
            if (view.kind == EquipmentKind.Speed)
                view.innerOrb.localPosition = new Vector3(-.09f, -.035f, .02f);
        }

        Transform CreateOrbShell(Transform parent, string name, Vector3 dimensions, float opacity, float layer,
            PowerupLook? style = null)
        {
            var shell = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            shell.transform.SetParent(parent, false);
            shell.transform.localScale = dimensions;
            shell.GetComponent<MeshFilter>().sharedMesh = weaponOrbMesh;
            var renderer = shell.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = weaponOrbMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var properties = new MaterialPropertyBlock();
            properties.SetFloat("_Opacity", opacity);
            properties.SetFloat("_Layer", layer);
            if (style is PowerupLook look)
            {
                properties.SetColor("_Mint", look.Upper);
                properties.SetColor("_Lime", look.Lower);
                properties.SetVector("_Flow", look.Flow);
                properties.SetFloat("_Facets", look.Facets);
            }
            renderer.SetPropertyBlock(properties);
            return shell.transform;
        }

        void UpdatePickupModel(PickupVisual view)
        {
            if(!view.model&&EquipmentModelData.Parts.TryGetValue(view.kind,out var parts))
            {
                view.model=new GameObject("Live equipment model").transform;view.model.SetParent(view.root,false);
                Bounds bounds=default;bool first=true;
                foreach(var part in parts)
                {
                    var go=new GameObject("Equipment geometry",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(view.model,false);go.transform.localPosition=part.local.GetColumn(3);go.transform.localRotation=part.local.rotation;go.transform.localScale=part.local.lossyScale;go.GetComponent<MeshFilter>().sharedMesh=part.mesh;
                    var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=part.material;var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",part.color);renderer.SetPropertyBlock(block);renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                    var b=renderer.bounds;if(first){bounds=b;first=false;}else bounds.Encapsulate(b);
                }
                if (!first)
                {
                    float scale = .52f / Mathf.Max(.1f, bounds.size.x, bounds.size.y, bounds.size.z);
                    var center = view.model.InverseTransformPoint(bounds.center);
                    var content = new GameObject("Centered model geometry").transform;
                    content.SetParent(view.model, false);
                    while (view.model.childCount > 1)
                        view.model.GetChild(0).SetParent(content, false);
                    content.localPosition = -center;
                    view.model.localScale = Vector3.one * scale;
                    if (view.kind != EquipmentKind.None && !EquipmentSpecifications.Get(view.kind).power)
                        BuildWeaponOrb(view, bounds.extents.magnitude * scale);
                }
                else if (view.kind != EquipmentKind.None && !EquipmentSpecifications.Get(view.kind).power)
                {
                    var icon = CandyWeaponIcons.Get(view.kind);
                    if (icon)
                    {
                        var renderer = view.model.gameObject.AddComponent<SpriteRenderer>();
                        renderer.sprite = icon;
                        renderer.color = new Color(1f, .94f, .78f);
                        view.model.localScale = Vector3.one * (.39f / Mathf.Max(icon.bounds.size.x, icon.bounds.size.y));
                        view.usesPickupIcon = true;
                    }
                    BuildWeaponOrb(view, .26f);
                }
            }
        }
    }
}
