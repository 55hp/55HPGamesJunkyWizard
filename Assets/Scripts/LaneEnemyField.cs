using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace JunkyWizard.Prototype
{
    /// <summary>
    /// Lanes1to5 enemies (v4): laneCount columns × gridRows rows, one cell per step (bar / beat / half beat).
    /// 1 lane: always straight, no intent. 2–5 lanes: intents ↑ ↗ ↖ as GridV3, never outside the grid.
    /// Each enemy has an absolute elemental resistance (body colour); proximity is a red halo.
    /// </summary>
    public class LaneEnemyField
    {
        const float EnemyHeight = 0.8f;
        const float IconHeight = 1.6f;

        class Enemy
        {
            public int Column;
            public int Row;
            public Intent Intent;
            public Element Resistance;
            public Vector3 From;
            public Vector3 To;
            public double MoveBeat; // visual From→To runs from MoveBeat to MoveBeat + moveBeats
            public float WobblePhase;
            public GameObject View;
            public Transform Icon; // null with 1 lane
            public Transform Halo;
            public Material HaloMaterial;
        }

        readonly PrototypeConfig config;
        readonly PrototypeWorld world;
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly int laneCount;
        readonly double moveBeats;

        public LaneEnemyField(PrototypeConfig config, PrototypeWorld world, int laneCount)
        {
            this.config = config;
            this.world = world;
            this.laneCount = laneCount;
            // Psychedelic move over 1 beat, shorter if the step itself is shorter (half beat).
            moveBeats = Math.Min(1.0, config.EnemyStepBeats);
        }

        public int LaneCount => laneCount;

        /// <summary>World x of lane <paramref name="column"/>, lanes centred on x = 0.</summary>
        public float LaneX(int column) => (column - (laneCount - 1) * 0.5f) * config.v4LaneSpacing;

        public static Color ResistanceColor(Element element) =>
            element == Element.Fire ? new Color(1f, 0.55f, 0.15f)
            : element == Element.Ice ? new Color(0.45f, 0.8f, 1f)
            : element == Element.Wind ? new Color(0.35f, 0.85f, 0.35f)
            : new Color(0.55f, 0.55f, 0.58f);

        /// <summary>Views are destroyed by <see cref="PrototypeWorld.Clear"/>; this only drops the logic.</summary>
        public void Clear() => enemies.Clear();

        /// <summary>
        /// Enemy step at <paramref name="stepBeat"/>: fallback strikes for finished final legs, then
        /// moves (front rows first) and new intents.
        /// </summary>
        /// <returns>True if a strike restarted the round.</returns>
        public bool OnStep(double stepBeat, Func<GameObject, bool> strike)
        {
            // Normally a row-0 enemy strikes in Tick, the frame its final leg visually ends.
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (enemies[i].Row != 0 || stepBeat < enemies[i].MoveBeat + moveBeats) continue;
                if (StrikeAt(i, strike)) return true;
            }

            enemies.Sort((a, b) => a.Row.CompareTo(b.Row));
            foreach (var enemy in enemies)
            {
                if (enemy.Row == 0) continue;
                Step(enemy, stepBeat);
            }
            return false;
        }

        /// <summary>Spawns at the back row, random lane, random resistance. Skipped if the cell is taken.</summary>
        public void TrySpawn(double beat)
        {
            int column = Random.Range(0, laneCount);
            int row = config.gridRows;
            if (IsOccupied(column, row)) return;

            var resistance = PickResistance();
            var position = CellPosition(column, row);
            var view = world.SpawnGridEnemy(position);
            view.GetComponent<Renderer>().material.color = ResistanceColor(resistance);
            var halo = world.CreateProximityHalo(view.transform);

            var enemy = new Enemy
            {
                Column = column,
                Row = row,
                Resistance = resistance,
                From = position,
                To = position,
                MoveBeat = beat,
                WobblePhase = Random.Range(0f, 2f * Mathf.PI),
                View = view,
                Icon = laneCount > 1 ? world.CreateIntentIcon(view.transform) : null,
                Halo = halo,
                HaloMaterial = halo.GetComponent<Renderer>().material,
            };
            PickIntent(enemy);
            enemies.Add(enemy);
        }

        /// <summary>Per-frame visuals; a row-0 enemy strikes the frame it visually reaches the wizard row.</summary>
        /// <returns>True if a strike restarted the round.</returns>
        public bool Tick(double visualBeat, Func<GameObject, bool> strike)
        {
            float wobbleAngle = (float)(visualBeat * config.wobbleFrequency % 1.0) * 2f * Mathf.PI;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                float t = Mathf.Clamp01((float)((visualBeat - enemy.MoveBeat) / moveBeats));
                bool finalLeg = enemy.Row == 0;
                if (finalLeg && t >= 1f)
                {
                    if (StrikeAt(i, strike)) return true;
                    continue;
                }

                float progress = finalLeg ? t : config.moveCurve.Evaluate(t);
                var position = Vector3.LerpUnclamped(enemy.From, enemy.To, progress);
                position.x += config.wobbleAmplitude * Mathf.Sin(wobbleAngle + enemy.WobblePhase);
                enemy.View.transform.position = position;

                // Proximity: red ground halo under the enemy, growing and thickening towards the wizard
                // (kept off the body so the body colour still reads as the resistance).
                float danger = Mathf.InverseLerp(config.gridRows, 1f, position.z / config.rowSpacing);
                float diameter = Mathf.Lerp(1f, 1.9f, danger);
                enemy.Halo.localScale = new Vector3(diameter, 0.01f, diameter);
                enemy.HaloMaterial.color = new Color(1f, 0.1f, 0.05f, Mathf.Lerp(0.08f, 0.55f, danger));

                if (enemy.Icon != null)
                {
                    enemy.Icon.gameObject.SetActive(!finalLeg);
                    enemy.Icon.position = position + Vector3.up * IconHeight;
                }
            }
            return false;
        }

        /// <summary>
        /// Hits up to <paramref name="maxHits"/> enemies of <paramref name="column"/>, nearest first.
        /// Enemies resisting <paramref name="element"/> are passed through: untouched and not counted.
        /// </summary>
        /// <returns>Kills.</returns>
        public int HitLane(int column, Element element, int maxHits, Color color)
        {
            var lane = new List<Enemy>();
            foreach (var enemy in enemies)
                if (enemy.Column == column && enemy.Resistance != element) lane.Add(enemy);
            lane.Sort((a, b) => a.Row.CompareTo(b.Row));

            int kills = Math.Min(maxHits, lane.Count);
            for (int i = 0; i < kills; i++)
            {
                enemies.Remove(lane[i]);
                world.KillEnemy(lane[i].View, color);
            }
            return kills;
        }

        /// <summary>Kills every enemy (buffer explosion), resistances ignored.</summary>
        public int KillAll()
        {
            int kills = enemies.Count;
            foreach (var enemy in enemies) world.KillEnemy(enemy.View);
            enemies.Clear();
            return kills;
        }

        bool StrikeAt(int index, Func<GameObject, bool> strike)
        {
            var enemy = enemies[index];
            enemies.RemoveAt(index);
            return strike(enemy.View);
        }

        void Step(Enemy enemy, double stepBeat)
        {
            var current = CellPosition(enemy.Column, enemy.Row);
            enemy.MoveBeat = stepBeat;
            enemy.From = current;

            int column = enemy.Column + (int)enemy.Intent;
            int row = enemy.Row - 1;
            if (IsOccupied(column, row))
            {
                enemy.To = current; // blocked: stay put this step, choose a new intent
            }
            else
            {
                enemy.Column = column;
                enemy.Row = row;
                enemy.To = CellPosition(column, row);
            }

            if (enemy.Row > 0) PickIntent(enemy);
        }

        void PickIntent(Enemy enemy)
        {
            if (enemy.Icon == null)
            {
                enemy.Intent = Intent.Forward;
                return;
            }

            float forward = IsValid(enemy, Intent.Forward) ? Mathf.Max(0f, config.intentWeightForward) : 0f;
            float right = IsValid(enemy, Intent.ForwardRight) ? Mathf.Max(0f, config.intentWeightForwardRight) : 0f;
            float left = IsValid(enemy, Intent.ForwardLeft) ? Mathf.Max(0f, config.intentWeightForwardLeft) : 0f;
            float roll = Random.value * (forward + right + left);
            enemy.Intent = forward + right + left <= 0f ? Intent.Forward
                : roll < forward ? Intent.Forward
                : roll < forward + right ? Intent.ForwardRight
                : Intent.ForwardLeft;
            world.SetIntentIcon(enemy.Icon, (int)enemy.Intent);
        }

        bool IsValid(Enemy enemy, Intent intent)
        {
            int column = enemy.Column + (int)intent;
            return column >= 0 && column < laneCount;
        }

        Element PickResistance()
        {
            float none = Mathf.Max(0f, config.resistWeightNone);
            float fire = Mathf.Max(0f, config.resistWeightFire);
            float ice = Mathf.Max(0f, config.resistWeightIce);
            float wind = Mathf.Max(0f, config.resistWeightWind);
            float total = none + fire + ice + wind;
            if (total <= 0f) return Element.None;

            float roll = Random.value * total;
            if (roll < none) return Element.None;
            if (roll < none + fire) return Element.Fire;
            if (roll < none + fire + ice) return Element.Ice;
            return wind > 0f ? Element.Wind : Element.None;
        }

        Vector3 CellPosition(int column, int row) => new Vector3(LaneX(column), EnemyHeight, row * config.rowSpacing);

        bool IsOccupied(int column, int row)
        {
            foreach (var enemy in enemies)
                if (enemy.Column == column && enemy.Row == row) return true;
            return false;
        }
    }
}
