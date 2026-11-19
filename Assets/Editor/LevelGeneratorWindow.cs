using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>
/// BFS solver and procedural level generator for 4 Blocks v2.
/// Simulates the exact simultaneous block-sliding mechanics from GameManager + GridSystem.
/// </summary>
public static class LevelPuzzleSolver
{
    public enum StartPattern
    {
        Auto,
        Line,
        Cluster2x2,
        Corners,
        Random
    }

    public class SolveResult
    {
        public bool IsSolvable;
        public List<string> Moves = new List<string>();
        public List<Vector2Int[]> StepStates = new List<Vector2Int[]>();
        public int ExploredStates;
    }

    public class GeneratedLevel
    {
        public int GridWidth;
        public int GridHeight;
        public bool RequireExactTargetMatch;
        public Vector2Int[] BlockStartPositions;
        public Vector2Int[] BlockTargetPositions;
        public Vector2Int[] WallPositions;
        public int OutlineWallCount;
        public int FloatingWallCount;
        public string LayoutSignature;
        public string WallSignature;
        public SolveResult Solution;
    }

    private static readonly Vector2Int[] Dirs =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    private static readonly string[] DirNames = { "Up", "Down", "Left", "Right" };

    // ──────────────────────────────────────────────
    //  EXACT SLIDE SIMULATION
    // ──────────────────────────────────────────────

    /// <summary>
    /// Simulates sliding all 4 blocks simultaneously in one direction.
    /// Blocks nearest to the wall in the slide direction are processed first.
    /// Preserves each block's index (0..3) in the returned array.
    /// </summary>
    public static Vector2Int[] SimulateSlide(Vector2Int[] blocks, Vector2Int dir, int width, int height, bool[,] wallGrid)
    {
        int count = blocks.Length;
        int[] order = new int[count];
        for (int i = 0; i < count; i++) order[i] = i;

        // Sort indices farthest along slide direction first
        Array.Sort(order, (a, b) =>
        {
            int projA = blocks[a].x * dir.x + blocks[a].y * dir.y;
            int projB = blocks[b].x * dir.x + blocks[b].y * dir.y;
            return projB.CompareTo(projA);
        });

        Vector2Int[] result = new Vector2Int[count];
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();

        for (int i = 0; i < count; i++)
        {
            int idx = order[i];
            Vector2Int cur = blocks[idx];

            while (true)
            {
                Vector2Int next = cur + dir;
                if (next.x < 0 || next.x >= width || next.y < 0 || next.y >= height) break;
                if (wallGrid[next.x, next.y]) break;
                if (occupied.Contains(next)) break;
                cur = next;
            }

            occupied.Add(cur);
            result[idx] = cur;
        }

        return result;
    }

    // ──────────────────────────────────────────────
    //  BFS SOLVER (FOR ANY EXISTING LEVELDATA)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Finds the shortest (optimal) move sequence for a LevelData asset using BFS.
    /// </summary>
    public static SolveResult Solve(LevelData data, int maxStates = 15000)
    {
        SolveResult result = new SolveResult();
        if (data == null || data.gridWidth <= 0 || data.gridHeight <= 0) return result;
        if (data.blockStartPositions == null || data.blockStartPositions.Length != 4) return result;
        if (data.blockTargetPositions == null || data.blockTargetPositions.Length != 4) return result;

        int w = data.gridWidth;
        int h = data.gridHeight;
        bool[,] walls = BuildWallGrid(w, h, data.wallPositions);

        for (int i = 0; i < 4; i++)
        {
            Vector2Int s = data.blockStartPositions[i];
            Vector2Int t = data.blockTargetPositions[i];
            if (s.x < 0 || s.x >= w || s.y < 0 || s.y >= h || walls[s.x, s.y]) return result;
            if (t.x < 0 || t.x >= w || t.y < 0 || t.y >= h || walls[t.x, t.y]) return result;
        }

        bool exact = data.requireExactTargetMatch;
        string startKey = EncodeState(data.blockStartPositions, exact);
        string targetKey = EncodeState(data.blockTargetPositions, exact);

        Dictionary<string, StateNode> visited = new Dictionary<string, StateNode>();
        Queue<Vector2Int[]> queue = new Queue<Vector2Int[]>();

        Vector2Int[] initial = (Vector2Int[])data.blockStartPositions.Clone();
        visited[startKey] = new StateNode { PrevKey = null, MoveName = null, Positions = initial, Depth = 0 };
        queue.Enqueue(initial);

        while (queue.Count > 0 && visited.Count < maxStates)
        {
            Vector2Int[] cur = queue.Dequeue();
            string curKey = EncodeState(cur, exact);

            if (curKey == targetKey)
            {
                BuildPathResult(visited, curKey, result);
                result.IsSolvable = true;
                result.ExploredStates = visited.Count;
                return result;
            }

            int depth = visited[curKey].Depth;
            for (int d = 0; d < 4; d++)
            {
                Vector2Int[] next = SimulateSlide(cur, Dirs[d], w, h, walls);
                string nextKey = EncodeState(next, exact);
                if (!visited.ContainsKey(nextKey))
                {
                    visited[nextKey] = new StateNode
                    {
                        PrevKey = curKey,
                        MoveName = DirNames[d],
                        Positions = next,
                        Depth = depth + 1
                    };
                    queue.Enqueue(next);
                }
            }
        }

        result.IsSolvable = false;
        result.ExploredStates = visited.Count;
        return result;
    }

