using UnityEngine;

namespace Veilward.Presentation
{
    public sealed class InkEnemyView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer occluded;
        [SerializeField] private Sprite[] stride;
        [SerializeField] private bool warden;
        private float _facing = 1f;

        internal void Face(Camera camera, Vector3 tangent)
        {
            if (camera == null) return;
            transform.rotation = camera.transform.rotation;
            var side = Vector3.Dot(tangent, camera.transform.right);
            if (Mathf.Abs(side) > .001f) _facing = Mathf.Sign(side);
        }

        internal void Sample(float clock, bool moving, float hit, float tail, bool dead)
        {
            var rate = warden ? 1.7f : 3.5f;
            var stridePhase = Mathf.Repeat(clock * rate, 1f);
            var swing = Mathf.Sin(2f * Mathf.PI * stridePhase);
            var lift = moving ? Mathf.Abs(swing) * (warden ? .055f : .11f) : .023f * Mathf.Sin(clock * 4f);
            var lean = moving ? (_facing * (warden ? 2f : 5f) * swing) : 2f * Mathf.Sin(clock * 3f);
            var offset = moving ? .025f * swing : 0f;
            var opacity = 1f;

            if (tail >= 0f)
            {
                lift = dead ? .18f * Mathf.Sin(Mathf.PI * tail) - .16f * tail : .34f * tail;
                lean = _facing * (dead ? 58f : -22f) * tail;
                offset = _facing * (dead ? -.24f : .21f) * tail;
                opacity = 1f - Mathf.SmoothStep(0f, 1f, tail);
            }
            else if (hit > 0f)
            {
                offset -= _facing * .16f * hit;
                lift += .07f * hit;
                lean += _facing * 17f * hit;
            }

            var frame = moving && tail < 0f ? Mathf.Min((int)(stridePhase * stride.Length), stride.Length - 1) : 0;
            var image = stride[frame];
            if (body.sprite != image) body.sprite = occluded.sprite = image;
            var tint = Color.Lerp(Color.white, new Color(1f, .52f, .36f), hit * .68f);
            tint.a = opacity;
            body.color = occluded.color = tint;
            var pose = body.transform;
            pose.localPosition = new Vector3(offset, lift, 0f);
            pose.localRotation = Quaternion.Euler(0f, 0f, lean);
            pose.localScale = new Vector3(_facing, 1f, 1f);
        }
    }
}
