using System;
using UnityEngine;

namespace Veilward.Presentation
{
    public sealed class EnemyHealthBarView : MonoBehaviour
    {
        private const float Width = 0.9f;

        [SerializeField] private Transform barRoot;
        [SerializeField] private Transform fill;

        public void SetNormalizedHealth(float normalizedHealth)
        {
            if (fill == null)
            {
                throw new InvalidOperationException("Enemy health bar fill reference is required.");
            }

            var ratio = Mathf.Clamp01(normalizedHealth);
            fill.localScale = new Vector3(Width * ratio, 0.1f, 1f);
            fill.localPosition = new Vector3(-Width * (1f - ratio) * 0.5f, 0f, -0.01f);
        }

        public void Face(Camera camera)
        {
            if (barRoot == null)
            {
                throw new InvalidOperationException("Enemy health bar root reference is required.");
            }

            if (camera != null)
            {
                barRoot.rotation = camera.transform.rotation;
            }
        }
    }
}
