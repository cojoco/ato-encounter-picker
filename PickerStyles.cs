using UnityEngine;

namespace EncounterPicker
{
    // Dark, gold-trimmed look for the picker panel, with large click targets. Built lazily inside OnGUI because GUI.skin is only readable there.
    internal static class PickerStyles
    {
        private static readonly Color Cream = new Color(0.93f, 0.87f, 0.72f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.45f);
        private static readonly Color Dim = new Color(0.72f, 0.66f, 0.52f);
        private static readonly Color Edge = new Color(0.42f, 0.33f, 0.18f);

        private static GUISkin skin;

        internal static GUIStyle Title;
        internal static GUIStyle Muted;
        internal static GUIStyle Row;

        internal static GUISkin Skin
        {
            get
            {
                if (skin == null)
                {
                    Build();
                }
                return skin;
            }
        }

        // 3x3 texture with a one pixel border, sliced by the style's border setting so it scales cleanly.
        private static Texture2D Tex(Color fill, Color border)
        {
            var t = new Texture2D(3, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    t.SetPixel(x, y, x == 1 && y == 1 ? fill : border);
                }
            }
            t.Apply();
            return t;
        }

        private static void States(GUIStyle s, Texture2D normal, Texture2D hover, Texture2D active, Texture2D on, Color text)
        {
            s.normal.background = s.focused.background = normal;
            s.hover.background = hover;
            s.active.background = active;
            s.onNormal.background = s.onFocused.background = on;
            s.onHover.background = hover;
            s.onActive.background = active;
            s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = text;
            s.onNormal.textColor = s.onHover.textColor = s.onActive.textColor = s.onFocused.textColor = Gold;
            s.border = new RectOffset(1, 1, 1, 1);
        }

        private static void Build()
        {
            skin = Object.Instantiate(GUI.skin);
            skin.hideFlags = HideFlags.HideAndDontSave;

            skin.box.normal.background = Tex(new Color(0.07f, 0.06f, 0.08f, 0.97f), new Color(0.6f, 0.46f, 0.22f));
            skin.box.border = new RectOffset(1, 1, 1, 1);

            skin.label.fontSize = 18;
            skin.label.wordWrap = true;
            skin.label.richText = true;
            skin.label.normal.textColor = Cream;

            skin.button.fontSize = 18;
            skin.button.richText = true;
            skin.button.alignment = TextAnchor.MiddleCenter;
            skin.button.padding = new RectOffset(14, 14, 10, 10);
            skin.button.margin = new RectOffset(4, 4, 4, 4);
            States(skin.button, Tex(new Color(0.15f, 0.13f, 0.15f), Edge), Tex(new Color(0.28f, 0.22f, 0.13f), new Color(0.75f, 0.58f, 0.28f)),
                Tex(new Color(0.38f, 0.28f, 0.14f), Gold), Tex(new Color(0.34f, 0.25f, 0.12f), new Color(0.75f, 0.58f, 0.28f)), Cream);

            Texture2D field = Tex(new Color(0.04f, 0.04f, 0.05f), Edge);
            skin.textField.fontSize = 18;
            skin.textField.padding = new RectOffset(10, 10, 10, 10);
            skin.textField.margin = new RectOffset(4, 4, 4, 4);
            skin.textField.border = new RectOffset(1, 1, 1, 1);
            skin.textField.normal.background = skin.textField.hover.background = field;
            skin.textField.focused.background = Tex(new Color(0.04f, 0.04f, 0.05f), new Color(0.75f, 0.58f, 0.28f));
            skin.textField.normal.textColor = skin.textField.hover.textColor = skin.textField.focused.textColor = Cream;

            skin.verticalScrollbar.fixedWidth = 14f;
            skin.verticalScrollbar.normal.background = Tex(new Color(0.04f, 0.04f, 0.05f), new Color(0.04f, 0.04f, 0.05f));
            skin.verticalScrollbar.border = new RectOffset(1, 1, 1, 1);
            skin.verticalScrollbarThumb.fixedWidth = 14f;
            skin.verticalScrollbarThumb.normal.background = Tex(new Color(0.45f, 0.35f, 0.18f), new Color(0.6f, 0.46f, 0.22f));
            skin.verticalScrollbarThumb.border = new RectOffset(1, 1, 1, 1);
            skin.verticalScrollbarUpButton.fixedWidth = skin.verticalScrollbarUpButton.fixedHeight = 0f;
            skin.verticalScrollbarDownButton.fixedWidth = skin.verticalScrollbarDownButton.fixedHeight = 0f;

            Title = new GUIStyle(skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            Title.normal.textColor = Gold;

            Muted = new GUIStyle(skin.label) { fontSize = 15 };
            Muted.normal.textColor = Dim;

            Row = new GUIStyle(skin.button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(18, 18, 12, 12) };
        }
    }
}
