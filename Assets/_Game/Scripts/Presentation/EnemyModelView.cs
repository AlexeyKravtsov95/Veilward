using UnityEngine;

namespace Veilward.Presentation
{
    // Только authored child: UI/root/Domain не получают animation/root motion.
    public sealed class EnemyModelView : MonoBehaviour
    {
        public const float TailDuration = .6f;
        [SerializeField] private Transform model;
        [SerializeField] private AnimationClip idle;
        [SerializeField] private AnimationClip move;
        [SerializeField] private AnimationClip hit;
        [SerializeField] private AnimationClip death;
        [SerializeField] private AnimationClip leak;
        private float _clock;
        private float _hitRemaining;
        private float _tailClock;
        private bool _moving;
        private bool _tail;
        private bool _dead;
        public Transform Model => model;
        public bool IsTail => _tail;
        public float Clock => _clock;
        public float TailClock => _tailClock;
        public string Pose => _tail ? (_dead ? "Death" : "Leak") : _hitRemaining > 0 ? "Hit" : _moving ? "Move" : "Idle";

        public void Synchronize(Vector3 tangent, bool moving)
        {
            if (_tail) return;
            _moving = moving;
            if (tangent.sqrMagnitude > .000001f) transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            Sample();
        }

        public void Hit()
        {
            if (_tail) return;
            // Повторные hits обновляют один response, не создают objects/material instances.
            _hitRemaining = hit != null ? hit.length : .4f;
            Sample();
        }

        public void BeginTail(bool dead, Transform owner)
        {
            if (_tail) return;
            _tail = true; _dead = dead; _tailClock = 0; _hitRemaining = 0;
            transform.SetParent(owner, true);
            Sample();
        }

        public bool Advance(float activeDelta)
        {
            if (activeDelta < 0) return false;
            _clock += activeDelta;
            _hitRemaining = Mathf.Max(0, _hitRemaining - activeDelta);
            if (_tail) _tailClock += activeDelta;
            Sample();
            return _tail && _tailClock >= TailDuration;
        }

        private void Sample()
        {
            if (model == null) return;
            // Полный idle pose reset исключает остаток scale/rotation от Death/Hit.
            if (idle != null) idle.SampleAnimation(model.gameObject, 0);
            var clip = _tail ? (_dead ? death : leak) : _hitRemaining > 0 ? hit : _moving ? move : idle;
            if (clip == null) return;
            var time = _tail ? Mathf.Min(_tailClock, clip.length) : _hitRemaining > 0 ? clip.length - _hitRemaining : Mathf.Repeat(_clock, clip.length);
            clip.SampleAnimation(model.gameObject, Mathf.Max(0, time));
        }
    }
}
