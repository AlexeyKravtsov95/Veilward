using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Veilward.Domain;

namespace Veilward.Presentation
{
    public readonly struct EnemyStatusSlots
    {
        public EnemyStatusSlots(string first, string second, Color firstColor, Color secondColor)
        { First = first; Second = second; FirstColor = firstColor; SecondColor = secondColor; }
        public string First { get; }
        public string Second { get; }
        public Color FirstColor { get; }
        public Color SecondColor { get; }
    }

    public sealed class EnemyStatusView : MonoBehaviour
    {
        [SerializeField] private Text firstSlot;
        [SerializeField] private Text secondSlot;
        private Vector3 _slotOrigin;
        private bool _hasOrigin;
        public static readonly Color SteamColor = new Color(1f, .77f, .35f);
        public static readonly Color StormColor = new Color(.42f, .87f, 1f);
        public int VisibleSlotCount => (firstSlot.gameObject.activeSelf ? 1 : 0) + (secondSlot.gameObject.activeSelf ? 1 : 0);

        public void Refresh(EnemyState enemy, long tick, ReactionCatalog catalog, EnemyReactionMemory memory, double activeTime, Camera camera)
        {
            var slots = Describe(enemy, tick, catalog, memory, activeTime);
            SetSlot(firstSlot, slots.First, slots.FirstColor);
            SetSlot(secondSlot, slots.Second, slots.SecondColor);
            var root = firstSlot.transform.parent;
            if (!_hasOrigin) { _slotOrigin = root.localPosition; _hasOrigin = true; }
            root.localPosition = _slotOrigin;
            if (camera != null) root.rotation = camera.transform.rotation;
        }
        // Простой локальный layout двух уже существующих slots; новых badges нет.
        public void Layout(Camera camera, List<Rect> occupied)
        {
            if (VisibleSlotCount == 0 || camera == null) return;
            var root = firstSlot.transform.parent;
            var rect = Bounds(camera);
            for (var pass = 0; pass < occupied.Count; pass++)
            {
                var lift = 0f;
                for (var i = 0; i < occupied.Count; i++)
                    if (rect.Overlaps(occupied[i])) lift = Mathf.Max(lift, occupied[i].yMax + 4 - rect.yMin);
                if (lift <= 0) break;
                var screen = camera.WorldToScreenPoint(root.position);
                root.position = camera.ScreenToWorldPoint(screen + new Vector3(0, lift, 0));
                rect.y += lift;
            }
            occupied.Add(rect);
        }
        private Rect Bounds(Camera camera)
        {
            var result = SlotBounds(firstSlot, camera);
            if (!firstSlot.gameObject.activeSelf) return SlotBounds(secondSlot, camera);
            if (!secondSlot.gameObject.activeSelf) return result;
            var other = SlotBounds(secondSlot, camera);
            return Rect.MinMaxRect(Mathf.Min(result.xMin, other.xMin), Mathf.Min(result.yMin, other.yMin),
                Mathf.Max(result.xMax, other.xMax), Mathf.Max(result.yMax, other.yMax));
        }
        private static Rect SlotBounds(Text slot, Camera camera)
        {
            var center = (Vector2)camera.WorldToScreenPoint(slot.transform.position);
            var unit = ((Vector2)camera.WorldToScreenPoint(slot.transform.position + camera.transform.right) - center).magnitude;
            var width = Mathf.Min(slot.rectTransform.rect.width, slot.preferredWidth) * slot.transform.lossyScale.x * unit;
            var height = slot.preferredHeight * slot.transform.lossyScale.y * unit;
            return new Rect(center.x - width * .5f - 2, center.y - height * .5f - 2, width + 4, height + 4);
        }

        private static void SetSlot(Text slot, string label, Color color)
        {
            if (slot.text != label) slot.text = label;
            slot.color = color;
            slot.gameObject.SetActive(label.Length > 0);
        }

        public static EnemyStatusSlots Describe(EnemyState enemy, long tick, ReactionCatalog catalog, EnemyReactionMemory memory, double activeTime)
        {
            if (memory?.Cue != null && activeTime < memory.CueUntil)
            {
                var cue = memory.Cue.Value;
                var exclude = cue.Reaction.TriggerKind == ReactionTriggerKind.StatusPair;
                StatusKind? remaining = null;
                for (var i = 0; i < 4; i++)
                {
                    var kind = (StatusKind)i;
                    if (Get(enemy, kind) != null && (!exclude ||
                        (kind != cue.Reaction.CauseFirstKind && kind != cue.Reaction.CauseSecondKind)))
                    { remaining = kind; break; }
                }
                return new EnemyStatusSlots(cue.MarkerLabel,
                    remaining.HasValue ? Label(remaining.Value) : "", cue.Reaction.Kind == ReactionKind.Steam ? SteamColor : StormColor,
                    remaining.HasValue ? ColorFor(remaining.Value) : Color.white);
            }
            StatusKind? first = null, second = null;
            if (enemy.Burn != null && enemy.Wet != null && ReactionPresentationState.Cooldown(enemy, ReactionKind.Steam, tick, catalog) == 0)
            { first = StatusKind.Burn; second = StatusKind.Wet; }
            else if (enemy.Wet != null && enemy.Gust != null && ReactionPresentationState.Cooldown(enemy, ReactionKind.Storm, tick, catalog) == 0)
            { first = StatusKind.Wet; second = StatusKind.Gust; }
            else
                for (var i = 0; i < 4; i++)
                    if (Get(enemy, (StatusKind)i) != null)
                    { if (!first.HasValue) first = (StatusKind)i; else { second = (StatusKind)i; break; } }
            return new EnemyStatusSlots(first.HasValue ? Label(first.Value) : "", second.HasValue ? Label(second.Value) : "",
                first.HasValue ? ColorFor(first.Value) : Color.white, second.HasValue ? ColorFor(second.Value) : Color.white);
        }

        private static StatusInstance Get(EnemyState e, StatusKind k)
        { switch (k) { case StatusKind.Burn: return e.Burn; case StatusKind.Wet: return e.Wet; case StatusKind.Gust: return e.Gust; default: return e.Crack; } }
        private static string Label(StatusKind kind)
        { switch (kind) { case StatusKind.Burn: return "▲ Burn"; case StatusKind.Wet: return "● Wet"; case StatusKind.Gust: return "» Gust"; default: return "× Crack"; } }
        private static Color ColorFor(StatusKind kind)
        { switch (kind) { case StatusKind.Burn: return new Color(1, .55f, .3f); case StatusKind.Wet: return new Color(.4f, .8f, 1); case StatusKind.Gust: return new Color(.5f, 1, .65f); default: return new Color(.85f, .65f, 1); } }
    }
}
