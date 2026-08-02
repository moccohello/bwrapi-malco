using System.Collections.Generic;
using Malco.Models;

namespace Malco.Data
{
    internal sealed class GasWorkerProjection
    {
        public GasWorkerProjection(
            List<GasWorkerGroup> groups,
            GasWorkerAssignmentSnapshot assignments)
        {
            Groups = groups;
            Assignments = assignments;
        }

        public List<GasWorkerGroup> Groups { get; }

        public GasWorkerAssignmentSnapshot Assignments { get; }
    }
}