    // ──────────────────────────────────────────────
    //  PROCEDURAL LEVEL GENERATOR (FORWARD BFS)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Generates a guaranteed solvable, unique level with randomized wall counts inside
    /// [minOutlineWalls, maxOutlineWalls] and [minFloatingWalls, maxFloatingWalls],
    /// verified not to duplicate any layout in excludedLayoutSignatures (including rotations & flips)
    /// and not reusing previousWallSignature.
    /// </summary>
    public static GeneratedLevel GenerateLevel(
        int width,
        int height,
        int minOutlineWalls,
        int maxOutlineWalls,
        int minFloatingWalls,
        int maxFloatingWalls,
        int minMoves,
        int maxMoves,
        bool requireExactTargetMatch,
        StartPattern startPattern,
        HashSet<string> excludedLayoutSignatures = null,
        string previousWallSignature = null,
        int maxAttempts = 160)
    {
        int safeMinOutline = Mathf.Max(0, minOutlineWalls);
        int safeMaxOutline = Mathf.Max(safeMinOutline, maxOutlineWalls);
        int safeMinFloating = Mathf.Max(0, minFloatingWalls);
        int safeMaxFloating = Mathf.Max(safeMinFloating, maxFloatingWalls);

        System.Random rng = new System.Random(Guid.NewGuid().GetHashCode());
        GeneratedLevel bestOverall = null;
        int bestOverallScore = int.MinValue;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // 1. Pick random wall counts inside the user's Min-Max windows
            int targetOutline = rng.Next(safeMinOutline, safeMaxOutline + 1);
            int targetFloating = rng.Next(safeMinFloating, safeMaxFloating + 1);

            List<Vector2Int> walls = GenerateWalls(width, height, targetOutline, targetFloating, rng, out int actualOutline, out int actualFloating);
            if (walls == null) continue;
            if (actualOutline < safeMinOutline || actualOutline > safeMaxOutline) continue;
            if (actualFloating < safeMinFloating || actualFloating > safeMaxFloating) continue;

            Vector2Int[] wallArray = walls.ToArray();
            string wallSig = ComputeCanonicalWallSignature(width, height, wallArray);

            // Avoid repeating the exact same wall silhouette as the immediately previous generated level
            if (!string.IsNullOrEmpty(previousWallSignature) && (safeMaxOutline > 0 || safeMaxFloating > 0) && wallSig == previousWallSignature)
            {
                continue;
            }

            bool[,] wallGrid = BuildWallGrid(width, height, walls);

            // Ensure playable area is a single connected component with enough space
            List<Vector2Int> playable = GetPlayableCells(width, height, wallGrid);
            if (playable.Count < 10 || !IsPlayableAreaConnected(width, height, wallGrid, playable))
                continue;

            // 2. Pick 4 distinct starting positions
            StartPattern pattern = startPattern == StartPattern.Auto
                ? (StartPattern)rng.Next(1, 5)
                : startPattern;

            Vector2Int[] starts = PickStartPositions(width, height, wallGrid, playable, pattern, rng);
            if (starts == null) continue;

            // 3. Forward BFS from starts up to maxMoves
            string startKey = EncodeState(starts, requireExactTargetMatch);
            Dictionary<string, StateNode> visited = new Dictionary<string, StateNode>();
            Queue<Vector2Int[]> queue = new Queue<Vector2Int[]>();

            visited[startKey] = new StateNode { PrevKey = null, MoveName = null, Positions = starts, Depth = 0 };
            queue.Enqueue(starts);

            int stateCap = requireExactTargetMatch ? 5000 : 3500;
            StateNode bestNode = null;
            string bestKey = null;
            string bestLayoutSig = null;
            int bestScore = int.MinValue;

            while (queue.Count > 0 && visited.Count < stateCap)
            {
                Vector2Int[] cur = queue.Dequeue();
                string curKey = EncodeState(cur, requireExactTargetMatch);
                StateNode node = visited[curKey];

                if (node.Depth >= minMoves && node.Depth <= maxMoves)
                {
                    int score = ScoreTargetState(cur, starts, width, height, node.Depth, minMoves, maxMoves);
                    if (score > bestScore)
                    {
                        string candidateSig = ComputeCanonicalLayoutSignature(width, height, wallArray, starts, cur, requireExactTargetMatch);
                        bool isDuplicate = excludedLayoutSignatures != null && excludedLayoutSignatures.Contains(candidateSig);
                        if (!isDuplicate)
                        {
                            bestScore = score;
                            bestNode = node;
                            bestKey = curKey;
                            bestLayoutSig = candidateSig;
                        }
                    }
                }

                if (node.Depth >= maxMoves) continue;

                for (int d = 0; d < 4; d++)
                {
                    Vector2Int[] next = SimulateSlide(cur, Dirs[d], width, height, wallGrid);
                    string nextKey = EncodeState(next, requireExactTargetMatch);
                    if (!visited.ContainsKey(nextKey))
                    {
                        visited[nextKey] = new StateNode
                        {
                            PrevKey = curKey,
                            MoveName = DirNames[d],
                            Positions = next,
                            Depth = node.Depth + 1
                        };
                        queue.Enqueue(next);
                    }
                }
            }

            if (bestNode != null && bestScore > bestOverallScore)
            {
                SolveResult solveResult = new SolveResult
                {
                    IsSolvable = true,
                    ExploredStates = visited.Count
                };
                BuildPathResult(visited, bestKey, solveResult);

                bestOverallScore = bestScore;
                bestOverall = new GeneratedLevel
                {
                    GridWidth = width,
                    GridHeight = height,
                    RequireExactTargetMatch = requireExactTargetMatch,
                    BlockStartPositions = starts,
                    BlockTargetPositions = (Vector2Int[])bestNode.Positions.Clone(),
                    WallPositions = wallArray,
                    OutlineWallCount = actualOutline,
                    FloatingWallCount = actualFloating,
                    LayoutSignature = bestLayoutSig,
                    WallSignature = wallSig,
                    Solution = solveResult
                };

                // Early exit once we find a strong unique puzzle candidate
                if (bestScore >= 32) break;
            }
        }

