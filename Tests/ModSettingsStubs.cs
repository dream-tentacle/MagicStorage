// Settings persistence and GUI remain native engine boundaries.
#if !TRADING_STUB_RECT
namespace UnityEngine { public struct Rect { } }
#endif
namespace Verse
{
    public abstract class ModSettings : IExposable { public virtual void ExposeData() { } }
    public class Listing_Standard
    {
        public void Begin(UnityEngine.Rect rect) { }
        public void End() { }
        public void TextFieldNumericLabeled<T>(string label, ref T value, ref string buffer, float min, float max) where T : struct { }
        public void Label(string label) { }
        public bool ButtonText(string label) => false;
    }
}
