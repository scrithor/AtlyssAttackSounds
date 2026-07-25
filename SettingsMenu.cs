using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace AtlyssAttackSounds
{
    #region Settings UI

    public static class SettingsUI
    {
        private static GameObject canvasObject;
        private static GameObject panelObject;
        private static bool isVisible = false;
        private static bool initialized = false;

        #region Initialization

        public static void Initialize(
            ConfigEntry<float> volumeFast, ConfigEntry<float> volumeMedium, ConfigEntry<float> volumeSlow,
            ConfigEntry<float> chanceFast, ConfigEntry<float> chanceMedium, ConfigEntry<float> chanceSlow,
            ConfigEntry<float> jiggleIntensity, ConfigEntry<float> particleSize)
        {
            if (initialized) return;

            CreateCanvas();
            CreateBackgroundOverlay();
            CreateMainPanel(volumeFast, volumeMedium, volumeSlow, chanceFast, chanceMedium, chanceSlow, jiggleIntensity, particleSize);

            canvasObject.SetActive(false);
            isVisible = false;
            initialized = true;

            AtlyssAttackSoundsMod.logger.LogInfo("[SettingsUI] Custom settings menu created. Press F7 to toggle.");
        }

        private static void CreateCanvas()
        {
            canvasObject = new GameObject("AttackSoundsSettingsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            GameObject.DontDestroyOnLoad(canvasObject);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void CreateBackgroundOverlay()
        {
            GameObject bgOverlay = CreateUIObject("BackgroundOverlay", canvasObject.transform);
            RectTransform bgRect = bgOverlay.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            Image bgImage = bgOverlay.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.65f);

            Button bgButton = bgOverlay.AddComponent<Button>();
            bgButton.onClick.AddListener(() => SetVisible(false));
            bgButton.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        private static void CreateMainPanel(
            ConfigEntry<float> volumeFast, ConfigEntry<float> volumeMedium, ConfigEntry<float> volumeSlow,
            ConfigEntry<float> chanceFast, ConfigEntry<float> chanceMedium, ConfigEntry<float> chanceSlow,
            ConfigEntry<float> jiggleIntensity, ConfigEntry<float> particleSize)
        {
            panelObject = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panelObject.transform.SetParent(canvasObject.transform, false);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(540, 0);
            panelRect.anchoredPosition = Vector2.zero;

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.97f);
            panelImage.type = Image.Type.Sliced;

            VerticalLayoutGroup vlg = panelObject.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(22, 22, 16, 16);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            CreateTitle("\u2694 Attack Sounds Settings");
            CreateSeparator();
            CreateSectionHeader("Audio Volumes");
            CreateSliderRow("Volume - Fast", volumeFast, "%", "F2", false);
            CreateSliderRow("Volume - Medium", volumeMedium, "%", "F2", false);
            CreateSliderRow("Volume - Slow", volumeSlow, "%", "F2", false);
            CreateSeparator();
            CreateSectionHeader("Proc Chances (Weights)");
            CreateSliderRow("Chance - Fast", chanceFast, "", "F0", false);
            CreateSliderRow("Chance - Medium", chanceMedium, "", "F0", false);
            CreateSliderRow("Chance - Slow", chanceSlow, "", "F0", false);
            CreateSeparator();
            CreateSectionHeader("Visual & Physical Effects");
            CreateSliderRow("Jiggle Intensity", jiggleIntensity, "", "F2", false);
            CreateSliderRow("Particle Size", particleSize, "", "F2", false);
            CreateSeparator();
            CreateCloseButton();
        }

        #endregion

        #region Visibility Control

        public static void SetVisible(bool visible)
        {
            isVisible = visible;
            if (canvasObject != null)
                canvasObject.SetActive(visible);
        }

        public static void ToggleVisible()
        {
            SetVisible(!isVisible);
        }

        public static bool IsVisible => isVisible;

        #endregion

        #region UI Helpers

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static void CreateTitle(string text)
        {
            GameObject go = CreateUIObject("Title", panelObject.transform);
            Text txt = go.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = 22;
            txt.fontStyle = FontStyle.Bold;
            txt.color = new Color(1f, 0.85f, 0.15f);
            txt.alignment = TextAnchor.MiddleCenter;
            AssignFont(txt);

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = 38;
            le.flexibleWidth = 1;
        }

        private static void CreateSeparator()
        {
            GameObject go = CreateUIObject("Separator", panelObject.transform);
            Image img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.12f);

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = 2;
            le.flexibleWidth = 1;
        }

        private static void CreateSectionHeader(string text)
        {
            GameObject go = CreateUIObject("Header", panelObject.transform);
            Text txt = go.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = 16;
            txt.fontStyle = FontStyle.Bold;
            txt.color = new Color(0.55f, 0.8f, 1f);
            txt.alignment = TextAnchor.MiddleLeft;
            AssignFont(txt);

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = 26;
            le.flexibleWidth = 1;
        }

        private static void CreateSliderRow(
            string label,
            ConfigEntry<float> configEntry,
            string displaySuffix,
            string format,
            bool wholeNumbers)
        {
            GameObject row = CreateUIObject("Row_" + configEntry.Definition.Key, panelObject.transform);
            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;

            LayoutElement rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = 32;
            rowLe.flexibleWidth = 1;

            GameObject labelObj = CreateUIObject("Label", row.transform);
            Text labelText = labelObj.AddComponent<Text>();
            float val = configEntry.Value;
            labelText.text = $"{label}:  {val.ToString(format)}{displaySuffix}";
            labelText.fontSize = 14;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleLeft;
            AssignFont(labelText);

            LayoutElement labelLe = labelObj.AddComponent<LayoutElement>();
            labelLe.minWidth = 200;
            labelLe.flexibleWidth = 0.55f;

            GameObject sliderObj = CreateUIObject("Slider", row.transform);
            Slider slider = sliderObj.AddComponent<Slider>();

            float min = 0f;
            float max = 1f;
            if (configEntry.Description.AcceptableValues is AcceptableValueRange<float> range)
            {
                min = range.MinValue;
                max = range.MaxValue;
            }

            slider.minValue = min;
            slider.maxValue = max;
            slider.value = configEntry.Value;
            slider.wholeNumbers = wholeNumbers;

            GameObject bg = CreateUIObject("Background", sliderObj.transform);
            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.22f, 0.22f, 0.22f, 1f);
            RectTransform bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            GameObject fillArea = CreateUIObject("Fill Area", sliderObj.transform);
            RectTransform faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0f);
            faRt.anchorMax = new Vector2(1f, 1f);
            faRt.sizeDelta = new Vector2(-8f, -4f);
            faRt.anchoredPosition = Vector2.zero;

            GameObject fill = CreateUIObject("Fill", fillArea.transform);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.3f, 0.65f, 1f, 1f);
            RectTransform fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.sizeDelta = Vector2.zero;

            GameObject handleArea = CreateUIObject("Handle Slide Area", sliderObj.transform);
            RectTransform haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = new Vector2(0f, 0f);
            haRt.anchorMax = new Vector2(1f, 1f);
            haRt.sizeDelta = new Vector2(-8f, 0f);
            haRt.anchoredPosition = Vector2.zero;

            GameObject handle = CreateUIObject("Handle", handleArea.transform);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;
            RectTransform handleRt = handle.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(14f, 14f);

            slider.targetGraphic = handleImg;
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };

            LayoutElement sliderLe = sliderObj.AddComponent<LayoutElement>();
            sliderLe.flexibleWidth = 0.45f;
            sliderLe.minHeight = 26;

            Text capturedLabel = labelText;
            slider.onValueChanged.AddListener((newVal) =>
            {
                configEntry.Value = newVal;
                capturedLabel.text = $"{label}:  {newVal.ToString(format)}{displaySuffix}";
            });
        }

        private static void CreateCloseButton()
        {
            GameObject btnObj = CreateUIObject("CloseButton", panelObject.transform);
            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.45f, 0.15f, 0.15f, 1f);
            btnImg.type = Image.Type.Sliced;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(() => SetVisible(false));
            btn.navigation = new Navigation { mode = Navigation.Mode.None };

            GameObject txtObj = CreateUIObject("Text", btnObj.transform);
            Text btnTxt = txtObj.AddComponent<Text>();
            btnTxt.text = "Close  [F7]";
            btnTxt.fontSize = 15;
            btnTxt.fontStyle = FontStyle.Bold;
            btnTxt.color = Color.white;
            btnTxt.alignment = TextAnchor.MiddleCenter;
            AssignFont(btnTxt);

            RectTransform txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.minHeight = 36;
            le.flexibleWidth = 1;
        }

        private static void AssignFont(Text text)
        {
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            }
            if (font != null)
            {
                text.font = font;
            }
        }

        #endregion

        #region Cleanup

        public static void Destroy()
        {
            if (canvasObject != null)
            {
                GameObject.Destroy(canvasObject);
                canvasObject = null;
                panelObject = null;
                initialized = false;
                isVisible = false;
            }
        }

        #endregion
    }

    #endregion
}