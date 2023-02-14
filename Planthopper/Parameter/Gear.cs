using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using Mesh = Rhino.Geometry.Mesh;

namespace Planthopper.Parameter
{
    public class Gear
    {
        public Plane Plane { get; set; }
        public double Rotation { get; private set; }
        public bool IsExternal { get; }
        public double Hole;
        public int N { get; }
        public double Width { get; set; }
        public double PAngle { get; }
        public double Addendum { get; }
        public double Dedendum { get; }
        public double PitchRadius { get; }
        public double BaseRadius { get; }
        public double AddRadius => PitchRadius + Addendum;
        public double DedRadius => PitchRadius - Dedendum;
        public Curve GearCurve { get; }
        public Gear()
        { }
        public Gear(Gear g)
        {
            Plane = new Plane(g.Plane);
            Rotation = g.Rotation;
            IsExternal = g.IsExternal;
            Hole = g.Hole;
            N = g.N;
            Width = g.Width;
            PAngle = g.PAngle;
            Addendum = g.Addendum;
            Dedendum = g.Dedendum;
            PitchRadius = g.PitchRadius;
            BaseRadius = g.BaseRadius;
            GearCurve = g.GearCurve.DuplicateCurve();
        }
        public Gear(Plane plane, double rotation, bool isExternal, double hole, int n, double width, double pAngle, double addendum, double dedendum, double pitchRadius, double baseRadius, Curve gearCurve)
        {
            Plane = new Plane(plane);
            Rotation = rotation;
            IsExternal = isExternal;
            Hole = hole;
            N = n;
            Width = width;
            PAngle = pAngle;
            Addendum = addendum;
            Dedendum = dedendum;
            PitchRadius = pitchRadius;
            BaseRadius = baseRadius;
            GearCurve = gearCurve;
        }
        public Gear(Plane plane, int n, double width, double pAngle, ref double addendum, ref double dedendum, bool isExternal, double hole, double tolerance)
        {
            Plane = plane;
            IsExternal = isExternal;
            Hole = hole;
            N = n;
            Width = width;
            PAngle = pAngle;
            PitchRadius = n * width / Math.PI;
            if (addendum < tolerance && dedendum < tolerance)
            {
                GearCurve = new Circle(PitchRadius).ToNurbsCurve();
                Addendum = 0.0;
                Dedendum = 0.0;
                BaseRadius = PitchRadius;
                return;
            }
            BaseRadius = Math.Cos(pAngle) * PitchRadius;
            var angle = Math.PI / n; //maybe we can merge this with invAngle
            var invAng = Math.Tan(pAngle) - pAngle;
            var alpha = 0.5 * angle + invAng; //MirrorLine angle alpha
            var beta = 1.0; //a carefully chosen starting value, involute domain angle
            while (Math.Abs(Math.Tan(beta) - beta - alpha) > 10e-10)
                beta -= (Math.Tan(beta) - beta - alpha) / Math.Pow(Math.Tan(beta), 2);

            var maxRadius = BaseRadius / Math.Cos(beta); //maximum addendum circle

            if (addendum > maxRadius - PitchRadius) addendum = maxRadius - PitchRadius;
            if (dedendum > maxRadius - PitchRadius) dedendum = maxRadius - PitchRadius;
            Addendum = addendum;
            Dedendum = dedendum;

            var involuteStart = new Point3d(BaseRadius, 0.0, 0.0);
            var trochoidStart = new Point3d(DedRadius, width * 0.5 - Math.Tan(pAngle) * dedendum, 0);

            var addCircle = new Circle(AddRadius);
            var dedCircle = new Circle(DedRadius);

            var involuteCurve = MoveAndRotate(involuteStart, -maxRadius, 0.0, -BaseRadius, 100);
            var x = Intersection.CurveCurve(involuteCurve, new Circle(PitchRadius).ToNurbsCurve(), tolerance, 0);
            involuteCurve.Rotate(-PointAngle(involuteCurve.PointAt(x[0].ParameterA)), Vector3d.ZAxis, Point3d.Origin);
            x = Intersection.CurveCurve(involuteCurve, addCircle.ToNurbsCurve(), tolerance, 0);

            involuteCurve = involuteCurve.Trim(x[0].ParameterA, involuteCurve.Domain.T1);

            involuteCurve.Rotate(Math.PI / 2 / n, Vector3d.ZAxis, Point3d.Origin);
            var trochoidCurve = MoveAndRotate(trochoidStart, -maxRadius / 3.0, maxRadius / 2, -PitchRadius, 100);
            x = Intersection.CurveCurve(involuteCurve, trochoidCurve, tolerance, 0);
            involuteCurve = involuteCurve.Trim(involuteCurve.Domain[0], x[0].ParameterA);
            var c = involuteCurve;
            if (dedendum > 0.0)
            {
                trochoidCurve = trochoidCurve.Trim(trochoidCurve.Domain[0], x[0].ParameterB);
                x = Intersection.CurveCurve(trochoidCurve, dedCircle.ToNurbsCurve(), tolerance, 0);
                trochoidCurve = trochoidCurve.Trim(x[0].ParameterA, trochoidCurve.Domain[0]);
                c = Curve.JoinCurves(new Curve[] { involuteCurve, trochoidCurve })[0];
            }

            var a1 = PointAngle(c.PointAtEnd); //start, end
            var arc1 = new Arc(dedCircle, new Interval(-a1, a1));
            var a2 = PointAngle(c.PointAtStart); //start, end
            var arc2 = new Arc(addCircle, new Interval(a2, 2 * angle - a2));

            var d = Curve.JoinCurves(new[] { arc1.ToNurbsCurve(), c, arc2.ToNurbsCurve() })[0];
            var curves = new List<Curve> { d };
            var e = c.DuplicateCurve();
            e.Transform(Transform.Mirror(Point3d.Origin, Vector3d.YAxis));
            curves.Add(e);
            var xForm = Transform.Rotation(Math.PI / n * 2, Point3d.Origin);
            var f = Curve.JoinCurves(curves)[0];
            curves = new List<Curve>() { f };
            for (var i = 1; i < n; i++)
            {
                var g = curves[i - 1].DuplicateCurve();
                g.Transform(xForm);
                curves.Add(g);
            }
            GearCurve = Curve.JoinCurves(curves)[0];
            xForm = Transform.PlaneToPlane(Plane.WorldXY, Plane);
            GearCurve.Transform(xForm);
        }
        private static Curve MoveAndRotate(Point3d start, double d0, double d1, double r, int n)
        {
            var pts = new Point3d[n + 1];
            var dStep = (d1 - d0) / n;
            var aStep = dStep / r;
            var a0 = d0 / r;
            for (var i = 0; i < n + 1; i++)
            {
                var sin = Math.Sin(a0);
                var cos = Math.Cos(a0);
                var x = start.X;
                var y = start.Y + d0;
                pts[i].X = x * cos - y * sin;
                pts[i].Y = y * cos + x * sin;
                d0 += dStep;
                a0 += aStep;
            }
            return Curve.CreateInterpolatedCurve(pts, 3, CurveKnotStyle.ChordSquareRoot);
        }
        private static double PointAngle(Point3d point) => Math.Atan(point.Y / point.X);
        public override string ToString() => $"{(IsExternal ? "External" : "Internal")} Gear (N={N}, W={GH_Format.FormatDouble(Width)}, P={GH_Format.FormatDouble(PAngle)})";