        return bestOverall;
    }

    // ──────────────────────────────────────────────
    //  CANONICAL LAYOUT FINGERPRINTS (ANTI-DUPLICATE)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Computes a rotation- and mirror-invariant fingerprint for a LevelData asset.
    /// Two levels produce the same signature if they are identical or rotated/flipped copies.
    /// </summary>
    public static string ComputeCanonicalLayoutSignature(LevelData data)
    {
        if (data == null || data.gridWidth <= 0 || data.gridHeight <= 0) return string.Empty;
        if (data.blockStartPositions == null || data.blockStartPositions.Length != 4) return string.Empty;
        if (data.blockTargetPositions == null || data.blockTargetPositions.Length != 4) return string.Empty;

        return ComputeCanonicalLayoutSignature(
            data.gridWidth,
            data.gridHeight,
            data.wallPositions ?? Array.Empty<Vector2Int>(),
            data.blockStartPositions,
            data.blockTargetPositions,
            data.requireExactTargetMatch
        );
    }

    public static string ComputeCanonicalLayoutSignature(
        int w,
        int h,
        Vector2Int[] walls,
        Vector2Int[] starts,
        Vector2Int[] targets,
        bool exactMatch)
    {
        string best = null;

        for (int flip = 0; flip < 2; flip++)
        {
            for (int rot = 0; rot < 4; rot++)
            {
                GetTransformedSize(w, h, rot, out int tw, out int th);

                List<int> wallCodes = new List<int>(walls != null ? walls.Length : 0);
                if (walls != null)
                {
                    foreach (Vector2Int p in walls)
                    {
                        if (p.x < 0 || p.x >= w || p.y < 0 || p.y >= h) continue;
                        Vector2Int tp = TransformPoint(p, w, h, flip == 1, rot);
                        int code = tp.y * 64 + tp.x;
                        if (!wallCodes.Contains(code)) wallCodes.Add(code);
                    }
                    wallCodes.Sort();
                }

                Vector2Int[] tStarts = new Vector2Int[4];
                Vector2Int[] tTargets = new Vector2Int[4];
                for (int i = 0; i < 4; i++)
                {
                    tStarts[i] = TransformPoint(starts[i], w, h, flip == 1, rot);
                    tTargets[i] = TransformPoint(targets[i], w, h, flip == 1, rot);
                }

                string sStr = EncodeState(tStarts, exactMatch);
                string tStr = EncodeState(tTargets, exactMatch);
                string wStr = string.Join(",", wallCodes);
                string sig = $"{tw}x{th}|{(exactMatch ? "E" : "A")}|W:{wStr}|S:{sStr}|T:{tStr}";

                if (best == null || string.CompareOrdinal(sig, best) < 0)
                {
                    best = sig;
                }
            }
        }

        return best ?? string.Empty;
    }

    /// <summary>
    /// Computes a rotation- and mirror-invariant fingerprint of just the wall silhouette.
    /// </summary>
    public static string ComputeCanonicalWallSignature(int w, int h, Vector2Int[] walls)
    {
        string best = null;

        for (int flip = 0; flip < 2; flip++)
        {
            for (int rot = 0; rot < 4; rot++)
            {
                GetTransformedSize(w, h, rot, out int tw, out int th);
                List<int> wallCodes = new List<int>(walls != null ? walls.Length : 0);
                if (walls != null)
                {
                    foreach (Vector2Int p in walls)
                    {
                        if (p.x < 0 || p.x >= w || p.y < 0 || p.y >= h) continue;
                        Vector2Int tp = TransformPoint(p, w, h, flip == 1, rot);
                        int code = tp.y * 64 + tp.x;
                        if (!wallCodes.Contains(code)) wallCodes.Add(code);
                    }
                    wallCodes.Sort();
                }

                string sig = $"{tw}x{th}|W:{string.Join(",", wallCodes)}";
                if (best == null || string.CompareOrdinal(sig, best) < 0)
                {
                    best = sig;
                }
            }
        }

        return best ?? string.Empty;
    }

    private static void GetTransformedSize(int w, int h, int rot, out int tw, out int th)
    {
        if (rot % 2 == 0)
        {
            tw = w;
            th = h;
        }
        else
        {
            tw = h;
            th = w;
        }
    }

    private static Vector2Int TransformPoint(Vector2Int p, int w, int h, bool flipX, int rot)
    {
        int x = flipX ? (w - 1 - p.x) : p.x;
        int y = p.y;
        int curW = w;
        int curH = h;

        for (int r = 0; r < rot; r++)
        {
            int nx = y;
            int ny = curW - 1 - x;
            x = nx;
            y = ny;
            int tmp = curW;
            curW = curH;
            curH = tmp;
        }

        return new Vector2Int(x, y);
    }

    // ──────────────────────────────────────────────
    //  WALL & START GENERATION HELPERS
    // ──────────────────────────────────────────────

    private static List<Vector2Int> GenerateWalls(
        int w,
        int h,
        int outlineCount,
        int floatingCount,
        System.Random rng,
        out int actualOutline,
        out int actualFloating)
    {
        actualOutline = 0;
        actualFloating = 0;
        HashSet<Vector2Int> outlineSet = new HashSet<Vector2Int>();

        // Gather border cells (corners weighted first for nice board silhouettes)
        List<Vector2Int> borderCells = new List<Vector2Int>();
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (x == 0 || x == w - 1 || y == 0 || y == h - 1)
                    borderCells.Add(new Vector2Int(x, y));
            }
        }

        Shuffle(borderCells, rng);

        // Grow outline walls organically from the border
        int safety = 0;
        while (outlineSet.Count < outlineCount && safety < 200)
        {
            safety++;
            if (outlineSet.Count == 0 || rng.NextDouble() < 0.55)
            {
                if (borderCells.Count == 0) break;
                Vector2Int pick = borderCells[borderCells.Count - 1];
                borderCells.RemoveAt(borderCells.Count - 1);
                outlineSet.Add(pick);
            }
            else
            {
                // Expand from an existing outline wall along border or 1 step inward
                Vector2Int anchor = outlineSet.ElementAt(rng.Next(outlineSet.Count));
                Vector2Int dir = Dirs[rng.Next(4)];
                Vector2Int cand = anchor + dir;
                if (cand.x >= 0 && cand.x < w && cand.y >= 0 && cand.y < h)
                {
                    bool onBorder = cand.x == 0 || cand.x == w - 1 || cand.y == 0 || cand.y == h - 1;
                    if (onBorder || rng.NextDouble() < 0.25)
                    {
                        outlineSet.Add(cand);
                    }
                }
            }
        }

        // Gather valid interior candidates for independent/floating cross walls:
        // Must NOT lie on border and must NOT orthogonally touch any outline wall
        List<Vector2Int> floatingCandidates = new List<Vector2Int>();
        for (int x = 1; x < w - 1; x++)
        {
            for (int y = 1; y < h - 1; y++)
            {
                Vector2Int p = new Vector2Int(x, y);
                if (outlineSet.Contains(p)) continue;

                bool touchesOutline = false;
                for (int d = 0; d < 4; d++)
                {
                    if (outlineSet.Contains(p + Dirs[d]))
                    {
                        touchesOutline = true;
                        break;
                    }
                }

                if (!touchesOutline)
                {
                    floatingCandidates.Add(p);
                }
            }
        }

        if (floatingCount > floatingCandidates.Count)
            return null;

        Shuffle(floatingCandidates, rng);
        HashSet<Vector2Int> floatingSet = new HashSet<Vector2Int>();

        for (int i = 0; i < floatingCount; i++)
        {
            // Sometimes attach a floating wall next to another floating wall to form an island group
            if (floatingSet.Count > 0 && rng.NextDouble() < 0.45)
            {
                Vector2Int anchor = floatingSet.ElementAt(rng.Next(floatingSet.Count));
                Vector2Int neighbor = anchor + Dirs[rng.Next(4)];
                if (floatingCandidates.Contains(neighbor) && floatingSet.Add(neighbor))
                    continue;
            }

            foreach (Vector2Int c in floatingCandidates)
            {
                if (floatingSet.Add(c)) break;
            }
        }

        actualOutline = outlineSet.Count;
        actualFloating = floatingSet.Count;

        List<Vector2Int> allWalls = new List<Vector2Int>(outlineSet);
        allWalls.AddRange(floatingSet);
        return allWalls;
    }

    private static Vector2Int[] PickStartPositions(int w, int h, bool[,] walls, List<Vector2Int> playable, StartPattern pattern, System.Random rng)
    {
        if (pattern == StartPattern.Line)
        {
            // Try a clean 4-cell horizontal or vertical line
            List<Vector2Int[]> lines = new List<Vector2Int[]>();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x <= w - 4; x++)
                {
                    if (!walls[x, y] && !walls[x + 1, y] && !walls[x + 2, y] && !walls[x + 3, y])
                        lines.Add(new[] { new Vector2Int(x, y), new Vector2Int(x + 1, y), new Vector2Int(x + 2, y), new Vector2Int(x + 3, y) });
                }
            }
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y <= h - 4; y++)
                {
                    if (!walls[x, y] && !walls[x, y + 1] && !walls[x, y + 2] && !walls[x, y + 3])
                        lines.Add(new[] { new Vector2Int(x, y), new Vector2Int(x, y + 1), new Vector2Int(x, y + 2), new Vector2Int(x, y + 3) });
                }
            }
            if (lines.Count > 0) return lines[rng.Next(lines.Count)];
        }
        else if (pattern == StartPattern.Cluster2x2)
        {
            List<Vector2Int[]> quads = new List<Vector2Int[]>();
            for (int x = 0; x < w - 1; x++)
            {
                for (int y = 0; y < h - 1; y++)
                {
                    if (!walls[x, y] && !walls[x + 1, y] && !walls[x, y + 1] && !walls[x + 1, y + 1])
                        quads.Add(new[] { new Vector2Int(x, y), new Vector2Int(x + 1, y), new Vector2Int(x, y + 1), new Vector2Int(x + 1, y + 1) });
                }
            }
            if (quads.Count > 0) return quads[rng.Next(quads.Count)];
        }
        else if (pattern == StartPattern.Corners)
        {
            // Pick one playable cell from each quadrant
            int midX = w / 2;
            int midY = h / 2;
            var q1 = playable.Where(p => p.x < midX && p.y >= midY).ToList();
            var q2 = playable.Where(p => p.x >= midX && p.y >= midY).ToList();
            var q3 = playable.Where(p => p.x < midX && p.y < midY).ToList();
            var q4 = playable.Where(p => p.x >= midX && p.y < midY).ToList();
            if (q1.Count > 0 && q2.Count > 0 && q3.Count > 0 && q4.Count > 0)
            {
                return new[]
                {
                    q1[rng.Next(q1.Count)],
                    q2[rng.Next(q2.Count)],
                    q3[rng.Next(q3.Count)],
                    q4[rng.Next(q4.Count)]
                };
            }
        }

        // Fallback / Random: 4 distinct playable cells
        if (playable.Count < 4) return null;
        List<Vector2Int> copy = new List<Vector2Int>(playable);
        Shuffle(copy, rng);
        return new[] { copy[0], copy[1], copy[2], copy[3] };
    }

    private static int ScoreTargetState(Vector2Int[] targets, Vector2Int[] starts, int w, int h, int depth, int minMoves, int maxMoves)
    {
        int score = 0;

        // Strong preference for zero overlap between starts and targets
        int overlap = 0;
        for (int i = 0; i < 4; i++)
        {
            if (Array.Exists(starts, s => s == targets[i])) overlap++;
        }
        if (overlap == 0) score += 16;
        else score -= overlap * 8;

        // Clean visual pattern bonus (all in a row/col, or 2x2 square)
        int distinctX = targets.Select(p => p.x).Distinct().Count();
        int distinctY = targets.Select(p => p.y).Distinct().Count();
        if (distinctX == 1 || distinctY == 1) score += 14;
        else if (distinctX == 2 && distinctY == 2) score += 12;

        // Bonus for targets in the interior (shows clever stopping against floating walls/blocks)
        int interiorCount = targets.Count(p => p.x > 0 && p.x < w - 1 && p.y > 0 && p.y < h - 1);
        score += interiorCount * 3;

        // Prefer depth near the upper-middle of [minMoves, maxMoves]
        int idealDepth = Mathf.RoundToInt(Mathf.Lerp(minMoves, maxMoves, 0.6f));
        score += 10 - Mathf.Abs(depth - idealDepth);

        return score;
    }

    // ──────────────────────────────────────────────
    //  INTERNAL UTILITIES
    // ──────────────────────────────────────────────

    private class StateNode
    {
        public string PrevKey;
        public string MoveName;
        public Vector2Int[] Positions;
        public int Depth;
    }

    private static bool[,] BuildWallGrid(int w, int h, IEnumerable<Vector2Int> wallPositions)
    {
        bool[,] walls = new bool[w, h];
        if (wallPositions != null)
        {
            foreach (Vector2Int p in wallPositions)
            {
                if (p.x >= 0 && p.x < w && p.y >= 0 && p.y < h)
                    walls[p.x, p.y] = true;
            }
        }
        return walls;
    }

    private static List<Vector2Int> GetPlayableCells(int w, int h, bool[,] walls)
    {
        List<Vector2Int> list = new List<Vector2Int>();
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (!walls[x, y]) list.Add(new Vector2Int(x, y));
            }
        }
        return list;
    }

    private static bool IsPlayableAreaConnected(int w, int h, bool[,] walls, List<Vector2Int> playable)
    {
        if (playable.Count == 0) return false;
        HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        seen.Add(playable[0]);
        q.Enqueue(playable[0]);

        while (q.Count > 0)
        {
            Vector2Int cur = q.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                Vector2Int nxt = cur + Dirs[d];
                if (nxt.x >= 0 && nxt.x < w && nxt.y >= 0 && nxt.y < h && !walls[nxt.x, nxt.y] && seen.Add(nxt))
                {
                    q.Enqueue(nxt);
                }
            }
        }

        return seen.Count == playable.Count;
    }

    private static string EncodeState(Vector2Int[] blocks, bool exactOrder)
    {
        if (exactOrder)
        {
            return $"{blocks[0].x},{blocks[0].y};{blocks[1].x},{blocks[1].y};{blocks[2].x},{blocks[2].y};{blocks[3].x},{blocks[3].y}";
        }

        // Canonical sorted encoding when any block can occupy any target
        int[] keys = new int[4];
        for (int i = 0; i < 4; i++) keys[i] = blocks[i].y * 64 + blocks[i].x;
        Array.Sort(keys);
        return $"{keys[0]};{keys[1]};{keys[2]};{keys[3]}";
    }

    private static void BuildPathResult(Dictionary<string, StateNode> visited, string endKey, SolveResult result)
    {
        List<string> moves = new List<string>();
        List<Vector2Int[]> states = new List<Vector2Int[]>();

        string k = endKey;
        while (k != null)
        {
            StateNode n = visited[k];
            states.Add(n.Positions);
            if (n.MoveName != null) moves.Add(n.MoveName);
            k = n.PrevKey;
        }

        moves.Reverse();
        states.Reverse();
        result.Moves = moves;
        result.StepStates = states;
    }

    private static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }
}

