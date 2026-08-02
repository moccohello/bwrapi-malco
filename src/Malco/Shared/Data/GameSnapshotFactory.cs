using System;
using Malco.Models;

namespace Malco.Data
{
    internal static class GameSnapshotFactory
    {
        public static GameSnapshot NotReady(string status)
        {
            return new GameSnapshot(
                DateTime.Now,
                false,
                Race.Unknown,
                -1,
                0,
                0,
                0,
                0,
                status,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
        }
    }
}
