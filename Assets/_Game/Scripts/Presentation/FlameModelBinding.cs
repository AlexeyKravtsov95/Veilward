using UnityEngine;
using Veilward.Domain;

namespace Veilward.Presentation
{
    // Только модель. Tower root, collider и gameplay position остаются прежними.
    public sealed class FlameModelBinding : MonoBehaviour
    {
        [SerializeField] private Transform[] levels;
        [SerializeField] private Renderer[] fallbackRenderers;
        private Transform _model;
        private int _level;

        public Transform Model => _model;
        public int Level => _level;
        public Transform ShotSource => _model != null ? _model.Find("ShotSource") : null;

        public bool Apply(TowerDefinitionId definitionId, int level)
        {
            var supported = definitionId.Value == "flame" && levels != null &&
                level >= 1 && level <= levels.Length && levels[level - 1] != null;
            if (!supported)
            {
                ClearModel();
                SetFallback(true);
                return false;
            }
            if (_model != null && _level == level) return true;
            ClearModel();
            _model = Instantiate(levels[level - 1], transform, false);
            _model.name = "FLAME L" + level;
            _level = level;
            SetFallback(false);
            return true;
        }

        private void SetFallback(bool visible)
        {
            if (fallbackRenderers == null) return;
            foreach (var renderer in fallbackRenderers)
                if (renderer != null) renderer.enabled = visible;
        }

        private void ClearModel()
        {
            if (_model != null)
            {
                // Destroy отложен до конца кадра; старый уровень уже не рисуется.
                _model.gameObject.SetActive(false);
                if (UnityEngine.Application.isPlaying) Destroy(_model.gameObject);
                else DestroyImmediate(_model.gameObject);
            }
            _model = null;
            _level = 0;
        }
    }
}
