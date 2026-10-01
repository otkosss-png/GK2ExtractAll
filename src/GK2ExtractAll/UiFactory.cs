using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2ExtractAll
{
    internal static class UiFactory
    {
        internal static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            // Игровые окна содержат layout-группы: без ignoreLayout они пересчитывают
            // наши кнопки/подписи (размер и позицию) и кнопки наезжают друг на друга.
            var le = go.AddComponent<UnityEngine.UI.LayoutElement>();
            le.ignoreLayout = true;
            return (RectTransform)go.transform;
        }

        internal static Image PanelImage(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            }
            return img;
        }

        internal static TextMeshProUGUI Label(string name, Transform parent, string text, int size,
            TextAlignmentOptions align, Color? color = null)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (GameStyle.Font != null) t.font = GameStyle.Font;
            if (GameStyle.FontMaterial != null) t.fontSharedMaterial = GameStyle.FontMaterial;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? GameStyle.Text;
            t.raycastTarget = false;
            FontFitter.Attach(t); // перевод может требовать другой шрифт (CJK)
            return t;
        }

        // Ищет «эталонную» игровую подпись в том же окне: живой TMP-текст с
        // назначенным шрифтом. У неё берём согласованную пару font+material —
        // глобальный GameStyle.FontMaterial мог указывать на материал чужого
        // шрифта (после установки сторонних модов) и текст не рисовался.
        internal static TMP_Text FindReferenceLabel(Transform root)
        {
            if (root == null) return null;
            TMP_Text fallback = null;
            try
            {
                var all = root.GetComponentsInChildren<TMP_Text>(true);
                foreach (var t in all)
                {
                    if (t == null || t.font == null || IsOwnObject(t.transform)) continue;
                    if (string.IsNullOrEmpty(t.text))
                    {
                        if (fallback == null) fallback = t;
                        continue;
                    }
                    return t;
                }
            }
            catch { }
            return fallback;
        }

        private static bool IsOwnObject(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (p.name == "ExtractAllBtn" || p.name == "ResultLabel") return true;
            }
            return false;
        }

        // Применяет к нашей подписи пару font+material эталонной игровой подписи.
        // Если эталона нет — берём GameStyle.Font и его собственный материал
        // (чужой материал не копируем).
        internal static void ApplyReferenceFont(TextMeshProUGUI target, TMP_Text reference)
        {
            if (target == null) return;
            try
            {
                var font = reference != null && reference.font != null ? reference.font : GameStyle.Font;
                if (font == null) return;
                target.font = font;
                var mat = reference != null ? reference.fontSharedMaterial : null;
                if (mat == null) mat = font.material;
                if (mat != null) target.fontSharedMaterial = mat;
                FontFitter.Fit(target); // в шрифте эталона может не быть символов перевода
            }
            catch { }
        }

        // Применяет игровой спрайт кнопки к уже созданной кнопке (для Close/Center).
        internal static void ApplyButtonSprite(Button btn)
        {
            var img = btn != null ? btn.targetGraphic as Image : null;
            var sprite = GameStyle.ButtonSprite;
            if (img == null || sprite == null) return;
            img.sprite = sprite;
            img.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.color = Color.white;
        }

        internal static Button TextButton(string name, Transform parent, string text, int size)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            var sprite = GameStyle.ButtonSprite;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                img.color = Color.white;
            }
            else
            {
                img.color = GameStyle.ButtonBg;
            }
            img.raycastTarget = true;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var label = Label("Label", rt, text, size, TextAlignmentOptions.Center, new Color(1f, 0.97f, 0.9f));
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(6, 2);
            lrt.offsetMax = new Vector2(-6, -2);
            return btn;
        }
    }
}