/// <summary>
/// Unity Editor window for generating, previewing, stepping through, and saving
/// mathematically verified solvable 4 Blocks levels.
/// Open via: Tools -> 4Blocks -> Level Generator
/// </summary>
public class LevelGeneratorWindow : EditorWindow
{
    private const string LevelsFolder = "Assets/Scripts/Data/Levels";
    private const string RegistryPath = "Assets/Scripts/Data/Levels/MainLevelRegistry.asset";

    private static readonly Color[] BlockColors =
    {
        new Color(0.10f, 0.85f, 0.32f, 1f), // Green
        new Color(0.95f, 0.24f, 0.22f, 1f), // Red
        new Color(0.16f, 0.54f, 0.98f, 1f), // Blue
        new Color(0.98f, 0.86f, 0.14f, 1f)  // Yellow
    };

    // Generator inputs
    private int gridWidth = 6;
    private int gridHeight = 6;
    private int minOutlineWalls = 2;
    private int maxOutlineWalls = 8;
    private int minFloatingWalls = 1;
    private int maxFloatingWalls = 4;
    private int minMoves = 6;
    private int maxMoves = 10;
    private bool requireExactTargetMatch = false;
    private LevelPuzzleSolver.StartPattern startPattern = LevelPuzzleSolver.StartPattern.Auto;
    private bool autoAddToRegistry = true;

