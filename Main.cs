using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Array = System.Array;

public partial class Main : Node2D
{
    public static Main Instance;

    
    [Export] private PackedScene DotScene;

    private Node PolygonsContainer;
    private Polygon2D CarvingCursor;

    private Vector2 lastCarvePosition = Vector2.Inf;
    private List<Vector2[]> polygons = new();

    public static void AddDot(Vector2 position)
    {
        Node2D dot = Instance.DotScene.Instantiate<Node2D>();
        dot.Position = position;
        Callable.From(() => Instance.PolygonsContainer.AddChild(dot)).CallDeferred();
    }

    public override void _Ready()
    {
        Instance = this;
        base._Ready();
        PolygonsContainer = GetNode<Node>("%PolygonsContainer");
        CarvingCursor = GetNode<Polygon2D>("%CarvingCursor");
        polygons.Add(GetNode<Polygon2D>("%InitialPolygon").Polygon);
    }

    public override void _Process(double delta)
    {
        CarvingCursor.GlobalPosition = GetGlobalMousePosition();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);

        if (Input.IsActionPressed("carve"))
        {
            Vector2 mouse = GetGlobalMousePosition();

            if (mouse.DistanceTo(lastCarvePosition) < 10)
                return;

            lastCarvePosition = mouse;

            // CarvingCursor.Polygon = PolygonCarver.NaivePolygonInflate(CarvingCursor.Polygon, 10);
            
            polygons = PolygonCarver.Carve(polygons, GetCarveShape());
            
            UpdateVisualPolygons();
        }
    }

    private Vector2[] GetCarveShape()
    {
        Vector2 position = CarvingCursor.GlobalPosition;
        return CarvingCursor.Polygon.Select(vertex => vertex + position).ToArray();
    }

    private void UpdateVisualPolygons()
    {
        foreach (Node child in PolygonsContainer.GetChildren())
            child.QueueFree();

        foreach (Vector2[] polygon in polygons)
        {
            Polygon2D polygon2D = new()
            {
                Polygon = polygon,
                Color = new Color(GD.Randf(), GD.Randf(), GD.Randf()).Lerp(Colors.White, 0.5f)
            };
            
            PolygonsContainer.AddChild(polygon2D);
        }
    }
}

public abstract class PolygonCarver
{
    public static List<Vector2[]> Carve(List<Vector2[]> polygons, Vector2[] carveShape)
    {
        List<Vector2[]> result = new();

        foreach (Vector2[] polygon in polygons)
            result.AddRange(CarveSingle(polygon, carveShape));

        return result;
    }
    
    private static List<Vector2[]> CarveSingle(Vector2[] polygon, Vector2[] carveShape)
    {
        List<Vector2[]> polygons = Geometry2D.ClipPolygons(polygon, carveShape).ToList();

        switch (polygons.Count)
        {
            case 0:
            case 1:
                return polygons;
            
            default:
                if (!polygons.Any(Geometry2D.IsPolygonClockwise))
                    return polygons.ToList();
                
                GetLowestVertex(carveShape, out Vector2 lowestCarveVertex, out int lowestCarveVertexIndex);

                float polygonHeight = GetPolygonHeight(polygon);
                
                Vector2? intersection = LineToPolygonFirstIntersection(
                    lowestCarveVertex,
                    lowestCarveVertex + Vector2.Down * polygonHeight,
                    polygon
                );

                if (intersection is null)
                    throw new Exception("what?");
                
                Vector2[] cuttingLine = { (Vector2)intersection + Vector2.Down, lowestCarveVertex + Vector2.Right };
                Vector2[] cuttingCarveShape = InsertAt(carveShape, cuttingLine, lowestCarveVertexIndex + 1);
                
                return CarveSingle(polygon, cuttingCarveShape);
        }
    }

    private static Vector2? LineToPolygonFirstIntersection(Vector2 from, Vector2 to, Vector2[] polygon)
    {
        List<Vector2> intersections = LineToPolygonIntersections(from, to, polygon);

        if (!intersections.Any())
            return null;
        
        Vector2 nearest = intersections[0];
        float shortestDistance = intersections[0].DistanceTo(from);

        for (int i = 1; i < intersections.Count; i++)
        {
            Vector2 intersection = intersections[i];
            float distance = intersection.DistanceTo(from);
            
            if (distance < shortestDistance)
            {
                nearest = intersection;
                shortestDistance = distance;
            }
        }

        return nearest;
    }

    private static List<Vector2> LineToPolygonIntersections(Vector2 from, Vector2 to, Vector2[] polygon)
    {
        List<Vector2> intersections = new();
        
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 vertexA = polygon[i];
            Vector2 vertexB = polygon[(i + 1) % polygon.Length];
            Vector2? intersection = LineToLineIntersection(from, to, vertexA, vertexB);
            
            if (intersection != null)
                intersections.Add((Vector2)intersection);
        }

        return intersections;
    }
    
    private static Vector2? LineToLineIntersection(Vector2 fromA, Vector2 toA, Vector2 fromB, Vector2 toB)
    {
        Vector2 dirA = toA - fromA;
        Vector2 dirB = toB - fromB;
        
        float denominator = dirA.X * dirB.Y - dirA.Y * dirB.X;
        
        if (Mathf.Abs(denominator) < Mathf.Epsilon)
            return null;
        
        Vector2 diff = fromB - fromA;
        float t = (diff.X * dirB.Y - diff.Y * dirB.X) / denominator;
        float u = (diff.X * dirA.Y - diff.Y * dirA.X) / denominator;
        
        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
            return fromA + t * dirA;
        
        return null;
    }

    private static void GetLowestVertex(Vector2[] polygon, out Vector2 vertex, out int index)
    {
        vertex = polygon[0];
        index = 0;
        
        for (int i = 1; i < polygon.Length; i++)
        {
            Vector2 vertexB = polygon[i];
            
            if (vertex.Y >= vertexB.Y)
                continue;
            
            vertex = vertexB;
            index = i;
        }
    }

    private static float GetPolygonHeight(Vector2[] polygon)
    {
        float min = polygon[0].Y;
        float max = min;

        foreach (Vector2 vertex in polygon)
        {
            min = Math.Min(min, vertex.Y);
            max = Math.Max(max, vertex.Y);
        }

        return Math.Abs(max - min);
    }
    
    private static T[] InsertAt<T>(T[] array, T[] items, int index)
    {
        T[] newArray = new T[array.Length + items.Length];
        Array.Copy(array, 0, newArray, 0, index);
        Array.Copy(items, 0, newArray, index, items.Length);
        Array.Copy(array, index, newArray, index + items.Length, array.Length - index);
        return newArray;
    }
}
