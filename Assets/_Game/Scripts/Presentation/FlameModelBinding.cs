using UnityEngine;
using Veilward.Domain;

namespace Veilward.Presentation
{
    // Только модель. Tower root, collider и gameplay position остаются прежними.
    public sealed class FlameModelBinding : MonoBehaviour
    {
        [SerializeField] private Transform[] levels;
        [SerializeField] private Renderer[] fallbackRenderers;
        [SerializeField] private Transform[] tideLevels;
        [SerializeField] private Transform[] galeLevels;
        [SerializeField] private Transform[] stoneLevels;
        [SerializeField] private Transform[] steamLevels;
        [SerializeField] private Transform[] stormLevels;
        private Transform _model;
        private int _level;
        private TowerDefinitionId _definitionId;
        private InkFlameView[] _inkLevels;
        private Camera _inkCamera;
        private InkFlameView _ink;

        public Transform Model => _model;
        public int Level => _level;
        public Transform ShotSource => _ink != null ? _ink.Muzzle : _model != null ? _model.Find("ShotSource") : null;

        internal void ConfigureInk(InkFlameView[] inkLevels, Camera camera)
        {
            _inkLevels = inkLevels;
            _inkCamera = camera;
        }

        internal void AdvanceInk(float activeDelta)
        {
            if (_ink != null) _ink.Advance(activeDelta);
        }

        internal void ReactToShot(Vector3 target)
        {
            if (_ink == null) return;
            _ink.Aim(target);
            _ink.Recoil();
        }

        public bool Apply(TowerDefinitionId definitionId, int level)
        {
            var family = definitionId.Value == "flame" ? levels : definitionId.Value == "tide" ? tideLevels : definitionId.Value == "gale" ? galeLevels : definitionId.Value == "stone" ? stoneLevels : definitionId.Value == "steam" ? steamLevels : definitionId.Value == "storm" ? stormLevels : null;
            var inkLevel = definitionId.Value == "flame" && _inkCamera != null &&
                _inkLevels != null && level >= 1 && level <= _inkLevels.Length ? _inkLevels[level - 1] : null;
            var supported = inkLevel != null || (family != null && level >= 1 && level <= family.Length && family[level - 1] != null);
            if (!supported)
            {
                ClearModel();
                SetFallback(true);
                return false;
            }
            if (_model != null && _definitionId == definitionId && _level == level) return true;
            ClearModel();
            if (inkLevel != null)
            {
                _ink = Instantiate(inkLevel, transform, false);
                _ink.Face(_inkCamera);
                _model = _ink.transform;
            }
            else _model = Instantiate(family[level - 1], transform, false);
            _model.name = definitionId.Value.ToUpperInvariant() + " L" + level;
            _definitionId = definitionId;
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
            _ink = null;
            _level = 0;
            _definitionId = default;
        }
    }
}