    // Preview & anti-repeat state
    private LevelPuzzleSolver.GeneratedLevel currentGenerated;
    private int previewStepIndex;
    private readonly HashSet<string> sessionGeneratedSignatures = new HashSet<string>();
    private string previousWallSignature;
    private string statusMessage = "Configure inputs above and click 'Generate Solvable Level'.";
    private Vector2 scrollPos;

    [MenuItem("Tools/4Blocks/Level Generator")]
    public static void OpenWindow()
    {
        LevelGeneratorWindow win = GetWindow<LevelGeneratorWindow>("4Blocks Level Generator");
        win.minSize = new Vector2(360f, 580f);
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        EditorGUILayout.LabelField("Procedural Level Generator (BFS Verified)", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        DrawInputControls();

        EditorGUILayout.Space(8);

        if (GUILayout.Button("Generate Solvable Level", GUILayout.Height(32f)))
        {
            RunGeneration();
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(statusMessage, currentGenerated != null ? MessageType.Info : MessageType.None);

        if (currentGenerated != null)
        {
            EditorGUILayout.Space(6);
            DrawPreviewMiniMap();
            EditorGUILayout.Space(8);
            DrawStepControls();
            EditorGUILayout.Space(10);
            DrawSaveControls();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawInputControls()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        gridWidth = EditorGUILayout.IntSlider("Grid Width", gridWidth, 4, 8);
        gridHeight = EditorGUILayout.IntSlider("Grid Height", gridHeight, 4, 8);

        EditorGUILayout.Space(4);

        int maxOutlineCap = Mathf.Max(0, (gridWidth * 2 + gridHeight * 2 - 4) / 2);
        minOutlineWalls = EditorGUILayout.IntSlider("Min Outline Cutout Walls (■)", Mathf.Clamp(minOutlineWalls, 0, maxOutlineCap), 0, maxOutlineCap);
        maxOutlineWalls = EditorGUILayout.IntSlider("Max Outline Cutout Walls (■)", Mathf.Clamp(Mathf.Max(minOutlineWalls, maxOutlineWalls), minOutlineWalls, maxOutlineCap), minOutlineWalls, maxOutlineCap);

        EditorGUILayout.Space(2);

        int maxFloatingCap = Mathf.Max(0, (gridWidth - 2) * (gridHeight - 2) / 3);
        minFloatingWalls = EditorGUILayout.IntSlider("Min Floating Cross Walls (✕)", Mathf.Clamp(minFloatingWalls, 0, maxFloatingCap), 0, maxFloatingCap);
        maxFloatingWalls = EditorGUILayout.IntSlider("Max Floating Cross Walls (✕)", Mathf.Clamp(Mathf.Max(minFloatingWalls, maxFloatingWalls), minFloatingWalls, maxFloatingCap), minFloatingWalls, maxFloatingCap);

        EditorGUILayout.Space(4);

        minMoves = EditorGUILayout.IntSlider("Min Optimal Moves", minMoves, 3, 14);
        maxMoves = EditorGUILayout.IntSlider("Max Optimal Moves", Mathf.Max(minMoves, maxMoves), minMoves, 18);

        startPattern = (LevelPuzzleSolver.StartPattern)EditorGUILayout.EnumPopup("Start Layout Style", startPattern);
        requireExactTargetMatch = EditorGUILayout.Toggle("Require Exact Color Match", requireExactTargetMatch);

        EditorGUILayout.EndVertical();
    }

    private void RunGeneration()
    {
        HashSet<string> excludedSignatures = CollectExistingLayoutSignatures(out int savedLevelCount);
        foreach (string s in sessionGeneratedSignatures)
        {
            excludedSignatures.Add(s);
        }

        LevelPuzzleSolver.GeneratedLevel generated = LevelPuzzleSolver.GenerateLevel(
            gridWidth,
            gridHeight,
            minOutlineWalls,
            maxOutlineWalls,
            minFloatingWalls,
            maxFloatingWalls,
            minMoves,
            maxMoves,
            requireExactTargetMatch,
            startPattern,
            excludedSignatures,
            previousWallSignature
        );

        currentGenerated = generated;
        previewStepIndex = 0;

        if (currentGenerated != null)
        {
            if (!string.IsNullOrEmpty(currentGenerated.LayoutSignature))
            {
                sessionGeneratedSignatures.Add(currentGenerated.LayoutSignature);
            }
            previousWallSignature = currentGenerated.WallSignature;

            string movesText = string.Join(" → ", currentGenerated.Solution.Moves);
            statusMessage =
                $"Unique Level Generated! Optimal: {currentGenerated.Solution.Moves.Count} moves\n" +
                $"Walls: {currentGenerated.OutlineWallCount} outline (■), {currentGenerated.FloatingWallCount} floating (✕)\n" +
                $"Verified unique vs {savedLevelCount} saved level(s) & previous previews (incl. rotations/flips).\n" +
                $"Solution: {movesText}";
        }
        else
        {
            statusMessage =
                "No unique solvable level matched those exact constraints in 160 attempts. " +
                "Try widening the wall Min-Max window or move range.";
        }
    }

    private HashSet<string> CollectExistingLayoutSignatures(out int savedLevelCount)
    {
        HashSet<string> signatures = new HashSet<string>();
        HashSet<LevelData> uniqueAssets = new HashSet<LevelData>();

        if (AssetDatabase.IsValidFolder(LevelsFolder))
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelData", new[] { LevelsFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                LevelData lvl = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (lvl != null) uniqueAssets.Add(lvl);
            }
        }

        LevelRegistry registry = AssetDatabase.LoadAssetAtPath<LevelRegistry>(RegistryPath);
        if (registry != null && registry.levels != null)
        {
            foreach (LevelData lvl in registry.levels)
            {
                if (lvl != null) uniqueAssets.Add(lvl);
            }
        }

        foreach (LevelData lvl in uniqueAssets)
        {
            string sig = LevelPuzzleSolver.ComputeCanonicalLayoutSignature(lvl);
            if (!string.IsNullOrEmpty(sig))
            {
                signatures.Add(sig);
            }
        }

        savedLevelCount = uniqueAssets.Count;
        return signatures;
    }

    private void DrawPreviewMiniMap()
    {
        int w = currentGenerated.GridWidth;
        int h = currentGenerated.GridHeight;
        float cellSize = Mathf.Clamp(Mathf.Floor((position.width - 56f) / w), 24f, 40f);

        // Compute independent walls using the exact same LevelManager rule
        LevelData tempData = ScriptableObject.CreateInstance<LevelData>();
        tempData.gridWidth = w;
        tempData.gridHeight = h;
        tempData.wallPositions = currentGenerated.WallPositions;
        HashSet<Vector2Int> independentWalls = LevelManager.GetIndependentWallPositions(tempData);
        DestroyImmediate(tempData);

        Vector2Int[] activeBlocks = currentGenerated.Solution.StepStates[
            Mathf.Clamp(previewStepIndex, 0, currentGenerated.Solution.StepStates.Count - 1)
        ];

        Rect gridRect = GUILayoutUtility.GetRect(w * cellSize, h * cellSize, GUILayout.ExpandWidth(false));
        EditorGUI.DrawRect(gridRect, new Color(0.09f, 0.12f, 0.15f, 1f));

        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Clamp(Mathf.RoundToInt(cellSize * 0.42f), 10, 14)
        };

        for (int y = h - 1; y >= 0; y--)
        {
            for (int x = 0; x < w; x++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                Rect cellRect = new Rect(
                    gridRect.x + x * cellSize + 1f,
                    gridRect.y + (h - 1 - y) * cellSize + 1f,
                    cellSize - 2f,
                    cellSize - 2f
                );

                bool isWall = Array.Exists(currentGenerated.WallPositions, p => p == pos);
                bool isIndependent = isWall && independentWalls.Contains(pos);
                int targetIdx = Array.FindIndex(currentGenerated.BlockTargetPositions, p => p == pos);
                int blockIdx = Array.FindIndex(activeBlocks, p => p == pos);

                Color fill = new Color(0.18f, 0.22f, 0.26f, 1f);
                string text = "·";
                Color textColor = new Color(1f, 1f, 1f, 0.25f);

                if (isIndependent)
                {
                    fill = new Color(0.14f, 0.17f, 0.21f, 1f);
                    text = "✕";
                    textColor = Color.white;
                }
                else if (isWall)
                {
                    fill = new Color(0.42f, 0.44f, 0.48f, 1f);
                    text = "■";
                    textColor = new Color(0.12f, 0.13f, 0.15f, 1f);
                }

                if (blockIdx >= 0 && targetIdx >= 0)
                {
                    EditorGUI.DrawRect(cellRect, new Color(0.95f, 0.78f, 0.14f, 1f));
                    Rect inner = new Rect(cellRect.x + 2.5f, cellRect.y + 2.5f, cellRect.width - 5f, cellRect.height - 5f);
                    EditorGUI.DrawRect(inner, BlockColors[blockIdx % 4]);
                    text = $"{blockIdx + 1}★";
                    textColor = blockIdx == 3 ? Color.black : Color.white;
                }
                else if (blockIdx >= 0)
                {
                    EditorGUI.DrawRect(cellRect, BlockColors[blockIdx % 4]);
                    text = (blockIdx + 1).ToString();
                    textColor = blockIdx == 3 ? Color.black : Color.white;
                }
                else if (targetIdx >= 0)
                {
                    Color gold = new Color(0.95f, 0.78f, 0.14f, 1f);
                    fill = currentGenerated.RequireExactTargetMatch
                        ? Color.Lerp(BlockColors[targetIdx % 4], gold, 0.35f)
                        : gold;
                    EditorGUI.DrawRect(cellRect, fill);
                    text = $"★{targetIdx + 1}";
                    textColor = (currentGenerated.RequireExactTargetMatch && targetIdx != 3)
                        ? Color.white
                        : new Color(0.20f, 0.14f, 0f, 1f);
                }
                else
                {
                    EditorGUI.DrawRect(cellRect, fill);
                }

                labelStyle.normal.textColor = textColor;
                GUI.Label(cellRect, text, labelStyle);
            }
        }
    }

