namespace Cove.Server.Utils
{
    internal static class WorldFile
    {
        /// <summary>
        /// Reads point positions from a .tscn file based on the specified node group.
        /// </summary>
        /// <param name="nodeGroup">The group name to search for in the file.</param>
        /// <param name="file">The content of the .tscn file as a string.</param>
        /// <param name="logger">An optional logger instance.</param>
        /// <returns>A list of Vector3 points found in the specified group.</returns>
        public static List<Vector3> ReadPoints(
            string nodeGroup,
            string file,
            ILogger? logger = null
        )
        {
            var points = new List<Vector3>();
            var lines = file.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var groupPattern = @"groups=\[([^\]]*)\]";
            var transformPattern =
                @"Transform\(.*?,\s*(-?\d+\.?\d*),\s*(-?\d+\.?\d*),\s*(-?\d+\.?\d*)\s*\)";

            for (int i = 0; i < lines.Length; i++)
            {
                var groupMatch = Regex.Match(lines[i], groupPattern);
                if (groupMatch.Success && groupMatch.Groups[1].Value.Contains($"\"{nodeGroup}\""))
                {
                    if (i + 1 < lines.Length) // Ensure there's a next line to check
                    {
                        var transformMatch = Regex.Match(lines[i + 1], transformPattern);
                        if (transformMatch.Success)
                        {
                            if (
                                float.TryParse(transformMatch.Groups[1].Value, out var x)
                                && float.TryParse(transformMatch.Groups[2].Value, out var y)
                                && float.TryParse(transformMatch.Groups[3].Value, out var z)
                            )
                            {
                                points.Add(new Vector3(x, y, z));
                            }
                            else
                            {
                                logger?.LogWarning(
                                    "Invalid transform values at line {LineNumber}.",
                                    i + 1
                                );
                            }
                        }
                        else
                        {
                            logger?.LogWarning(
                                "Transform data not found for group \"{NodeGroup}\" at line {LineNumber}.",
                                nodeGroup,
                                i + 1
                            );
                        }
                    }
                }
            }

            logger?.LogInformation(
                "Found {PointCount} points in group \"{NodeGroup}\".",
                points.Count,
                nodeGroup
            );
            return points;
        }
    }
}
