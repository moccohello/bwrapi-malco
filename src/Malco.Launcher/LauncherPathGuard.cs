using System;
using System.IO;

namespace Malco.Launcher
{
    internal static class LauncherPathGuard
    {
        public static string Child(string parent, string name)
        {
            var root = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var child = Path.GetFullPath(Path.Combine(parent, name));
            if (!child.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("A launcher path escapes its fixed install root.");
            }
            return child;
        }

        public static void RequireOrdinaryDirectory(string path, string label)
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.Directory) == 0 ||
                (attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("The launcher " + label + " is not an ordinary directory.");
            }
        }

        public static void RequireOrdinaryFile(string path, string label)
        {
            if (!File.Exists(path))
            {
                throw new InvalidDataException("The launcher " + label + " is missing.");
            }
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.Directory) != 0 ||
                (attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("The launcher " + label + " is not an ordinary file.");
            }
        }
    }
}
