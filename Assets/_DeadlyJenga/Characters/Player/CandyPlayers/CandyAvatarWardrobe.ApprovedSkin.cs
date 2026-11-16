using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyAvatarWardrobe
    {
        readonly List<(Renderer renderer, WardrobeSkinAsset.Part part)> approvedParts = new();
        WardrobeSkinAsset approvedOriginal, appliedSkin;
        Color? appliedTeam;
        Color appliedAvatarColor;
        Mesh baseSkinHead;
        Material[] baseSkinBodyMaterials, baseSkinHeadMaterials;
        Vector3 baseSkinHeadScale;
        bool baseSkinBodyVisible;

        bool TryUseApprovedSkin(CandyPlayerRig rig)
        {
            if (appearance.skin == AvatarSkin.Default || appearance.poweredArmor) return false;
            approvedOriginal = Resources.Load<WardrobeSkinAsset>("WardrobeSkins/" + appearance.skin);
            if (!approvedOriginal) return false;
            baseSkinHead = rig.headRenderer.sharedMesh;
            baseSkinBodyMaterials = rig.bodyRenderer.sharedMaterials;
            baseSkinHeadMaterials = rig.headRenderer.sharedMaterials;
            baseSkinHeadScale = rig.HeadAnchor.localScale;
            baseSkinBodyVisible = rig.bodyRenderer.enabled;
            return true;
        }

        void ClearApprovedSkin(CandyPlayerRig rig)
        {
            if (!approvedOriginal) return;
            RemoveApprovedParts();
            ClearApprovedOverrides(rig.bodyRenderer, appliedSkin.bodyMaterials.Length);
            ClearApprovedOverrides(rig.headRenderer, appliedSkin.headMaterials.Length);
            rig.bodyRenderer.sharedMaterials = baseSkinBodyMaterials;
            rig.headRenderer.sharedMaterials = baseSkinHeadMaterials;
            rig.headRenderer.sharedMesh = baseSkinHead;
            rig.HeadAnchor.localScale = baseSkinHeadScale;
            rig.bodyRenderer.enabled = baseSkinBodyVisible;
            approvedOriginal = null;
            appliedSkin = null;
            appliedTeam = null;
        }

        void RemoveApprovedParts()
        {
            foreach (var (renderer, _) in approvedParts)
            {
                if (!renderer) continue;
                renderer.gameObject.SetActive(false);
                Destroy(renderer.gameObject);
            }
            approvedParts.Clear();
        }

        void RefreshApprovedSkin(CandyPlayerRig rig, Color? team)
        {
            bool teamChanged = !appliedSkin || appliedTeam != team;
            Color avatarColor = rig.BaseAppearanceColor;
            if (teamChanged)
            {
                var skin = team.HasValue
                    ? Resources.Load<WardrobeSkinAsset>("WardrobeSkins/" + appearance.skin + "-" + ColorUtility.ToHtmlStringRGB(team.Value))
                    : approvedOriginal;
                if (!skin) skin = approvedOriginal;
                if (skin != appliedSkin)
                {
                    RemoveApprovedParts();
                    if (appliedSkin)
                    {
                        ClearApprovedOverrides(rig.bodyRenderer, appliedSkin.bodyMaterials.Length);
                        ClearApprovedOverrides(rig.headRenderer, appliedSkin.headMaterials.Length);
                    }
                    rig.bodyRenderer.sharedMesh = skin.bodyMesh ? skin.bodyMesh : originalBody;
                    rig.headRenderer.sharedMesh = skin.headMesh ? skin.headMesh : baseSkinHead;
                    rig.bodyRenderer.sharedMaterials = skin.bodyMaterials;
                    rig.headRenderer.sharedMaterials = skin.headMaterials;
                    rig.HeadAnchor.localScale = skin.headScale;
                    foreach (var part in skin.parts)
                    {
                        var piece = new GameObject(part.label);
                        var pose = piece.transform;
                        pose.SetParent(WardrobeSkinAsset.FindBone(rig, part.anchor), false);
                        pose.SetLocalPositionAndRotation(part.position, part.rotation);
                        pose.localScale = part.scale;
                        Renderer renderer;
                        if (part.bones != null && part.bones.Length > 0)
                        {
                            var skinned = piece.AddComponent<SkinnedMeshRenderer>();
                            skinned.sharedMesh = part.mesh;
                            skinned.bones = System.Array.ConvertAll(part.bones, index => WardrobeSkinAsset.FindBone(rig, index));
                            skinned.rootBone = WardrobeSkinAsset.FindBone(rig, part.rootBone);
                            skinned.localBounds = part.bounds;
                            renderer = skinned;
                        }
                        else
                        {
                            piece.AddComponent<MeshFilter>().sharedMesh = part.mesh;
                            renderer = piece.AddComponent<MeshRenderer>();
                        }
                        renderer.sharedMaterials = part.materials;
                        renderer.shadowCastingMode = part.shadows;
                        approvedParts.Add((renderer, part));
                    }
                    appliedSkin = skin;
                }
                appliedTeam = team;
                foreach (var (renderer, part) in approvedParts)
                    if (renderer) ApplyApprovedTeamTint(renderer, part.materials, team);
            }

            rig.bodyRenderer.enabled = appliedSkin.bodyVisible;
            rig.headRenderer.enabled = appliedSkin.headVisible;
            if (teamChanged || appliedAvatarColor != avatarColor)
            {
                ApplyApprovedTeamTint(rig.bodyRenderer, appliedSkin.bodyMaterials, team);
                ApplyApprovedTeamTint(rig.headRenderer, appliedSkin.headMaterials, team);
                appliedAvatarColor = avatarColor;
            }
        }

        void ApplyApprovedTeamTint(Renderer renderer, Material[] materials, Color? team)
        {
            // Approved team assets already contain their authored finish. Otherwise
            // keep the skin's original finish and apply team color to the main body.
            ClearApprovedOverrides(renderer, materials.Length);
            if (!team.HasValue || appliedSkin != approvedOriginal) return;
            var main = approvedOriginal.bodyMaterials[0];
            for (int slot = 0; slot < materials.Length; slot++)
            {
                if (materials[slot] != main) continue;
                properties.Clear();
                properties.SetColor("_BaseColor", team.Value);
                renderer.SetPropertyBlock(properties, slot);
            }
        }

        static void ClearApprovedOverrides(Renderer renderer, int materialCount)
        {
            renderer.SetPropertyBlock(null);
            for (int slot = 0; slot < materialCount; slot++) renderer.SetPropertyBlock(null, slot);
        }

        void SetApprovedSkinFirstPerson(bool active)
        {
            foreach (var (renderer, part) in approvedParts)
            {
                if (!renderer) continue;
                bool blocksView = part.anchor <= 1 || CandyPlayerRig.IsTorsoSkinBone(part.anchor);
                renderer.shadowCastingMode = active && blocksView ? ShadowCastingMode.ShadowsOnly : part.shadows;
            }
        }
    }
}
