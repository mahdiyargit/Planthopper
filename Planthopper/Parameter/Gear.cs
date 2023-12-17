using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
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
        public double PitchRad { get; }
        public double BaseRad { get; }
        public double AddRad => PitchRad + Addendum;
        public double DedRad => PitchRad - Dedendum;
        public Curve GearCurve { get; }
        public Gear()
        { }
        public Gear(Gear g)
        {
            Plane = g.Plane;
            Rotation = g.Rotation;
            IsExternal = g.IsExternal;
            Hole = g.Hole;
            N = g.N;
            Width = g.Width;
            PAngle = g.PAngle;
            Addendum = g.Addendum;
            Dedendum = g.Dedendum;
            PitchRad = g.PitchRad;
            BaseRad = g.BaseRad;
            GearCurve = g.GearCurve.DuplicateCurve();
        }
        public Gear(Plane plane, double rotation, bool isExternal, double hole, int n, double width, double pAngle, double addendum, double dedendum, double pitchRad, double baseRad, Curve gearCurve)
        {
            Plane = plane;
            Rotation = rotation;
            IsExternal = isExternal;
            Hole = hole;
            N = n;
            Width = width;
            PAngle = pAngle;
            Addendum = addendum;
            Dedendum = dedendum;
            PitchRad = pitchRad;
            BaseRad = baseRad;
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
            PitchRad = n * width / Math.PI;
            if (addendum < tolerance && dedendum < tolerance)
            {
                GearCurve = new Circle(Plane, PitchRad).ToNurbsCurve();
                Addendum = 0.0;
                Dedendum = 0.0;
                BaseRad = PitchRad;
                return;
            }
            BaseRad = Math.Cos(pAngle) * PitchRad;
            var angle = Math.PI / n;
            var invAng = Math.Tan(pAngle) - pAngle;
            //Calculate the mirror line angle alpha
            var α = 0.5 * angle + invAng;
            //A carefully chosen starting value, involute domain angle
            var β = 1.0;
            while (Math.Abs(Math.Tan(β) - β - α) > 10e-10)
                β -= (Math.Tan(β) - β - α) / Math.Pow(Math.Tan(β), 2);
            var maxRad = BaseRad / Math.Cos(β);
            if (addendum > maxRad - PitchRad) addendum = Math.Floor((maxRad - PitchRad) / tolerance) * tolerance;
            if (dedendum > maxRad - PitchRad) dedendum = Math.Floor((maxRad - PitchRad) / tolerance) * tolerance;
            Addendum = addendum;
            Dedendum = dedendum;
            var trochoidStart = new Point3d(DedRad, width * 0.5 - Math.Tan(pAngle) * dedendum, 0.0);
            var addCir = new Circle(AddRad);
            var dedCir = new Circle(DedRad);
            var involuteCrv = MoveAndRotate(new Point3d(BaseRad, 0.0, 0.0), -maxRad, 0.0, -BaseRad, 100);
            var x = Intersection.CurveCurve(involuteCrv, new Circle(PitchRad).ToNurbsCurve(), tolerance, 0);
            involuteCrv.Rotate(-PointAngle(involuteCrv.PointAt(x[0].ParameterA)), Vector3d.ZAxis, Point3d.Origin);
            x = Intersection.CurveCurve(involuteCrv, addCir.ToNurbsCurve(), tolerance, 0);
            involuteCrv = involuteCrv.Trim(x[0].ParameterA, involuteCrv.Domain.T1);
            involuteCrv.Rotate(Math.PI / 2 / n, Vector3d.ZAxis, Point3d.Origin);
            var trochoidCrv = MoveAndRotate(trochoidStart, -maxRad / 3.0, maxRad / 1.6, -PitchRad, 100);
            x = Intersection.CurveCurve(involuteCrv, trochoidCrv, tolerance, 0);
            double ta, tb;
            if (x.Count != 0)
            {
                ta = x[0].ParameterA;
                tb = x[0].ParameterB;
            }
            else
            {
                involuteCrv.ClosestPoints(trochoidCrv, out var pa, out var pb);
                involuteCrv.ClosestPoint(pa, out ta);
                trochoidCrv.ClosestPoint(pb, out tb);
            }
            involuteCrv = involuteCrv.Trim(involuteCrv.Domain[0], ta);
            var c = involuteCrv;
            if (dedendum > 0.0)
            {
                trochoidCrv = trochoidCrv.Trim(trochoidCrv.Domain[0], tb);
                x = Intersection.CurveCurve(trochoidCrv, dedCir.ToNurbsCurve(), tolerance, 0);
                trochoidCrv = trochoidCrv.Trim(x[0].ParameterA, trochoidCrv.Domain[0]);
                if (involuteCrv is null)
                {
                    x = Intersection.CurveCurve(trochoidCrv, addCir.ToNurbsCurve(), tolerance, 0);
                    c = x.Count > 0 ? trochoidCrv.Trim(trochoidCrv.Domain[0], x[0].ParameterA) : trochoidCrv;
                    c.Reverse();
                }
                else
                    c = Curve.JoinCurves(new[] { involuteCrv, trochoidCrv }, tolerance)[0];
            }
            var a1 = PointAngle(c.PointAtEnd);
            var a2 = PointAngle(c.PointAtStart);
            var arc1 = new Arc(dedCir, new Interval(-a1, a1));
            var arc2 = new Arc(addCir, new Interval(a2, 2 * angle - a2));
            var d = c.DuplicateCurve();
            d.Transform(Transform.Mirror(Point3d.Origin, Vector3d.YAxis));
            var crvArr = new Curve[n];
            crvArr[0] = Curve.JoinCurves(new[] { arc1.ToNurbsCurve(), c, arc2.ToNurbsCurve(), d }, tolerance)[0];
            var xForm = Transform.Rotation(Math.PI / n * 2, Point3d.Origin);
            for (var i = 1; i < n; i++)
            {
                crvArr[i] = crvArr[i - 1].DuplicateCurve();
                crvArr[i].Transform(xForm);
            }
            GearCurve = Curve.JoinCurves(crvArr, tolerance)[0];
            GearCurve.Transform(Transform.PlaneToPlane(Plane.WorldXY, Plane));
        }
        private static Curve MoveAndRotate(Point3d start, double d0, double d1, double r, int n)
        {
            var pts = new Point3d[n + 1];
            var dStep = (d1 - d0) / n;
            var aStep = dStep / r;
            var a0 = d0 / r;
            for (var i = 0; i <= n; i++)
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
            var pl = GearCurve.ToPolyline(-1, -1, 0.2, 10, 100, 0.1, 1, 50, false).ToPolyline();
            if (IsExternal)
            {
                if (Hole > DedRad || Hole < 0.01)
                {
                    pl.Add(pl[0]);
                    return Mesh.CreateFromTessellation(pl, new List<Polyline> { pl }, Plane, false);
                }
                var circle = Polyline.CreateCircumscribedPolygon(new Circle(Plane, Hole), 30);
                var pts = new List<Point3d>(pl);
                pts.AddRange(circle);
                pl.Add(pl[0]);
                circle.Add(circle[0]);
                var mesh = Mesh.CreateFromTessellation(pts, new List<Polyline> { pl, circle }, Plane, false);
                var indices = new List<int>();
                for (var i = 0; i < mesh.Faces.Count; i++)
                {
                    if (mesh.Faces.GetFaceCenter(i).DistanceToSquared(Plane.Origin) < Hole * Hole)
                        indices.Add((i));
                }
                mesh.Faces.DeleteFaces(indices);
                return mesh;
            }
            else
            {
                var circle = Polyline.CreateCircumscribedPolygon(new Circle(Plane, Hole + AddRad), 60);
                var pts = new List<Point3d>(pl);
                pts.AddRange(circle);
                pl.Add(pl[0]);
                circle.Add(circle[0]);
                var mesh = Mesh.CreateFromTessellation(pts, new List<Polyline> { pl, circle }, Plane, false);
                var indices = new List<int>();
                var insideMesh = Mesh.CreateFromClosedPolyline(pl);
                for (var i = 0; i < mesh.Faces.Count; i++)
                {
                    if (insideMesh.ClosestMeshPoint(mesh.Faces.GetFaceCenter(i), 0.01) == null) continue;
                    indices.Add(i);
                }
                mesh.Faces.DeleteFaces(indices);
                return mesh;
            }
        }
        public Brep ToBrep() => Brep.CreatePlanarBreps(new[] { GearCurve, new Circle(Plane, IsExternal ? Hole : AddRad + Hole).ToNurbsCurve() }, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)[0];
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
