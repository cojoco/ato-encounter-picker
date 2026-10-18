using System.Collections.Generic;
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
        internal static GUIStyle Slot;

        private const float RowIcon = 48f;
        private const float SlotIcon = 40f;

        private static readonly Dictionary<int, GUIStyle> iconRows = new Dictionary<int, GUIStyle>();
        private static readonly Dictionary<Sprite, KeyValuePair<Texture2D, Rect>> uvs = new Dictionary<Sprite, KeyValuePair<Texture2D, Rect>>();

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
            Slot = new GUIStyle(skin.button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset((int)SlotIcon + 20, 8, 8, 8) };
        }

        // A list row with room on the left for this many portraits, so the text lines up across rows.
        internal static GUIStyle RowWithIcons(int count)
        {
            if (!iconRows.TryGetValue(count, out GUIStyle style))
            {
                style = new GUIStyle(Row) { padding = new RectOffset(12 + count * ((int)RowIcon + 4) + 8, 18, 12, 12) };
                iconRows[count] = style;
            }
            return style;
        }

        // Draws each enemy's portrait over the last control, left-aligned. Call right after the button, repaint only.
        internal static void DrawRowIcons(NPCData[] npcs, int count)
        {
            Rect row = GUILayoutUtility.GetLastRect();
            for (int i = 0; i < count && npcs != null && i < npcs.Length; i++)
            {
                DrawNpc(new Rect(row.x + 12f + i * (RowIcon + 4f), row.y + (row.height - RowIcon) / 2f, RowIcon, RowIcon), npcs[i]);
            }
        }

        internal static void DrawSlotIcon(NPCData npc)
        {
            Rect row = GUILayoutUtility.GetLastRect();
            DrawNpc(new Rect(row.x + 10f, row.y + (row.height - SlotIcon) / 2f, SlotIcon, SlotIcon), npc);
        }

        // The small turn-order portrait the combat screen uses, falling back to the larger ones, fitted into the box without stretching.
        private static void DrawNpc(Rect box, NPCData npc)
        {
            if (npc == null)
            {
                return;
            }
            Sprite sprite = npc.SpriteSpeed != null ? npc.SpriteSpeed : npc.SpritePortrait != null ? npc.SpritePortrait : npc.Sprite;
            if (sprite == null)
            {
                return;
            }
            if (!uvs.TryGetValue(sprite, out KeyValuePair<Texture2D, Rect> entry))
            {
                Texture2D tex = sprite.texture;
                Rect uv = new Rect(0f, 0f, 1f, 1f);
                if (tex != null)
                {
                    try
                    {
                        Rect r = sprite.textureRect;
                        uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
                    }
                    catch (System.Exception)
                    {
                        // Tight-packed sprites refuse textureRect; the whole texture is the best that can be shown.
                    }
                }
                entry = new KeyValuePair<Texture2D, Rect>(tex, uv);
                uvs[sprite] = entry;
            }
            if (entry.Key == null)
            {
                return;
            }
            float aspect = entry.Value.width * entry.Key.width / System.Math.Max(1f, entry.Value.height * entry.Key.height);
            float w = aspect >= 1f ? box.width : box.height * aspect;
            float h = aspect >= 1f ? box.width / aspect : box.height;
            GUI.DrawTextureWithTexCoords(new Rect(box.x + (box.width - w) / 2f, box.y + (box.height - h) / 2f, w, h), entry.Key, entry.Value);
        }
    }
}
