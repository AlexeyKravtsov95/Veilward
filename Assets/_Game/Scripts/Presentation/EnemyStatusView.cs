using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Veilward.Domain;

namespace Veilward.Presentation
{
    public readonly struct EnemyStatusSlot
    {
        internal EnemyStatusSlot(StatusKind? status) { Status = status; Reaction = null; Trigger = null; }
        internal EnemyStatusSlot(ReactionEvent reaction)
        { Status = null; Reaction = reaction.Kind; Trigger = reaction.TriggerKind; }
        public StatusKind? Status { get; }
        public ReactionKind? Reaction { get; }
        public ReactionTriggerKind? Trigger { get; }
    }

    public readonly struct EnemyStatusSlots
    {
        public EnemyStatusSlots(EnemyStatusSlot first, EnemyStatusSlot second) { First = first; Second = second; }
        public EnemyStatusSlot First { get; }
        public EnemyStatusSlot Second { get; }
    }

    [Serializable]
    internal sealed class EnemyStatusBadgePair
    {
        [SerializeField] private Image firstSlot;
        [SerializeField] private Image secondSlot;
        [SerializeField] private Sprite burn, wet, gust, crack, steam, storm;

        public int VisibleCount => (firstSlot.enabled ? 1 : 0) + (secondSlot.enabled ? 1 : 0);
        public void Display(EnemyStatusSlots slots)
        {
            Set(firstSlot, Artwork(slots.First));
            Set(secondSlot, Artwork(slots.Second));
        }

        private Sprite Artwork(EnemyStatusSlot slot) => slot.Status switch
        {
            StatusKind.Burn => burn,
            StatusKind.Wet => wet,
            StatusKind.Gust => gust,
            StatusKind.Crack => crack,
            _ => slot.Reaction switch
            {
                ReactionKind.Steam => steam,
                ReactionKind.Storm => storm,
                _ => null
            }
        };

        private static void Set(Image image, Sprite sprite)
        {
            if (image.sprite != sprite) image.sprite = sprite;
            if (image.enabled != (sprite != null)) image.enabled = sprite != null;
        }
    }

    public sealed class EnemyStatusView : MonoBehaviour
    {
        public const int CueCapacity = 2;
        // Палитра существующих гибридных снарядов и карточек башен.
        public static readonly Color SteamColor = new Color(1f, .77f, .35f);
        public static readonly Color StormColor = new Color(.42f, .87f, 1f);
        [SerializeField] private RectTransform badgeRoot;
        [SerializeField] private EnemyStatusBadgePair badges;
        public int VisibleSlotCount => badgeRoot.gameObject.activeSelf ? badges.VisibleCount : 0;
        internal void Hide() => badgeRoot.gameObject.SetActive(false);

        public void Refresh(EnemyState enemy, long tick, ReactionCatalog catalog, EnemyReactionMemory memory, double activeTime,
            Camera camera)
        {
            badges.Display(Describe(enemy, tick, catalog, memory, activeTime));
            if (camera != null) badgeRoot.rotation = camera.transform.rotation;
        }

        internal void Layout(Camera camera, Vector3 barPosition, List<Rect> healthRects, List<Rect> occupied)
        {
            if (camera == null || badges.VisibleCount == 0)
            {
                badgeRoot.gameObject.SetActive(false);
                return;
            }

            // Anchor у отрисованной HP-полосы, включая её смещение и линию-связку в плотной группе.
            badgeRoot.position = barPosition + camera.transform.up * .23f - camera.transform.forward * .035f;
            var local = badgeRoot.rect;
            if (badges.VisibleCount == 1) local.xMax = 0;
            var low = camera.WorldToScreenPoint(badgeRoot.TransformPoint(local.min));
            var high = camera.WorldToScreenPoint(badgeRoot.TransformPoint(local.max));
            var rect = Rect.MinMaxRect(low.x, low.y, high.x, high.y);
            var visible = low.z > 0 && !Overlaps(rect, healthRects) && !Overlaps(rect, occupied);
            if (visible) occupied.Add(rect);
            if (badgeRoot.gameObject.activeSelf != visible) badgeRoot.gameObject.SetActive(visible);
        }

        private static bool Overlaps(Rect rect, List<Rect> occupied)
        {
            for (var i = 0; i < occupied.Count; i++)
                if (rect.Overlaps(occupied[i])) return true;
            return false;
        }

        public static EnemyStatusSlots Describe(EnemyState enemy, long tick, ReactionCatalog catalog, EnemyReactionMemory memory,
            double activeTime)
        {
            if (memory?.Cue != null && activeTime < memory.CueUntil)
            {
                var reaction = memory.Cue.Value.Reaction;
                var excludesPair = reaction.TriggerKind == ReactionTriggerKind.StatusPair;
                for (var i = (int)StatusKind.Burn; i <= (int)StatusKind.Crack; i++)
                {
                    var kind = (StatusKind)i;
                    if (Get(enemy, kind) != null &&
                        (!excludesPair || (kind != reaction.CauseFirstKind && kind != reaction.CauseSecondKind)))
                        return new EnemyStatusSlots(new EnemyStatusSlot(reaction), new EnemyStatusSlot(kind));
                }
                return new EnemyStatusSlots(new EnemyStatusSlot(reaction), default);
            }

            if (enemy.Burn != null && enemy.Wet != null && ReactionPresentationState.Cooldown(enemy, ReactionKind.Steam, tick, catalog) == 0)
                return StatusPair(StatusKind.Burn, StatusKind.Wet);
            if (enemy.Wet != null && enemy.Gust != null && ReactionPresentationState.Cooldown(enemy, ReactionKind.Storm, tick, catalog) == 0)
                return StatusPair(StatusKind.Wet, StatusKind.Gust);

            StatusKind? first = null;
            for (var i = (int)StatusKind.Burn; i <= (int)StatusKind.Crack; i++)
            {
                var kind = (StatusKind)i;
                if (Get(enemy, kind) == null) continue;
                if (first.HasValue) return StatusPair(first, kind);
                first = kind;
            }
            return StatusPair(first, null);
        }

        private static StatusInstance Get(EnemyState enemy, StatusKind kind) => kind switch
        {
            StatusKind.Burn => enemy.Burn,
            StatusKind.Wet => enemy.Wet,
            StatusKind.Gust => enemy.Gust,
            _ => enemy.Crack
        };

        private static EnemyStatusSlots StatusPair(StatusKind? first, StatusKind? second) =>
            new EnemyStatusSlots(new EnemyStatusSlot(first), new EnemyStatusSlot(second));
    }
}
