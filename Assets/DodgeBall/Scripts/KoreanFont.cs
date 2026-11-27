using UnityEngine;
using UnityEngine.UI;

namespace DodgeBall
{
    /// <summary>
    /// The built-in UI font has no Hangul glyphs. Put this on a Canvas to switch all its
    /// Text components to an installed Korean OS font at runtime.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class KoreanFont : MonoBehaviour
    {
        [SerializeField] string[] fontNames = { "Malgun Gothic", "맑은 고딕", "AppleSDGothicNeo", "Noto Sans CJK KR" };

        void Awake()
        {
            Font font = Font.CreateDynamicFontFromOSFont(fontNames, 32);
            if (font == null) return;
            foreach (var text in GetComponentsInChildren<Text>(true)) text.font = font;
        }
    }
}