        public Mesh ToMesh()
        {
            var segments = GearCurve.DuplicateSegments();
            var pl = new Polyline();
            {
                if (Addendum < 0.01 && Dedendum < 0.01)
                    pl = Polyline.CreateCircumscribedPolygon(new Circle(Plane, PitchRadius), 30);
                else
                    foreach (var segment in segments)
                    {
                        pl.Add(segment.PointAtStart);
                        segment.DivideByCount(4, false, out var pts);
                        pl.AddRange(pts);
                    }
            }
            pl.Add(pl[0]);
            Mesh mesh;
            switch (IsExternal)
            {
                case true when Hole > DedRadius || Hole < 0.01:
                    mesh = Mesh.CreateFromClosedPolyline(pl);
                    return mesh;
                case true:
                    {
                        var circle = Polyline.CreateCircumscribedPolygon(new Circle(Plane, IsExternal ? Hole : AddRadius + Hole), 30);
                        mesh = Mesh.CreatePatch(pl, 0.1, null, null, null, circle, false, 0);
                        var indices = new List<int>();
                        for (var i = 0; i < mesh.Faces.Count; i++)
                        {
                            if (mesh.Faces.GetFaceCenter(i).DistanceToSquared(Plane.Origin) < Hole * Hole)
                                indices.Add((i));
                        }
                        mesh.Faces.DeleteFaces(indices, true);
                        return mesh;
                    }
                case false:
                    {
                        var circle = Polyline.CreateCircumscribedPolygon(new Circle(Plane, IsExternal ? Hole : AddRadius + Hole), 60);
                        mesh = Mesh.CreatePatch(circle, 0.1, null, null, null, pl, false, 0);
                        var indices = new List<int>();
                        var insideMesh = Mesh.CreateFromClosedPolyline(pl);
                        for (var i = 0; i < mesh.Faces.Count; i++)
                        {
                            if (insideMesh.ClosestMeshPoint(mesh.Faces.GetFaceCenter(i), 0.01) == null) continue;
                            indices.Add(i);
                        }
                        mesh.Faces.DeleteFaces(indices, true);
                        return mesh;
                    }
            }
        }
        public void XFormAll(Transform xForm)
        {
            var plane = Plane;
            plane.Transform(xForm);
            Plane = plane;
            GearCurve.Transform(xForm);
        }
        public void Rotate(double angle)
        {
            GearCurve.Rotate(angle, Plane.ZAxis, Plane.Origin);
            Rotation = angle;
        }
    }
}
