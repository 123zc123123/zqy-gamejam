using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 摇杆外观。图空着时用原型 CSS 配色占位；正式图拖进来即可替换，不必改代码。
    /// 资源规格见 docs/资源替换.md。
    /// </summary>
    [CreateAssetMenu(menuName = "DouQuqu/Stick Theme", fileName = "DouQuquStickTheme")]
    public sealed class DouQuquStickTheme : ScriptableObject
    {
        public const float PrototypePhoneWidth = 390f;
        public const float PrototypeSummonedSize = 128f;
        public const float PrototypeFixedSize = 220f;
        public const float PrototypeSummonedKnob = 52f;
        public const float PrototypeFixedKnob = 64f;
        public const float PrototypeSummonedTravel = 38f;
        public const float PrototypeFixedTravel = 78f;

        [Header("正式图（可空 = 继续用颜色占位）")]
        [Tooltip("底盘。正方形，透明底。空则用原型径向渐变占位。")]
        public Sprite baseSprite;
        [Tooltip("圆心点，滑长档才显示。空则用米色小圆占位。")]
        public Sprite centerSprite;
        [Tooltip("摇杆头，跟手移动。空则用原型陶色圆头占位。")]
        public Sprite knobSprite;

        [Header("占位色（有正式图时忽略）")]
        public Color baseFill = new Color(0.055f, 0.063f, 0.055f, 0.58f);
        public Color baseRim = new Color(0.769f, 0.647f, 0.455f, 0.40f);
        public Color knobFill = new Color(0.541f, 0.290f, 0.227f, 0.95f);
        public Color centerFill = new Color(0.937f, 0.902f, 0.824f, 1f);

        [Header("原型尺寸（相对 390 宽竖屏）")]
        public float summonedSize = PrototypeSummonedSize;
        public float fixedSize = PrototypeFixedSize;
        public float summonedKnob = PrototypeSummonedKnob;
        public float fixedKnob = PrototypeFixedKnob;
        public float summonedTravel = PrototypeSummonedTravel;
        public float fixedTravel = PrototypeFixedTravel;

        public bool HasBaseSprite => baseSprite != null;
        public bool HasCenterSprite => centerSprite != null;
        public bool HasKnobSprite => knobSprite != null;

        public float StickSize(bool fixedStick)
        {
            return Mathf.Max(8f, fixedStick ? fixedSize : summonedSize);
        }

        public float KnobSize(bool fixedStick)
        {
            return Mathf.Max(8f, fixedStick ? fixedKnob : summonedKnob);
        }

        public float Travel(bool fixedStick)
        {
            return Mathf.Max(4f, fixedStick ? fixedTravel : summonedTravel);
        }
    }
}
