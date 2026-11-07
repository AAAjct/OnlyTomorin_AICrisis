using System;
using UnityEngine;

namespace TomorinTrial
{
    [Flags]
    public enum TrialOutfit : byte
    {
        None = 0,
        Hat = 1,
        Mask = 2,
        InitialSword = 4,
        MingShuang = 8,
        Swords = InitialSword | MingShuang,
        Default = Hat | Mask | MingShuang
    }

    static class TrialAppearance
    {
        const string OutfitKey = "Ink.Outfit";

        public static TrialOutfit Saved
        {
            get => Normalize((TrialOutfit)PlayerPrefs.GetInt(OutfitKey, (int)TrialOutfit.Default));
            set
            {
                PlayerPrefs.SetInt(OutfitKey, (int)Normalize(value));
                PlayerPrefs.Save();
            }
        }

        public static TrialOutfit Normalize(TrialOutfit outfit)
        {
            outfit &= TrialOutfit.Hat | TrialOutfit.Mask | TrialOutfit.Swords;
            if ((outfit & TrialOutfit.Swords) == TrialOutfit.Swords)
                outfit &= ~TrialOutfit.InitialSword;
            if ((outfit & TrialOutfit.Swords) == TrialOutfit.None)
                outfit |= TrialOutfit.InitialSword;
            return outfit;
        }

        public static void ApplyAccessories(Transform model, TrialOutfit outfit)
        {
            foreach (var part in model.GetComponentsInChildren<Transform>(true))
            {
                if (part.name == "RovingHat") part.gameObject.SetActive((outfit & TrialOutfit.Hat) != 0);
                if (part.name == "RovingMask") part.gameObject.SetActive((outfit & TrialOutfit.Mask) != 0);
            }
        }

        public static void FitModel(Transform model, Renderer[] bodyMeshes)
        {
            if (bodyMeshes.Length == 0) return;
            Bounds bounds = ModelBounds(bodyMeshes);
            if (bounds.size.y < .001f) return;
            model.localScale *= 1.68f / bounds.size.y;
            bounds = ModelBounds(bodyMeshes);
            Vector3 origin = model.parent.position;
            model.position += origin - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        static Bounds ModelBounds(Renderer[] bodyMeshes)
        {
            var bounds = bodyMeshes[0].bounds;
            foreach (var mesh in bodyMeshes) bounds.Encapsulate(mesh.bounds);
            return bounds;
        }
    }
}
