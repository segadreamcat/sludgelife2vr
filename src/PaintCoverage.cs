using System;
using System.Collections.Generic;

namespace SludgeLife2VR {
    // CPU coverage rules are separate from Unity rendering so they can be checked without a game launch.
    internal sealed class PaintCoverage {
        readonly int width, height, radius;
        readonly byte[] alpha;
        readonly bool[] painted;
        readonly int important;
        int covered;
        float time, extent;
        internal int Covered { get { return covered; } }
        internal float Coverage { get { return important == 0 ? 0 : (float)covered / important; } }
        internal bool Completed;
        internal PaintCoverage(int width, int height, byte[] alpha) {
            if (width <= 0 || height <= 0 || alpha == null || alpha.Length != width * height) throw new ArgumentException("Invalid paint mask.");
            this.width = width; this.height = height; this.alpha = alpha;
            painted = new bool[alpha.Length]; radius = Math.Max(2, (int)Math.Round(Math.Min(width, height) * 0.085));
            for (int i = 0; i < alpha.Length; i++) if (alpha[i] > 25) important++;
        }
        internal List<int> Stamp(float u, float v, float delta, float distanceFromStart, float worldScale) {
            List<int> changed = new List<int>();
            if (Completed) return changed;
            time += Math.Min(0.05f, Math.Max(0, delta)); extent = Math.Max(extent, distanceFromStart);
            int cx = (int)Math.Round(Math.Max(0, Math.Min(1, u)) * (width - 1));
            int cy = (int)Math.Round(Math.Max(0, Math.Min(1, v)) * (height - 1));
            for (int y = Math.Max(0, cy - radius); y <= Math.Min(height - 1, cy + radius); y++)
                for (int x = Math.Max(0, cx - radius); x <= Math.Min(width - 1, cx + radius); x++) {
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > radius * radius) continue;
                    int index = y * width + x;
                    if (painted[index] || alpha[index] <= 25) continue;
                    painted[index] = true; covered++; changed.Add(index);
                }
            Completed = important > 0 && Coverage >= 0.65f && time >= 1.25f && extent >= 0.12f * worldScale;
            return changed;
        }
    }
}
