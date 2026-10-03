using System;
using UnityEngine;
using UnityEngine.UI;
using Nekki.Vector.Core;
using Nekki.Vector.Core.Game;
using Nekki.Vector.Core.Localization;
using Nekki.Vector.GUI;
using Nekki.Vector.GUI.Common;

namespace Nekki.Vector.GUI.Dialogs
{
    public sealed class GraphicsOptionsPanel : MonoBehaviour
    {
        private Font _Font;
        private GameObject _GameView;
        private GameObject _MenuView;
        private ButtonUI _ButtonTemplate;
        private ButtonUI _OpenButton;

        private static readonly int[] FpsCaps = { 0, 60, 100, 120, 144, 165, 240, -1 };
        private static readonly string[] FpsLabels = { "DISPLAY", "60", "100", "120", "144", "165", "240", "UNLIMITED" };
        private static readonly int[] AaValues = { 0, 2, 4, 8 };
        private static readonly string[] AaLabels = { "OFF", "2X", "4X", "8X" };
        private static readonly string[] TextureLabels = { "FULL", "HALF", "QUARTER", "EIGHTH" };
        private static readonly string[] AnisoLabels = { "OFF", "PER TEXTURE", "FORCED ON" };

        public static void Create(GameObject gameView, Font font)
        {
            if (gameView == null) return;
            Transform parent = gameView.transform.parent;
            if (parent.Find("GraphicsOptionsView") != null || FindButtonUI(parent, "GraphicsMenuButton") != null) return;

            ButtonUI template = gameView.transform.Find("BuyOST")?.GetComponent<ButtonUI>();
            if (template == null) return;
            GameObject menu = new GameObject("GraphicsOptionsView", typeof(RectTransform), typeof(Image));
            menu.transform.SetParent(parent, false);
            CopyRect(gameView.GetComponent<RectTransform>(), menu.GetComponent<RectTransform>());

            Image sourceImage = gameView.GetComponent<Image>();
            menu.GetComponent<Image>().color = sourceImage != null
                ? new Color(sourceImage.color.r, sourceImage.color.g, sourceImage.color.b, 0.97f)
                : new Color(0.02f, 0.035f, 0.05f, 0.97f);

            GraphicsOptionsPanel panel = menu.AddComponent<GraphicsOptionsPanel>();
            panel._Font = font;
            panel._GameView = gameView;
            panel._MenuView = menu;
            panel._ButtonTemplate = template;
            panel.Build();

            ButtonUI rowButton = FindButtonUI(parent, "GameOptions");
            Transform openParent = rowButton != null ? rowButton.transform.parent : gameView.transform;
            ButtonUI open = panel.CloneNativeButton(openParent, "GraphicsMenuButton", "GRAPHICS", ButtonUI.Type.Blue);
            RectTransform openRect = open.GetComponent<RectTransform>();
            if (rowButton != null)
            {
                SetTopAnchored(openRect, new Vector2(0f, -100f), new Vector2(510f, 66f));
            }
            else
            {
                SetTopAnchored(openRect, new Vector2(655f, -912f), new Vector2(510f, 66f));
            }
            panel._OpenButton = open;
            open.Button.onClick.AddListener(panel.Open);

            menu.SetActive(false);
        }
        private void Build()
        {
            MakeLabel("GraphicsTitle", "GRAPHICS SETTINGS", 48, new Vector2(0f, -86f), new Vector2(1000f, 72f), TextAnchor.MiddleCenter);
            Text subtitle = MakeLabel("GraphicsSubtitle", "HIGH-REFRESH PRESENTATION  •  60 HZ GAMEPLAY", 22,
                new Vector2(0f, -145f), new Vector2(1100f, 42f), TextAnchor.MiddleCenter);
            subtitle.color = new Color(0.63f, 0.75f, 0.81f, 1f);

            Text hint = MakeLabel("GraphicsHint", "CLICK A VALUE TO CHANGE IT", 18,
                new Vector2(0f, -188f), new Vector2(900f, 32f), TextAnchor.MiddleCenter);
            hint.color = new Color(0.50f, 0.59f, 0.64f, 1f);

            float y = -270f;
            const float step = 92f;

            CreateChoiceRow("FPS CAP", FpsLabels, IndexOf(FpsCaps, GameTiming.FpsCap), y,
                delegate(int i) { GameTiming.SetFpsCap(FpsCaps[i]); });
            y -= step;

            CreateChoiceRow("ANTI-ALIASING", AaLabels, IndexOf(AaValues, GameTiming.AntiAliasing), y,
                delegate(int i) { GameTiming.SetAntiAliasing(AaValues[i]); });
            y -= step;

            CreateChoiceRow("TEXTURE QUALITY", TextureLabels, Mathf.Clamp(GameTiming.TextureMipmapLimit, 0, 3), y,
                delegate(int i) { GameTiming.SetTextureMipmapLimit(i); });
            y -= step;
            CreateChoiceRow("ANISOTROPIC", AnisoLabels, Mathf.Clamp(GameTiming.AnisotropicMode, 0, 2), y,
                delegate(int i) { GameTiming.SetAnisotropicMode(i); });
            y -= step;

            CreateToggleRow("FPS COUNTER", Settings.Visual.ShowFPS, y, delegate(bool value)
            {
                Settings.Visual.ShowFPS = value;
                Settings.Save();
                if (DebugUI.FPSMeter != null) DebugUI.FPSMeter.UpdateVisiblity();
                if (DebugUI.RunFPSMeter != null) DebugUI.RunFPSMeter.UpdateVisiblity();
            });
            y -= step;

            CreateToggleRow("V-SYNC", GameTiming.VSyncEnabled, y,
                delegate(bool value) { GameTiming.SetVSync(value); });
            y -= step;

            CreateToggleRow("FIXED-STEP SMOOTHING", GameTiming.InterpolationEnabled, y,
                delegate(bool value) { GameTiming.SetInterpolationEnabled(value); });

            ButtonUI back = CloneNativeButton(transform, "BackButton", "BACK", ButtonUI.Type.Green);
            SetTopAnchored(back.GetComponent<RectTransform>(), new Vector2(0f, -935f), new Vector2(510f, 66f));
            back.Button.onClick.AddListener(Back);
        }

