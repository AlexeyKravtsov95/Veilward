using UnityEngine;
using UnityEngine.UI;

namespace Veilward.Presentation
{
    public sealed class VeilwardUiButton : Button
    {
        [SerializeField] private RectTransform face;
        [SerializeField] private Graphic stateGraphic;
        private Vector2 _restingPosition;

        private bool _positionCaptured;

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (stateGraphic != null)
            {
                var tint = state switch
                {
                    SelectionState.Highlighted => colors.highlightedColor,
                    SelectionState.Pressed => colors.pressedColor,
                    SelectionState.Selected => colors.selectedColor,
                    SelectionState.Disabled => colors.disabledColor,
                    _ => colors.normalColor
                };
                stateGraphic.CrossFadeColor(tint * colors.colorMultiplier, instant ? 0 : colors.fadeDuration, true, true);
            }
            if (!UnityEngine.Application.isPlaying || !isActiveAndEnabled || face == null) return;
            if (!_positionCaptured)
            {
                _restingPosition = face.anchoredPosition;
                _positionCaptured = true;
            }
            face.anchoredPosition = _restingPosition + (state == SelectionState.Pressed ? Vector2.down * 2 : Vector2.zero);
        }
    }
}
