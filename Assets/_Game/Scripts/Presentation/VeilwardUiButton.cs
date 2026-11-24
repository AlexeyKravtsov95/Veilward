using UnityEngine;
using UnityEngine.UI;

namespace Veilward.Presentation
{
    public sealed class VeilwardUiButton : Button
    {
        [SerializeField] private RectTransform face;
        private Vector2 _restingPosition;

        protected override void Awake()
        {
            base.Awake();
            if (face != null) _restingPosition = face.anchoredPosition;
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (face != null)
                face.anchoredPosition = _restingPosition + (state == SelectionState.Pressed ? Vector2.down * 2 : Vector2.zero);
        }
    }
}
