using UnityEngine;

namespace Veilward.Presentation
{
    public sealed class InkFlameView : MonoBehaviour
    {
        private const float RecoilDuration = .24f;

        [SerializeField] private Transform billboard;
        [SerializeField] private Transform fox;
        [SerializeField] private Transform muzzle;
        private Vector3 _perch;
        private float _idle;
        private float _recoil;
        private float _facing = 1f;

        public Transform Muzzle => muzzle;

        internal void Face(Camera camera)
        {
            _perch = fox.localPosition;
            billboard.rotation = camera.transform.rotation;
        }

        internal void Aim(Vector3 target)
        {
            var side = Vector3.Dot(target - transform.position, billboard.right);
            if (Mathf.Abs(side) < .05f) return;
            _facing = Mathf.Sign(side);
            fox.localScale = new Vector3(_facing, 1f, 1f);
        }

        internal void Recoil() => _recoil = RecoilDuration;

        internal void Advance(float delta)
        {
            _idle = Mathf.Repeat(_idle + delta, 2.4f);
            _recoil = Mathf.Max(0f, _recoil - delta);
            var breath = Mathf.Sin(_idle * (2f * Mathf.PI / 2.4f));
            var kick = Mathf.Sin(_recoil / RecoilDuration * Mathf.PI);
            fox.localPosition = _perch + new Vector3(-_facing * .09f * kick, .024f * breath + .065f * kick, 0f);
            fox.localRotation = Quaternion.Euler(0f, 0f, _facing * (2.2f * breath + 13f * kick));
        }
    }
}
