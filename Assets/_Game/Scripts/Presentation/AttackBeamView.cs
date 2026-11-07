using UnityEngine;
using UnityEngine.Rendering;

namespace Veilward.Presentation
{
    public sealed class AttackBeamView : MonoBehaviour
    {
        private const float LifetimeSeconds = 0.12f;
        private float _remainingSeconds = LifetimeSeconds;

        public void Initialize(Material material, Vector3 start, Vector3 end)
        {
            var line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = 0.08f;
            line.endWidth = 0.04f;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        private void Update()
        {
            _remainingSeconds -= Time.deltaTime;
            if (_remainingSeconds <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}
