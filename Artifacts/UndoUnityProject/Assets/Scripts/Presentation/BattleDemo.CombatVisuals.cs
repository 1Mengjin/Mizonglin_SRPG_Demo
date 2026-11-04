using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zilong.Forest;

namespace Zilong.Demo
{
    public sealed partial class BattleDemo
    {
        sealed class OverheadHud
        {
            public RectTransform root;
            public Text name, hp;
            public RectTransform fill, trail;
            public float current, trailing, target, holdUntil;
        }
        RectTransform overheadLayer;
        readonly Dictionary<DemoUnit, OverheadHud> overheadHuds = new Dictionary<DemoUnit, OverheadHud>();
        readonly Dictionary<DemoUnit, Vector3> figurePositionOverrides = new Dictionary<DemoUnit, Vector3>();

        void ClearCombatPresentation()
        {
            if (overheadLayer != null) ReleaseObject(overheadLayer.gameObject);
            overheadLayer = null; overheadHuds.Clear(); figurePositionOverrides.Clear();
        }
        void EnsureOverheadLayer()
        {
            if (overheadLayer != null) return;
            var go = new GameObject("Unit nameplates and combat text", typeof(RectTransform)); go.transform.SetParent(canvas.transform, false);
            overheadLayer = go.GetComponent<RectTransform>(); overheadLayer.anchorMin = Vector2.zero; overheadLayer.anchorMax = Vector2.one;
            overheadLayer.offsetMin = overheadLayer.offsetMax = Vector2.zero; overheadLayer.SetAsFirstSibling();
        }
        void MakeOverheadHud(DemoUnit unit)
        {
            EnsureOverheadLayer();
            var root = Box(overheadLayer, unit.id + " nameplate", new Vector2(.5f, .5f), Vector2.zero, new Vector2(150, 68), new Color(0, 0, 0, .76f));
            var name = Label(root, "Name", unit.name, new Vector2(.5f, 1), new Vector2(0, -4), new Vector2(142, 25), 19, TextAnchor.MiddleCenter);
            name.fontStyle = FontStyle.Bold; name.supportRichText = false;
            var shadow = name.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .95f); shadow.effectDistance = new Vector2(1, -1);
            var track = Box(root, "Health track", new Vector2(.5f, .5f), new Vector2(0, -2), new Vector2(134, 12), new Color(.14f, .14f, .15f));
            var trail = Box(track, "Recent health loss", new Vector2(0, .5f), Vector2.zero, new Vector2(134, 12), new Color(1, .72f, .18f));
            var fill = Box(track, "Health", new Vector2(0, .5f), Vector2.zero, new Vector2(134, 12), unit.enemy ? new Color(.95f, .25f, .20f) : new Color(.22f, .88f, .55f));
            var hp = Label(root, "Health value", "", new Vector2(.5f, 0), new Vector2(0, 3), new Vector2(142, 20), 16, TextAnchor.MiddleCenter);
            hp.fontStyle = FontStyle.Bold; hp.color = Color.white;
            float ratio = unit.hp / (float)Mathf.Max(1, unit.stats?.hp ?? unit.hp);
            overheadHuds[unit] = new OverheadHud { root = root, name = name, hp = hp, fill = fill, trail = trail, current = ratio, trailing = ratio, target = ratio };
            labels[unit] = name;
        }
        void RefreshOverheadHuds()
        {
            foreach (var pair in overheadHuds)
            {
                var unit = pair.Key; var hud = pair.Value;
                int hp = unit.hp, max = unit.stats?.hp ?? hp;
                if (ForestEnabled && forestSession.State.unitsById.TryGetValue(unit.id, out var authoritative))
                { hp = authoritative.currentHp; max = ForestRules.Stats(forestSession.State, authoritative).maxHp; }
                float ratio = Mathf.Clamp01(hp / (float)Mathf.Max(1, max));
                if (ratio < hud.target) hud.holdUntil = Time.unscaledTime + .38f;
                if (ratio > hud.target) hud.current = hud.trailing = ratio;
                hud.target = ratio; hud.name.text = unit.name;
                hud.fill.GetComponent<Image>().color = unit.enemy || ratio <= .25f ? new Color(.98f, .25f, .20f)
                    : ratio <= .5f ? new Color(1, .72f, .18f) : new Color(.22f, .88f, .55f);
                hud.name.color = unit.enemy ? new Color(1, .84f, .73f) : new Color(.80f, 1, .92f);
                hud.hp.text = hp + " / " + max;
                if (!Application.isPlaying) hud.current = hud.trailing = ratio;
                hud.fill.sizeDelta = new Vector2(134 * hud.current, 12); hud.trail.sizeDelta = new Vector2(134 * hud.trailing, 12);
            }
        }
        void UpdateOverheadHud(DemoUnit unit, Vector3 world)
        {
            if (!overheadHuds.TryGetValue(unit, out var hud)) return;
            hud.current = Mathf.MoveTowards(hud.current, hud.target, Time.unscaledDeltaTime * 3.5f);
            if (Time.unscaledTime >= hud.holdUntil) hud.trailing = Mathf.MoveTowards(hud.trailing, hud.target, Time.unscaledDeltaTime * .85f);
            hud.trailing = Mathf.Max(hud.trailing, hud.current);
            hud.fill.sizeDelta = new Vector2(134 * hud.current, 12); hud.trail.sizeDelta = new Vector2(134 * hud.trailing, 12);
            var screen = view.WorldToScreenPoint(world + Vector3.up * 1.40f);
            hud.root.gameObject.SetActive(screen.z > 0);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, HudCamera, out var local);
            hud.root.anchoredPosition = local;
            if (State.selected == unit) hud.root.SetAsLastSibling();
        }
        Transform Visual(DemoUnit unit) => unit != null && figures.TryGetValue(unit, out var root) ? root.Find("Visual") : null;
        IEnumerator AnimateEnemyTravel(ForestCommand command)
        {
            var actor = State.units.FirstOrDefault(u => u.id == command.actorId);
            if (actor == null || !figures.TryGetValue(actor, out var root) || command.path == null || command.path.Count <= 1) yield break;
            foreach (var cell in command.path.Skip(1))
            {
                var target = World(cell);
                while (root != null && Vector3.Distance(root.position, target) > .002f)
                {
                    figurePositionOverrides[actor] = Vector3.MoveTowards(root.position, target, Time.unscaledDeltaTime * 7);
                    yield return null;
                }
                figurePositionOverrides[actor] = target;
            }
        }
        IEnumerator AnimateActionLeadIn(ForestCommand command)
        {
            if (command.kind == ActionKind.Wait || ForestRules.IsSpiritAction(command.kind)) yield break;
            var actor = State.units.FirstOrDefault(u => u.id == command.actorId); var visual = Visual(actor);
            if (visual == null) yield break;
            var target = State.units.FirstOrDefault(u => u.id == command.targetId);
            bool strike = command.kind == ActionKind.BasicAttack || command.kind == ActionKind.Retreat && target != null;
            var direction = target == null ? Vector3.zero : World(target.cell) - visual.position;
            direction.y = 0; direction.Normalize();
            float start = Time.unscaledTime;
            while (visual != null && Time.unscaledTime - start < .20f)
            {
                float pulse = Mathf.Sin((Time.unscaledTime - start) / .20f * Mathf.PI);
                visual.localPosition = strike ? direction * (.28f * pulse) : Vector3.up * (.08f * pulse);
                visual.localScale = new Vector3(1 + .07f * pulse, 1 - .06f * pulse, 1 + .07f * pulse);
                visual.localRotation = Quaternion.Euler(0, 0, strike ? -direction.x * 9 * pulse : 0);
                yield return null;
            }
            if (visual != null) { visual.localPosition = Vector3.zero; visual.localScale = Vector3.one; visual.localRotation = Quaternion.identity; }
        }
        IEnumerator PlayForestImpacts(int eventStart)
        {
            var events = forestSession.State.eventLog.Skip(eventStart).ToArray();
            bool buffPulse = PlayBuffSettlementEffects(events);
            RefreshOverheadHuds();
            foreach (var group in events.Where(e => e.amount > 0 && (ForestFeedback.IsDamage(e.kind) || ForestFeedback.IsHealing(e.kind)) && e.targetId != null).GroupBy(e => e.targetId))
            {
                var target = State.units.FirstOrDefault(u => u.id == group.Key);
                int damage = group.Where(e => ForestFeedback.IsDamage(e.kind)).Sum(e => e.amount), healing = group.Where(e => ForestFeedback.IsHealing(e.kind)).Sum(e => e.amount);
                var position = target != null && figures.TryGetValue(target, out var root) ? root.position : World(group.First().cell);
                if (damage > 0) StartCoroutine(FloatCombatText(position, "−" + damage, "扣血", new Color(1, .30f, .16f), 0));
                if (healing > 0) StartCoroutine(FloatCombatText(position, "+" + healing, "治疗", new Color(.35f, 1, .56f), damage > 0 ? 1 : 0));
                if (target != null) StartCoroutine(AnimateHit(target, damage > 0));
            }
            foreach (var e in events.Where(e => e.kind == "AllyTeleported")) StartCoroutine(FloatCombatText(World(e.cell), "传送", "符箓·引", jade, 0));
            foreach (var e in events.Where(e => e.kind == "CorpseSummoned")) StartCoroutine(FloatCombatText(World(e.cell), "腐卒", "召唤", red, 0));
            foreach (var e in events.Where(e => e.kind == "PhaseChanged")) StartCoroutine(FloatCombatText(World(e.cell), "阶段 " + e.amount, "千面转阶段", gold, 1));
            if (buffPulse || events.Any(e => e.amount > 0 && (ForestFeedback.IsDamage(e.kind) || ForestFeedback.IsHealing(e.kind)))) yield return new WaitForSecondsRealtime(buffPulse ? BuffPulseDuration + .04f : .32f);
        }
        IEnumerator AnimateHit(DemoUnit unit, bool damage)
        {
            var visual = Visual(unit); if (visual == null) yield break;
            var renderers = visual.GetComponentsInChildren<Renderer>(); var colors = renderers.Select(r => r.sharedMaterial.color).ToArray();
            bool dead = ForestEnabled && forestSession.State.unitsById.TryGetValue(unit.id, out var authoritative) && !ForestRules.Alive(authoritative);
            float start = Time.unscaledTime;
            while (visual != null && Time.unscaledTime - start < .28f)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - start) / .28f), pulse = Mathf.Sin(t * Mathf.PI);
                visual.localPosition = damage ? new Vector3(Mathf.Sin(t * 34) * .065f * (1 - t), .035f * pulse, 0) : Vector3.up * (.06f * pulse);
                visual.localScale = dead ? Vector3.one * Mathf.Lerp(1, .06f, t) : new Vector3(1 + .12f * pulse, 1 - .13f * pulse, 1 + .12f * pulse);
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null)
                    renderers[i].sharedMaterial.color = Color.Lerp(colors[i], t < .22f ? Color.white : damage ? new Color(1, .22f, .12f) : jade, pulse * .86f);
                yield return null;
            }
            if (visual != null)
            {
                visual.localPosition = Vector3.zero; visual.localScale = dead ? Vector3.one * .06f : Vector3.one;
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].sharedMaterial.color = colors[i];
            }
        }
        IEnumerator FloatCombatText(Vector3 world, string value, string caption, Color color, int lane)
        {
            EnsureOverheadLayer();
            var screen = view.WorldToScreenPoint(world + Vector3.up * 1.65f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, HudCamera, out var local);
            local += new Vector2(lane * 26, 55 + lane * 46);
            var bounds = ((RectTransform)canvas.transform).rect;
            local.x = Mathf.Clamp(local.x, bounds.xMin + 95, bounds.xMax - 95);
            local.y = Mathf.Clamp(local.y, bounds.yMin + 55, bounds.yMax - 110);
            var panel = Box(overheadLayer, "Combat number", new Vector2(.5f, .5f), local, new Vector2(150, 80), new Color(0, 0, 0, .76f));
            var group = panel.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            var text = Label(panel, "Amount", value, new Vector2(.5f, .5f), new Vector2(0, -6), new Vector2(146, 52), 39, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold; text.color = color;
            var outline = text.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0, 0, 0, .95f); outline.effectDistance = new Vector2(2, -2);
            Label(panel, "Effect", caption, new Vector2(.5f, 1), new Vector2(0, -3), new Vector2(142, 21), 15, TextAnchor.MiddleCenter);
            float start = Time.unscaledTime;
            while (panel != null && Time.unscaledTime - start < 1.15f)
            {
                float t = (Time.unscaledTime - start) / 1.15f;
                panel.anchoredPosition = local + Vector2.up * (65 * (1 - (1 - t) * (1 - t)));
                panel.localScale = Vector3.one * (1 + .25f * Mathf.Max(0, 1 - t * 6)); group.alpha = t < .62f ? 1 : (1 - t) / .38f;
                yield return null;
            }
            if (panel != null) ReleaseObject(panel.gameObject);
        }
        void RemoveGoneFigures()
        {
            foreach (var unit in figures.Keys.Where(u => !State.units.Contains(u)).ToArray())
            {
                foreach (var renderer in figures[unit].GetComponentsInChildren<Renderer>()) { materials.Remove(renderer.sharedMaterial); ReleaseObject(renderer.sharedMaterial); }
                ReleaseObject(figures[unit].gameObject); figures.Remove(unit); labels.Remove(unit); figurePositionOverrides.Remove(unit);
                if (overheadHuds.TryGetValue(unit, out var hud)) { ReleaseObject(hud.root.gameObject); overheadHuds.Remove(unit); }
            }
            if (State.units.Any(u => !figures.ContainsKey(u))) ResetFigures();
        }
    }
}