        private void CreateChoiceRow(string label, string[] values, int initial, float y, Action<int> changed)
        {
            MakeLabel(label + "Label", label, 28, new Vector2(-330f, y),
                new Vector2(520f, 66f), TextAnchor.MiddleLeft);
            int index = Mathf.Clamp(initial, 0, values.Length - 1);
            ButtonUI valueButton = CloneNativeButton(transform, label + "Value", values[index], ButtonUI.Type.Blue);
            SetTopAnchored(valueButton.GetComponent<RectTransform>(), new Vector2(325f, y), new Vector2(520f, 66f));

            valueButton.Button.onClick.AddListener(delegate
            {
                index = (index + 1) % values.Length;
                SetButtonText(valueButton, values[index]);
                changed(index);
            });
        }

        private void CreateToggleRow(string label, bool initial, float y, Action<bool> changed)
        {
            MakeLabel(label + "Label", label, 28, new Vector2(-330f, y),
                new Vector2(520f, 66f), TextAnchor.MiddleLeft);

            bool state = initial;
            ButtonUI valueButton = CloneNativeButton(transform, label + "Value", state ? "ON" : "OFF",
                state ? ButtonUI.Type.Green : ButtonUI.Type.Grey);
            SetTopAnchored(valueButton.GetComponent<RectTransform>(), new Vector2(325f, y), new Vector2(520f, 66f));

            valueButton.Button.onClick.AddListener(delegate
            {
                state = !state;
                SetButtonText(valueButton, state ? "ON" : "OFF");
                valueButton.SetType(state ? ButtonUI.Type.Green : ButtonUI.Type.Grey);
                changed(state);
            });
        }
        private ButtonUI CloneNativeButton(Transform parent, string name, string text, ButtonUI.Type type)
        {
            GameObject clone = Instantiate(_ButtonTemplate.gameObject, parent, false);
            clone.name = name;
            clone.SetActive(true);

            ButtonUI ui = clone.GetComponent<ButtonUI>();
            ui.Button.onClick.RemoveAllListeners();
            ui.TurnToFree();
            ui.SetType(type);
            SetButtonText(ui, text);

            LayoutElement layout = clone.GetComponent<LayoutElement>();
            if (layout != null) Destroy(layout);

            return ui;
        }

        private static void SetButtonText(ButtonUI button, string value)
        {
            if (button.ButtonText == null) return;
            button.ButtonText.Alias = string.Empty;
            button.ButtonText.Text = value;
            button.ButtonText.fontSize = 28;
        }

        private Text MakeLabel(string name, string value, int size, Vector2 position, Vector2 dimensions, TextAnchor anchor)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(transform, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            SetTopAnchored(rect, position, dimensions);

            Text label = obj.GetComponent<Text>();
            label.font = _Font;
            label.fontSize = size;
            label.color = Color.white;
            label.alignment = anchor;
            label.text = value;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private void Open()
        {
            _GameView.SetActive(false);
            if (_OpenButton != null) _OpenButton.gameObject.SetActive(false);
            _MenuView.SetActive(true);
        }

        private void Back()
        {
            _MenuView.SetActive(false);
            _GameView.SetActive(true);
            if (_OpenButton != null) _OpenButton.gameObject.SetActive(true);
        }

        private static void CopyRect(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.localScale = source.localScale;
        }

        private static void SetTopAnchored(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static ButtonUI FindButtonUI(Transform root, string objectName)
        {
            ButtonUI[] buttons = root.GetComponentsInChildren<ButtonUI>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].gameObject.name == objectName) return buttons[i];
            }
            return null;
        }

        private static int IndexOf(int[] values, int target)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == target) return i;
            }
            return 0;
        }
    }
}
