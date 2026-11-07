using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TomorinTrial
{
    public sealed partial class InkMainMenu
    {
        [SerializeField] GameObject wardrobeModel;
        [SerializeField] Button wardrobeButton;
        RectTransform wardrobePanel;
        Button wardrobeFirst;
        readonly List<(Button button, Text caption, TrialOutfit mask, TrialOutfit value)> wardrobeChoices = new();
        GameObject wardrobeStage;
        Transform wardrobeVisual, wardrobeInitialSword, wardrobeMingShuang, wardrobeScabbard;
        TrialRig wardrobeRig;
        RenderTexture wardrobeTexture;
        Material[] wardrobeMaterials;

        void InstallWardrobeEntry()
        {
            if (wardrobeButton) return;
            var settingsRect = (RectTransform)options.transform;
            var quitRect = (RectTransform)quit.transform;
            Vector2 position = quitRect.anchoredPosition;
            Vector2 size = settingsRect.sizeDelta;
            wardrobeButton = Button(home, "装扮", position.x, -position.y, size.x, size.y, "");
            wardrobeButton.transform.SetSiblingIndex(options.transform.GetSiblingIndex() + 1);
            quitRect.anchoredPosition += position - settingsRect.anchoredPosition;
        }

        void OpenWardrobe()
        {
            if (!wardrobePanel) BuildWardrobe();
            Show(wardrobePanel);
            RefreshWardrobe(TrialAppearance.Saved);
            wardrobeFirst.Select();
        }

        void CloseWardrobe()
        {
            Show(home);
            wardrobeButton.Select();
        }

        void BuildWardrobe()
        {
            var card = wardrobePanel = Panel("Wardrobe card", root, 0, 0, 1120, 680, Paper);
            card.GetComponent<Image>().raycastTarget = true;
            Line(card, 0, 0, 3, 680, Red);
            Label(card, "装扮", 36, 23, 420, 56, 34, Ink);
            Label(card, "部件", 620, 98, 455, 32, 20, Grey);
            Line(card, 590, 86, 1, 566, new Color(.25f,.28f,.27f,.2f));
            var preview = Rect("Tomorin preview area", card, new Vector2(36,86), new Vector2(518,566));
            var previewImage = Rect("Live Tomorin preview", preview, Vector2.zero, Vector2.zero);
            previewImage.pivot = new Vector2(.5f,.5f);
            Stretch(previewImage);
            var image = previewImage.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            if (wardrobeModel) BuildWardrobePreview(image);
            else
            {
                image.enabled = false;
                Label(preview, "主角模型不可用", 0, 0, 518, 566, 20, Grey, TextAnchor.MiddleCenter);
            }

            var hat = WardrobeRow(card, "斗笠", 150, TrialOutfit.Hat, ("佩戴", TrialOutfit.Hat), ("不佩戴", TrialOutfit.None));
            var sword = WardrobeRow(card, "剑", 258, TrialOutfit.Swords, ("初始", TrialOutfit.InitialSword), ("鸣霜", TrialOutfit.MingShuang));
            var mask = WardrobeRow(card, "口罩", 366, TrialOutfit.Mask, ("佩戴", TrialOutfit.Mask), ("不佩戴", TrialOutfit.None));
            var done = WardrobeChoice(card, "返回", 900, 576, 156, CloseWardrobe);
            Button[][] rows = {hat, sword, mask, new[] {done}};
            for (int row = 0; row < rows.Length; row++)
            {
                var current = rows[row];
                var above = rows[(row + rows.Length - 1) % rows.Length];
                var below = rows[(row + 1) % rows.Length];
                for (int column = 0; column < current.Length; column++)
                    SetDirectionalNavigation(current[column],
                        up:above[Mathf.Min(column,above.Length-1)], down:below[Mathf.Min(column,below.Length-1)],
                        left:current[(column+current.Length-1)%current.Length], right:current[(column+1)%current.Length]);
            }
            wardrobeFirst = hat[0];
            TrialMenuLayout.StretchContents(card);
            Stretch(card);
        }

        Button[] WardrobeRow(RectTransform card, string part, int y, TrialOutfit mask,
            params (string label, TrialOutfit value)[] choices)
        {
            Label(card, part, 620, y, 450, 30, 22, Ink);
            var buttons = new Button[choices.Length];
            for (int i = 0; i < choices.Length; i++)
            {
                var choice = choices[i];
                var button = WardrobeChoice(card, choice.label, 620+i*150, y+38, 136, () =>
                {
                    var outfit = (TrialAppearance.Saved & ~mask) | choice.value;
                    TrialAppearance.Saved = outfit;
                    RefreshWardrobe(outfit);
                });
                buttons[i] = button;
                wardrobeChoices.Add((button, button.GetComponentInChildren<Text>(), mask, choice.value));
            }
            return buttons;
        }

        Button WardrobeChoice(RectTransform card, string caption, int x, int y, int width, UnityEngine.Events.UnityAction click)
        {
            var rect = Panel(caption, card, x, y, width, 44, new Color(.88f,.86f,.81f));
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(.87f,.83f,.78f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.75f,.70f,.63f);
            button.colors = colors;
            Label(rect, caption, 0, 0, width, 44, 19, Ink, TextAnchor.MiddleCenter);
            button.onClick.AddListener(click);
            return button;
        }

        void RefreshWardrobe(TrialOutfit outfit)
        {
            foreach (var choice in wardrobeChoices)
            {
                bool selected = (outfit & choice.mask) == choice.value;
                choice.button.image.color = selected ? Red : new Color(.88f,.86f,.81f);
                choice.caption.color = selected ? Paper : Ink;
            }
            if (!wardrobeVisual) return;

            TrialAppearance.ApplyAccessories(wardrobeVisual, outfit);
            var sword = outfit & TrialOutfit.Swords;
            wardrobeInitialSword.gameObject.SetActive(sword == TrialOutfit.InitialSword);
            wardrobeMingShuang.gameObject.SetActive(sword == TrialOutfit.MingShuang);
            if (wardrobeScabbard) wardrobeScabbard.gameObject.SetActive(sword == TrialOutfit.MingShuang);
            wardrobeRig.SetWeapon(sword == TrialOutfit.MingShuang ? wardrobeMingShuang : wardrobeInitialSword);
            wardrobeRig.SetScabbard(sword == TrialOutfit.InitialSword ? null : wardrobeScabbard);
            wardrobeRig.PreviewSwordLocomotion(0, 0, 0);
        }

        void BuildWardrobePreview(RawImage image)
        {
            // Keep preview geometry outside the menu camera's far plane.
            wardrobeStage = new GameObject("Wardrobe preview stage");
            wardrobeStage.transform.SetParent(transform,false);
            wardrobeStage.transform.position = new Vector3(0, 0, -1200);
            var actor = wardrobeStage.transform;
            wardrobeVisual = Instantiate(wardrobeModel, actor, false).transform;
            foreach (var animator in wardrobeVisual.GetComponentsInChildren<Animator>()) animator.enabled = false;
            foreach (var collider in wardrobeVisual.GetComponentsInChildren<Collider>()) collider.enabled = false;
            var skins = wardrobeVisual.GetComponentsInChildren<SkinnedMeshRenderer>();
            TrialAppearance.FitModel(wardrobeVisual, skins.Length > 0 ? skins : wardrobeVisual.GetComponentsInChildren<Renderer>());
            wardrobeRig = wardrobeStage.AddComponent<TrialRig>();
            wardrobeRig.Initialize(null, wardrobeVisual);
            var hand = wardrobeRig.RightHand ? wardrobeRig.RightHand : wardrobeVisual;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            wardrobeMaterials = new[]
            {
                PreviewMaterial(shader, new Color(.58f,.66f,.73f)),
                PreviewMaterial(shader, new Color(.52f,.32f,.12f)),
                PreviewMaterial(shader, new Color(.92f,.75f,.42f))
            };
            wardrobeInitialSword = TrialWorld.Sword(hand, wardrobeMaterials[0], wardrobeMaterials[1], wardrobeMaterials[2], false, TrialOutfit.InitialSword);
            wardrobeMingShuang = TrialWorld.Sword(hand, wardrobeMaterials[0], wardrobeMaterials[1], wardrobeMaterials[2], false);
            wardrobeScabbard = TrialWorld.SwordScabbard(actor);

            var light = new GameObject("Wardrobe light").AddComponent<Light>();
            light.transform.SetParent(actor, false);
            light.transform.localRotation = Quaternion.Euler(33, 202, 0);
            light.type = LightType.Directional;
            light.intensity = 1.45f;
            light.shadows = LightShadows.None;

            wardrobeTexture = new RenderTexture(840, 900, 24) { name = "Wardrobe preview", antiAliasing = 2 };
            var camera = new GameObject("Wardrobe camera").AddComponent<Camera>();
            camera.transform.SetParent(actor, false);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.22f,.28f,.30f);
            camera.orthographic = true;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 12;
            camera.allowHDR = false;
            camera.targetTexture = wardrobeTexture;
            image.texture = wardrobeTexture;
            image.raycastTarget = true;
            image.gameObject.AddComponent<InkPreviewOrbit>().Configure(camera, actor);
            var fit = image.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = (float)wardrobeTexture.width / wardrobeTexture.height;
        }

        static Material PreviewMaterial(Shader shader, Color color)
        {
            var material = new Material(shader);
            material.SetColor("_BaseColor",color);
            return material;
        }

        void ReleaseWardrobePreview()
        {
            if (wardrobeTexture) Destroy(wardrobeTexture);
            if (wardrobeMaterials != null)
                foreach (var material in wardrobeMaterials) Destroy(material);
        }
    }
}
