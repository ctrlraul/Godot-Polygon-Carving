using Godot;
using System;
using System.Collections.Generic;

namespace Carving;

/// <summary>
/// Ramer Douglas Peucker Method
/// </summary>
public static class SimplifyPolygon
{
    public static List<Vector2> Simplify(List<Vector2> points, float tolerance)
    {
        if (points == null || points.Count < 3)
            return points;

        return RamerDouglasPeucker(points, tolerance);
    }

    private static List<Vector2> RamerDouglasPeucker(List<Vector2> points, float tolerance)
    {
        if (points.Count < 3)
            return points;

        int first = 0;
        int last = points.Count - 1;
        List<Vector2> result = new List<Vector2>();

        // Mark the endpoints as kept
        bool[] keep = new bool[points.Count];
        keep[first] = true;
        keep[last] = true;

        // Recursive simplification
        Iterate(points, first, last, tolerance, keep);

        // Collect the points that are kept
        for (int i = 0; i < points.Count; i++)
        {
            if (keep[i])
                result.Add(points[i]);
        }

        return result;
    }

    private static void Iterate(List<Vector2> points, int first, int last, float tolerance, bool[] keep)
    {
        float maxDistance = 0;
        int index = -1;

        Vector2 start = points[first];
        Vector2 end = points[last];

        // Find the point farthest from the line segment [start, end]
        for (int i = first + 1; i < last; i++)
        {
            float distance = PerpendicularDistance(points[i], start, end);
            if (distance > maxDistance)
            {
                index = i;
                maxDistance = distance;
            }
        }

        // If the maximum distance is greater than tolerance, recursively simplify
        if (maxDistance > tolerance && index != -1)
        {
            keep[index] = true;
            Iterate(points, first, index, tolerance, keep);
            Iterate(points, index, last, tolerance, keep);
        }
    }

    private static float PerpendicularDistance(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
    {
        float dx = lineEnd.X - lineStart.X;
        float dy = lineEnd.Y - lineStart.Y;

        // Normalize the line segment
        float length = Mathf.Sqrt(dx * dx + dy * dy);
        if (Mathf.Abs(length) < Mathf.Epsilon) return 0;

        dx /= length;
        dy /= length;

        // Calculate the distance from `point` to the line
        float pvx = point.X - lineStart.X;
        float pvy = point.Y - lineStart.Y;
        float pvDot = pvx * dx + pvy * dy;
        float ax = pvDot * dx;
        float ay = pvDot * dy;

        float distX = pvx - ax;
        float distY = pvy - ay;

        return Mathf.Sqrt(distX * distX + distY * distY);
    }
}