    private void DrawStepControls()
    {
        int totalMoves = currentGenerated.Solution.Moves.Count;
        string stepDesc = previewStepIndex == 0
            ? $"Step 0 / {totalMoves} (Start)"
            : $"Step {previewStepIndex} / {totalMoves} (After: {currentGenerated.Solution.Moves[previewStepIndex - 1]})";

        EditorGUILayout.LabelField(stepDesc, EditorStyles.miniBoldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("⟲ Reset")) previewStepIndex = 0;
        GUI.enabled = previewStepIndex > 0;
        if (GUILayout.Button("◀ Prev")) previewStepIndex--;
        GUI.enabled = previewStepIndex < totalMoves;
        if (GUILayout.Button("Next ▶")) previewStepIndex++;
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSaveControls()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        autoAddToRegistry = EditorGUILayout.Toggle("Add to MainLevelRegistry", autoAddToRegistry);

        if (GUILayout.Button("Save as Next Level Asset", GUILayout.Height(28f)))
        {
            SaveGeneratedLevel();
        }
        EditorGUILayout.EndVertical();
    }

    private void SaveGeneratedLevel()
    {
        if (!AssetDatabase.IsValidFolder(LevelsFolder))
        {
            Debug.LogError($"Levels folder not found: {LevelsFolder}");
            return;
        }

        // Prevent accidentally saving the same generated layout twice (or duplicating an existing level)
        HashSet<string> existing = CollectExistingLayoutSignatures(out _);
        if (!string.IsNullOrEmpty(currentGenerated.LayoutSignature) && existing.Contains(currentGenerated.LayoutSignature))
        {
            statusMessage = "This layout is already saved! Click 'Generate Solvable Level' to create a fresh unique level.";
            return;
        }

        int nextNumber = FindNextLevelNumber();
        string assetName = $"Level_{nextNumber:00}";
        string assetPath = $"{LevelsFolder}/{assetName}.asset";

        LevelData asset = ScriptableObject.CreateInstance<LevelData>();
        asset.levelName = $"Level {nextNumber}";
        asset.requireExactTargetMatch = currentGenerated.RequireExactTargetMatch;
        asset.gridWidth = currentGenerated.GridWidth;
        asset.gridHeight = currentGenerated.GridHeight;
        asset.blockStartPositions = (Vector2Int[])currentGenerated.BlockStartPositions.Clone();
        asset.blockTargetPositions = (Vector2Int[])currentGenerated.BlockTargetPositions.Clone();
        asset.wallPositions = (Vector2Int[])currentGenerated.WallPositions.Clone();

        AssetDatabase.CreateAsset(asset, assetPath);

        if (autoAddToRegistry)
        {
            LevelRegistry registry = AssetDatabase.LoadAssetAtPath<LevelRegistry>(RegistryPath);
            if (registry != null)
            {
                Undo.RecordObject(registry, "Register Generated Level");
                List<LevelData> list = registry.levels != null
                    ? new List<LevelData>(registry.levels)
                    : new List<LevelData>();
                list.Add(asset);
                registry.levels = list.ToArray();
                EditorUtility.SetDirty(registry);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);

        statusMessage = $"Saved {assetPath} ({currentGenerated.Solution.Moves.Count} moves optimal)!";
    }

    private static int FindNextLevelNumber()
    {
        int maxNum = 0;
        string[] files = Directory.GetFiles(LevelsFolder, "Level_*.asset");
        foreach (string f in files)
        {
            string name = Path.GetFileNameWithoutExtension(f);
            string suffix = name.Substring("Level_".Length);
            if (int.TryParse(suffix, out int num) && num > maxNum)
            {
                maxNum = num;
            }
        }
        return maxNum + 1;
    }
}

