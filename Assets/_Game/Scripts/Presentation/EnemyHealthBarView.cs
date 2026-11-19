using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Veilward.Presentation
{
    public sealed class EnemyHealthBarView : MonoBehaviour
    {
        private const float Width = 0.9f;
        [SerializeField] private Transform barRoot;
        [SerializeField] private Transform fill;
        private Vector3 _origin;
        private bool _initialized;
        private LineRenderer _leader;
        public float NormalizedHealth { get; private set; }
        public Vector3 AnchorPosition => transform.TransformPoint(_origin);
        public Vector3 BarPosition => barRoot.position;
        public bool LeaderVisible => _leader != null && _leader.enabled;

        public void SetNormalizedHealth(float normalizedHealth)
        {
            if (fill == null) throw new InvalidOperationException("Enemy health bar fill reference is required.");
            var ratio = Mathf.Clamp01(normalizedHealth);
            NormalizedHealth = ratio;
            fill.localScale = new Vector3(Width * ratio, 0.1f, 1f);
            fill.localPosition = new Vector3(-Width * (1f - ratio) * 0.5f, 0f, -0.01f);
        }

        public void Face(Camera camera)
        {
            if (barRoot == null) throw new InvalidOperationException("Enemy health bar root reference is required.");
            if (!_initialized)
            {
                // Сохраняем anchor в системе enemy, независимо от вложенности prefab.
                _origin = transform.InverseTransformPoint(barRoot.position);
                _initialized = true;
                var go = new GameObject("HP anchor link"); go.transform.SetParent(transform, false);
                _leader = go.AddComponent<LineRenderer>();
                _leader.sharedMaterial = barRoot.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                _leader.positionCount = 2; _leader.useWorldSpace = true;
                _leader.startWidth = _leader.endWidth = .018f;
                _leader.shadowCastingMode = ShadowCastingMode.Off; _leader.receiveShadows = false;
            }
            barRoot.position = AnchorPosition;
            if (_leader != null) _leader.enabled = false;
            if (camera != null) barRoot.rotation = camera.transform.rotation;
        }

        public Rect ScreenBounds(Camera camera)
        {
            var center = camera.WorldToScreenPoint(barRoot.position);
            var half = barRoot.TransformVector(new Vector3(Width * .5f, .05f, 0));
            var corner = camera.WorldToScreenPoint(barRoot.position + half);
            var size = (Vector2)(corner - center) * 2;
            return new Rect((Vector2)center - size * .5f, size);
        }

        // Вызывается в стабильном EnemyId order после Face, с reused frame-local buffer.
        public void Layout(Camera camera, List<Rect> occupied)
        {
            if (camera == null || !gameObject.activeInHierarchy) return;
            var rect = ScreenBounds(camera);
            var padding = 3f * camera.pixelHeight / 900f;
            var liftTotal = 0f;
            for (var pass = 0; pass < occupied.Count; pass++)
            {
                var lift = 0f;
                for (var i = 0; i < occupied.Count; i++)
                {
                    var padded = occupied[i]; padded.xMin -= padding; padded.xMax += padding;
                    padded.yMin -= padding; padded.yMax += padding;
                    if (rect.Overlaps(padded)) lift = Mathf.Max(lift, padded.yMax - rect.yMin);
                }
                if (lift <= 0) break;
                rect.y += lift; liftTotal += lift;
            }
            occupied.Add(rect);
            if (liftTotal <= .01f) return;
            var point = camera.WorldToScreenPoint(barRoot.position);
            barRoot.position = camera.ScreenToWorldPoint(point + Vector3.up * liftTotal);
            // Чуть ближе камеры, чтобы линия заканчивалась у полосы и не скрывалась корпусом.
            var forward = camera.transform.forward;
            _leader.SetPosition(0, AnchorPosition - forward * .025f);
            _leader.SetPosition(1, barRoot.position - camera.transform.up * .065f - forward * .025f);
            _leader.enabled = true;
        }

        private void OnDisable()
        {
            if (_initialized && barRoot != null) barRoot.position = AnchorPosition;
            if (_leader != null) _leader.enabled = false;
        }
    }
}
