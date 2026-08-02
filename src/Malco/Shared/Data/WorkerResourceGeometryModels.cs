using System;
using System.Collections.Generic;
using Malco.Models;

namespace Malco.Data
{
    internal sealed class MineralBaseCandidate
    {
        public string Key { get; set; }
        public StableIdentity ResourceIdentity { get; set; }
        public int UnitId { get; set; }
        public int MapX { get; set; }
        public int MapY { get; set; }
        public int Order { get; set; }
        public int MineralPatchCount { get; set; }
    }

    internal sealed class MineralCluster
    {
        public List<Tuple<int, int>> Fields { get; set; }
        public int CenterX { get; set; }
        public int CenterY { get; set; }
    }
}
