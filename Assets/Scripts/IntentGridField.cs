using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace JunkyWizard.Prototype
{
    /// <summary>Next move of a GridV3 enemy: always one row forward, optionally one column sideways (+1 = player's right).</summary>
    public enum Intent { Forward = 0, ForwardRight = 1, ForwardLeft = -1 }

    /// <summary>
    /// GridV3 enemies: one cell per bar, on the first beat of the bar. Each enemy shows its next
    /// move (intent) for the whole bar before it happens. Logic is discrete; visuals interpolate
    /// over one beat with the psychedelic curve, except the straight final leg into row 0.
    /// </summary>
    public class IntentGridField
    {
        public const int BeatsPerBar = 4;

        const float EnemyHeight = 0.8f;
        const float IconHeight = 1.6f; // above the enemy centre
        static readonly Color FarColor = Color.gray;
        static readonly Color NearColor = new Color(0.9f, 0.12f, 0.1f);

        class Enemy
        {
            public int Column; // -1 or Columns = outside the grid (side spawn, not entered yet)
            public int Row;
            public Intent Intent;
            public Vector3 From;
            public Vector3 To;
            public int MoveBeat; // visual From→To runs from MoveBeat to MoveBeat + 1
            public float WobblePhase;
            public GameObject View;
            public Renderer Renderer;
            public Transform Icon;
        }

        readonly PrototypeConfig config;
        readonly PrototypeWorld world;
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly bool singleLane;

        /// <param name="singleLane">SingleLane mode: centre column only, spawns from the back only,
        /// always straight ahead, no intent icons.</param>
        public IntentGridField(PrototypeConfig config, PrototypeWorld world, bool singleLane = false)
        {
            this.config = config;
            this.world = world;
            this.singleLane = singleLane;
        }

        int Columns => config.LaneCount;

        /// <summary>The only column used in SingleLane mode.</summary>
        public int CenterColumn => config.LaneCount / 2;

        public static bool IsBarStart(int beat) => ((beat % BeatsPerBar) + BeatsPerBar) % BeatsPerBar == 0;

        /// <summary>Views are destroyed by <see cref="PrototypeWorld.Clear"/>; this only drops the logic.</summary>
        public void Clear() => enemies.Clear();

        /// <summary>
        /// Beat step. Every beat: fallback strike for row-0 enemies whose final leg is over.
        /// On the first beat of a bar: enemies move following their intent (front rows first) and pick a new one.
        /// </summary>
        /// <param name="strike">Resolves a strike, returns true if the round restarted.</param>
        /// <returns>True if the round restarted (stop processing this beat).</returns>
        public bool OnBeat(int beat, Func<GameObject, bool> strike)
        {
            // Normally a row-0 enemy strikes in Tick, the frame its final leg visually ends.
            // Fallback for the rare frame where the dsp beat fires before the smoothed visual clock gets there.
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (enemies[i].Row != 0 || beat < enemies[i].MoveBeat + 1) continue;
                if (StrikeAt(i, strike)) return true;
            }

            if (!IsBarStart(beat)) return false;

            // Front rows first, so followers can step into cells freed this bar.
            enemies.Sort((a, b) => a.Row.CompareTo(b.Row));
            foreach (var enemy in enemies)
            {
                if (enemy.Row == 0) continue; // still on its final leg (should not happen: it strikes one beat after moving)
                Step(enemy, beat);
            }
            return false;
        }

        public void TrySpawn(int beat)
        {
            float totalWeight = config.spawnWeightBack + config.spawnWeightSide;
            bool side = !singleLane && totalWeight > 0f && Random.value * totalWeight < config.spawnWeightSide;

            int column, row;
            if (singleLane)
            {
                column = CenterColumn;
                row = config.gridRows;
            }
            else if (side)
            {
                column = Random.value < 0.5f ? -1 : Columns;
                row = Random.Range(Mathf.Clamp(config.sideSpawnMinRow, 1, config.gridRows), config.gridRows + 1);
            }
            else
            {
                column = Random.Range(0, Columns);
                row = config.gridRows;
            }
            if (IsOccupied(column, row)) return; // skip this spawn

            var position = CellPosition(column, row);
            var view = world.SpawnGridEnemy(position);
            var enemy = new Enemy
            {
                Column = column,
                Row = row,
                From = position,
                To = position,
                MoveBeat = beat,
                WobblePhase = Random.Range(0f, 2f * Mathf.PI),
                View = view,
                Renderer = view.GetComponent<Renderer>(),
                Icon = singleLane ? null : world.CreateIntentIcon(view.transform),
            };
            PickIntent(enemy);
            enemies.Add(enemy);
        }

        /// <summary>
        /// Per-frame visuals: interpolation with overshoot, lateral wobble, colour by distance, intent icon.
        /// A row-0 enemy strikes the frame it visually reaches the wizard row.
        /// </summary>
        /// <returns>True if a strike restarted the round.</returns>
        public bool Tick(double visualBeat, Func<GameObject, bool> strike)
        {
            float wobbleAngle = (float)(visualBeat * config.wobbleFrequency % 1.0) * 2f * Mathf.PI;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                float t = Mathf.Clamp01((float)(visualBeat - enemy.MoveBeat));
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

                float visualRow = position.z / config.rowSpacing;
                float danger = Mathf.InverseLerp(config.gridRows, 1f, visualRow);
                enemy.Renderer.material.color = Color.Lerp(FarColor, NearColor, danger);

                // Icon stays upright above the head (the enemy cube itself is not rotated).
                if (enemy.Icon != null)
                {
                    enemy.Icon.gameObject.SetActive(!finalLeg);
                    enemy.Icon.position = position + Vector3.up * IconHeight;
                }
            }
            return false;
        }

        /// <summary>Kills every enemy whose logical column is <paramref name="column"/>.</summary>
        public int KillColumn(int column)
        {
            int kills = 0;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (enemies[i].Column != column) continue;
                world.KillEnemy(enemies[i].View);
                enemies.RemoveAt(i);
                kills++;
            }
            return kills;
        }

        /// <summary>Kills every enemy on the field, side spawns not yet entered included.</summary>
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

        void Step(Enemy enemy, int beat)
        {
            var current = CellPosition(enemy.Column, enemy.Row);
            enemy.MoveBeat = beat;
            enemy.From = current;

            int column = enemy.Column + (int)enemy.Intent;
            int row = enemy.Row - 1;
            if (IsOccupied(column, row))
            {
                // Blocked: stay put this bar and choose a new intent.
                enemy.To = current;
            }
            else
            {
                enemy.Column = column;
                enemy.Row = row;
                enemy.To = CellPosition(column, row);
            }

            if (enemy.Row > 0) PickIntent(enemy);
        }

        /// <summary>
        /// Random intent among those that stay inside the grid, weighted by config.
        /// Side spawns outside the grid can only move diagonally into the outer column.
        /// </summary>
        void PickIntent(Enemy enemy)
        {
            if (singleLane)
            {
                enemy.Intent = Intent.Forward;
                return;
            }

            float forward = IsValid(enemy, Intent.Forward) ? Mathf.Max(0f, config.intentWeightForward) : 0f;
            float right = IsValid(enemy, Intent.ForwardRight) ? Mathf.Max(0f, config.intentWeightForwardRight) : 0f;
            float left = IsValid(enemy, Intent.ForwardLeft) ? Mathf.Max(0f, config.intentWeightForwardLeft) : 0f;
            float total = forward + right + left;

            if (total <= 0f)
            {
                // All valid intents weigh 0: fall back to the first valid one.
                enemy.Intent = IsValid(enemy, Intent.Forward) ? Intent.Forward
                    : IsValid(enemy, Intent.ForwardRight) ? Intent.ForwardRight
                    : Intent.ForwardLeft;
            }
            else
            {
                float roll = Random.value * total;
                enemy.Intent = roll < forward ? Intent.Forward
                    : roll < forward + right ? Intent.ForwardRight
                    : Intent.ForwardLeft;
            }
            world.SetIntentIcon(enemy.Icon, (int)enemy.Intent);
        }

        bool IsValid(Enemy enemy, Intent intent)
        {
            int column = enemy.Column + (int)intent;
            return column >= 0 && column < Columns;
        }

        Vector3 CellPosition(int column, int row)
        {
            float x = column < 0 ? -config.sideSpawnX
                : column >= Columns ? config.sideSpawnX
                : config.laneX[column];
            return new Vector3(x, EnemyHeight, row * config.rowSpacing);
        }

        bool IsOccupied(int column, int row)
        {
            foreach (var enemy in enemies)
                if (enemy.Column == column && enemy.Row == row) return true;
            return false;
        }
    }
}
