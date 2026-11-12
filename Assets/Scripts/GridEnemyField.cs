using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace JunkyWizard.Prototype
{
    public enum MovePattern { Straight, ZigZag, Lateral }

    /// <summary>
    /// Grid mode enemies. Logic is discrete (one cell per beat); visuals interpolate
    /// continuously between cells and the hitbox follows the visual position.
    /// </summary>
    public class GridEnemyField
    {
        const float EnemyHeight = 0.8f;
        static readonly Color FarColor = Color.gray;
        static readonly Color NearColor = new Color(0.9f, 0.12f, 0.1f);

        class Enemy
        {
            public int Column; // -1 or Columns = outside the grid (side spawn, not entered yet)
            public int Row;
            public MovePattern Pattern;
            public int SideDirection; // ±1, zig-zag and lateral patterns
            public bool NextStepIsSide; // lateral pattern
            public Vector3 From;
            public Vector3 To;
            public int MoveBeat; // visual From→To runs from MoveBeat to MoveBeat + 1
            public float WobblePhase;
            public GameObject View;
            public Renderer Renderer;
            public Vector3 VisualPosition;
        }

        readonly PrototypeConfig config;
        readonly PrototypeWorld world;
        readonly List<Enemy> enemies = new List<Enemy>();

        public GridEnemyField(PrototypeConfig config, PrototypeWorld world)
        {
            this.config = config;
            this.world = world;
        }

        int Columns => config.LaneCount;

        /// <summary>Views are destroyed by <see cref="PrototypeWorld.Clear"/>; this only drops the logic.</summary>
        public void Clear() => enemies.Clear();

        /// <summary>
        /// Beat step: enemies that reached row 0 strike, the others move, front rows first.
        /// <paramref name="strike"/> resolves a strike and returns true if the round restarted.
        /// </summary>
        /// <returns>True if the round restarted (stop processing this beat).</returns>
        public bool OnBeat(int beat, Func<GameObject, bool> strike)
        {
            // Normally a row-0 enemy strikes in Tick, the frame its final leg visually ends (= this beat).
            // Fallback for the rare frame where the dsp beat fires before the smoothed visual clock gets there.
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (enemies[i].Row != 0) continue;
                if (StrikeAt(i, strike)) return true;
            }

            // Front rows first, so followers can step into cells freed this beat.
            enemies.Sort((a, b) => a.Row.CompareTo(b.Row));
            foreach (var enemy in enemies) Step(enemy, beat);
            return false;
        }

        public void TrySpawn(int beat)
        {
            float totalWeight = config.spawnWeightBack + config.spawnWeightSide;
            bool side = totalWeight > 0f && Random.value * totalWeight < config.spawnWeightSide;

            int column, row;
            if (side)
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
            enemies.Add(new Enemy
            {
                Column = column,
                Row = row,
                Pattern = PickPattern(),
                SideDirection = Random.value < 0.5f ? -1 : 1,
                NextStepIsSide = true,
                From = position,
                To = position,
                MoveBeat = beat,
                WobblePhase = Random.Range(0f, 2f * Mathf.PI),
                View = view,
                Renderer = view.GetComponent<Renderer>(),
                VisualPosition = position,
            });
        }

        /// <summary>
        /// Per-frame visuals: interpolation with overshoot, lateral wobble, colour by distance.
        /// The final leg into row 0 is linear (never passes the wizard) and the enemy strikes the
        /// frame it visually reaches the wizard row; until then it stays on the field and can be killed.
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
                enemy.VisualPosition = position;
                enemy.View.transform.position = position;

                float visualRow = position.z / config.rowSpacing;
                float danger = Mathf.InverseLerp(config.gridRows, 1f, visualRow);
                enemy.Renderer.material.color = Color.Lerp(FarColor, NearColor, danger);
            }
            return false;
        }

        /// <summary>Removes the enemy at <paramref name="index"/> and resolves its strike.</summary>
        bool StrikeAt(int index, Func<GameObject, bool> strike)
        {
            var enemy = enemies[index];
            enemies.RemoveAt(index);
            return strike(enemy.View);
        }

        /// <summary>Kills every enemy whose hitbox (visual position, enemyHitRadius) touches the jet.</summary>
        /// <param name="origin">Jet start (wand tip).</param>
        /// <param name="direction">Horizontal unit direction.</param>
        public int KillInJet(Vector3 origin, Vector3 direction, float length, float halfWidth)
        {
            var origin2 = new Vector2(origin.x, origin.z);
            var direction2 = new Vector2(direction.x, direction.z).normalized;
            float reach = halfWidth + config.enemyHitRadius;
            int kills = 0;

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                var relative = new Vector2(enemy.VisualPosition.x, enemy.VisualPosition.z) - origin2;
                float along = Mathf.Clamp(Vector2.Dot(relative, direction2), 0f, length);
                if ((relative - direction2 * along).magnitude > reach) continue;

                enemies.RemoveAt(i);
                world.KillEnemy(enemy.View);
                kills++;
            }
            return kills;
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

        void Step(Enemy enemy, int beat)
        {
            int column = enemy.Column;
            int row = enemy.Row;
            int usedSide = 0;

            if (column < 0 || column >= Columns)
            {
                // Side spawn: first move enters the outer column on that side, same row.
                column = column < 0 ? 0 : Columns - 1;
            }
            else
            {
                switch (enemy.Pattern)
                {
                    case MovePattern.Straight:
                        row--;
                        break;
                    case MovePattern.ZigZag:
                        usedSide = BounceSide(column, enemy.SideDirection);
                        column += usedSide;
                        row--;
                        break;
                    case MovePattern.Lateral:
                        if (enemy.NextStepIsSide)
                        {
                            usedSide = BounceSide(column, enemy.SideDirection);
                            column += usedSide;
                        }
                        else
                        {
                            row--;
                        }
                        break;
                }
            }

            var current = CellPosition(enemy.Column, enemy.Row);
            enemy.MoveBeat = beat;
            enemy.From = current;

            if (IsOccupied(column, row))
            {
                // Blocked: stay put this beat and retry the same move next beat.
                enemy.To = current;
                return;
            }

            enemy.Column = column;
            enemy.Row = row;
            enemy.To = CellPosition(column, row);

            if (enemy.Pattern == MovePattern.ZigZag && usedSide != 0)
                enemy.SideDirection = -usedSide; // alternate
            else if (enemy.Pattern == MovePattern.Lateral)
            {
                if (usedSide != 0) enemy.SideDirection = usedSide; // keep going, bounced at the edges
                enemy.NextStepIsSide = !enemy.NextStepIsSide;
            }
        }

        /// <summary>Side step in <paramref name="direction"/>, reversed if it would leave the grid.</summary>
        int BounceSide(int column, int direction)
        {
            int target = column + direction;
            return target < 0 || target >= Columns ? -direction : direction;
        }

        MovePattern PickPattern()
        {
            float straight = Mathf.Max(0f, config.patternWeightStraight);
            float zigZag = Mathf.Max(0f, config.patternWeightZigZag);
            float lateral = Mathf.Max(0f, config.patternWeightLateral);
            float roll = Random.value * (straight + zigZag + lateral);
            if (roll < straight) return MovePattern.Straight;
            if (roll < straight + zigZag) return MovePattern.ZigZag;
            return lateral > 0f ? MovePattern.Lateral : MovePattern.Straight;
        }
    }
}
