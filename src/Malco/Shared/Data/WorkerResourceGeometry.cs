using System;
using System.Collections.Generic;
using System.Linq;

namespace Malco.Data
{
    internal static class WorkerResourceGeometry
    {
        private const long BaseMineralRadiusSquared = 384L * 384L;
        private const long MineralClusterLinkRadiusSquared = 256L * 256L;

        public static List<MineralCluster> BuildMineralClusters(
            IList<Tuple<int, int>> mineralFields)
        {
            var fields = (mineralFields ?? new List<Tuple<int, int>>())
                .Where(IsNonZeroPosition)
                .OrderBy(field => field.Item1)
                .ThenBy(field => field.Item2)
                .ToList();
            var clusters = new List<MineralCluster>();
            var assigned = new bool[fields.Count];
            for (var seed = 0; seed < fields.Count; seed++)
            {
                if (assigned[seed])
                {
                    continue;
                }

                var members = new List<Tuple<int, int>>();
                var pending = new Queue<int>();
                assigned[seed] = true;
                pending.Enqueue(seed);
                while (pending.Count > 0)
                {
                    int current = pending.Dequeue();
                    Tuple<int, int> field = fields[current];
                    members.Add(field);
                    for (var candidate = 0; candidate < fields.Count; candidate++)
                    {
                        if (assigned[candidate] ||
                            DistanceSquared(
                                field.Item1,
                                field.Item2,
                                fields[candidate].Item1,
                                fields[candidate].Item2) >
                            MineralClusterLinkRadiusSquared)
                        {
                            continue;
                        }

                        assigned[candidate] = true;
                        pending.Enqueue(candidate);
                    }
                }

                clusters.Add(new MineralCluster
                {
                    Fields = members,
                    CenterX = (int)Math.Round(members.Average(field => field.Item1)),
                    CenterY = (int)Math.Round(members.Average(field => field.Item2))
                });
            }

            return clusters;
        }

        public static List<MineralBaseCandidate> SelectMineralClusterBases(
            IList<MineralBaseCandidate> bases,
            IList<MineralCluster> clusters)
        {
            var basesByCluster =
                new Dictionary<MineralCluster, List<MineralBaseCandidate>>();
            foreach (var baseCandidate in
                     (bases ?? new List<MineralBaseCandidate>())
                     .Where(candidate => candidate != null))
            {
                MineralCluster nearestCluster = null;
                long nearestCenterDistance = long.MaxValue;
                foreach (var cluster in
                         (clusters ?? new List<MineralCluster>())
                         .Where(cluster => cluster != null))
                {
                    long nearestPatchDistance = NearestDistanceSquared(
                        Tuple.Create(baseCandidate.MapX, baseCandidate.MapY),
                        cluster.Fields);
                    if (nearestPatchDistance > BaseMineralRadiusSquared)
                    {
                        continue;
                    }

                    long centerDistance = DistanceSquared(
                        baseCandidate.MapX,
                        baseCandidate.MapY,
                        cluster.CenterX,
                        cluster.CenterY);
                    if (centerDistance < nearestCenterDistance)
                    {
                        nearestCluster = cluster;
                        nearestCenterDistance = centerDistance;
                    }
                }

                if (nearestCluster == null)
                {
                    continue;
                }

                List<MineralBaseCandidate> candidates;
                if (!basesByCluster.TryGetValue(nearestCluster, out candidates))
                {
                    candidates = new List<MineralBaseCandidate>();
                    basesByCluster[nearestCluster] = candidates;
                }
                candidates.Add(baseCandidate);
            }

            return basesByCluster
                .Select(pair =>
                {
                    MineralBaseCandidate selected = pair.Value
                        .OrderBy(candidate => DistanceSquared(
                            candidate.MapX,
                            candidate.MapY,
                            pair.Key.CenterX,
                            pair.Key.CenterY))
                        .ThenBy(candidate => candidate.Order)
                        .First();
                    selected.MineralPatchCount = pair.Key.Fields.Count;
                    return selected;
                })
                .OrderBy(candidate => candidate.Order)
                .ToList();
        }

        public static Tuple<int, int> SnapToNearest(
            Tuple<int, int> position,
            IList<Tuple<int, int>> targets,
            long maxDistanceSquared)
        {
            if (!IsNonZeroPosition(position) ||
                targets == null ||
                targets.Count == 0)
            {
                return position;
            }

            Tuple<int, int> nearest = null;
            var nearestDistance = long.MaxValue;
            foreach (var target in targets.Where(IsNonZeroPosition))
            {
                var distance = DistanceSquared(
                    position.Item1,
                    position.Item2,
                    target.Item1,
                    target.Item2);
                if (distance < nearestDistance)
                {
                    nearest = target;
                    nearestDistance = distance;
                }
            }

            return nearest != null && nearestDistance <= maxDistanceSquared
                ? nearest
                : position;
        }

        public static long DistanceSquared(
            int x1,
            int y1,
            int x2,
            int y2)
        {
            var dx = x1 - x2;
            var dy = y1 - y2;
            return ((long)dx * dx) + ((long)dy * dy);
        }

        public static long NearestDistanceSquared(
            Tuple<int, int> position,
            IEnumerable<Tuple<int, int>> candidates)
        {
            if (!IsNonZeroPosition(position))
            {
                return long.MaxValue;
            }

            var nearest = long.MaxValue;
            foreach (var candidate in
                     (candidates ?? Enumerable.Empty<Tuple<int, int>>())
                     .Where(IsNonZeroPosition))
            {
                nearest = Math.Min(
                    nearest,
                    DistanceSquared(
                        position.Item1,
                        position.Item2,
                        candidate.Item1,
                        candidate.Item2));
            }

            return nearest;
        }

        public static bool IsNonZeroPosition(Tuple<int, int> position)
        {
            return position != null &&
                   (position.Item1 != 0 || position.Item2 != 0);
        }

        public static string BuildPositionKey(Tuple<int, int> position)
        {
            return position.Item1.ToString(
                       System.Globalization.CultureInfo.InvariantCulture) +
                   ":" +
                   position.Item2.ToString(
                       System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
