using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;

namespace Planthopper.Parameter
{
    public class GH_Gear : GH_GeometricGoo<Gear>, IGH_BakeAwareData, IGH_PreviewData
    {
        public GH_Gear() { }

        public GH_Gear(Gear gear) => m_value = gear;
        public GH_Gear(GH_Gear ghGear) => m_value = ghGear.m_value;
        public override string TypeName => "Gear";
        public override string TypeDescription => "Gear";
        public override BoundingBox Boundingbox => new Circle(m_value.Plane, m_value.IsExternal ? m_value.AddRadius : m_value.AddRadius + m_value.Hole).BoundingBox;
        public override BoundingBox GetBoundingBox(Transform xform)
        {
            var addCir = new Circle(m_value.Plane, m_value.IsExternal ? m_value.AddRadius : m_value.AddRadius + m_value.Hole);
            return addCir.ToNurbsCurve().GetBoundingBox(xform);
        }
        public BoundingBox ClippingBox => Boundingbox;
        public override string ToString() => m_value.ToString();
        public override IGH_GeometricGoo DuplicateGeometry() => new GH_Gear(m_value);
        public override IGH_GeometricGoo Transform(Transform xform)
        {
            if (xform.SimilarityType == TransformSimilarityType.NotSimilarity)
            {
                var curve = m_value.GearCurve.DuplicateCurve();
                curve.Transform(xform);
                return new GH_Curve(curve);
            }
            var gear = new Gear(m_value);
            gear.XFormAll(xform);
            return new GH_Gear(gear);
        }
        public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
        {
            var gearCurve = m_value.GearCurve.DuplicateCurve();
            xmorph.Morph(gearCurve);
            return new GH_Curve(gearCurve);
        }

        public void DrawViewportWires(GH_PreviewWireArgs args)
        {
            args.Pipeline.DrawCurve(m_value.GearCurve, args.Color, args.Thickness);
            args.Pipeline.DrawPoint(m_value.Plane.Origin, PointStyle.X, 5, args.Color);
            if (m_value.IsExternal && m_value.Hole > m_value.DedRadius || m_value.Hole < 0.01) return;
            var circle = new Circle(m_value.Plane, m_value.IsExternal ? m_value.Hole : m_value.AddRadius + m_value.Hole);
            args.Pipeline.DrawCircle(circle, args.Color, args.Thickness);
        }

        public void DrawViewportMeshes(GH_PreviewMeshArgs args)
        {
        }
        public bool BakeGeometry(RhinoDoc doc, ObjectAttributes att, out Guid objGuid)
        {
            objGuid = doc.Objects.AddCurve(m_value.GearCurve, att);
            doc.Objects.AddPoint(m_value.Plane.Origin);
            if (m_value.IsExternal && m_value.Hole > m_value.DedRadius || m_value.Hole < 0.01) return true;
            var circle = new Circle(m_value.Plane, m_value.IsExternal ? m_value.Hole : m_value.AddRadius + m_value.Hole);
            doc.Objects.AddCircle(circle, att);
            return true;
        }
        public override bool CastFrom(object source)
        {
            switch (source)
            {
                case null:
                    m_value = null;
                    return true;
                case Gear gear:
                    m_value = gear;
                    return true;
                case GH_Gear ghGear:
                    m_value = ghGear.m_value;
                    return true;
                default:
                    return false;
            }
        }
        public override bool CastTo<T>(ref T target)
        {
            if (typeof(T).IsAssignableFrom(typeof(GH_Point)))
            {
                target = (T)(object)new GH_Point(m_value.Plane.Origin);
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_Plane)))
            {
                target = (T)(object)new GH_Plane(m_value.Plane);
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_Circle)))
            {
                target = (T)(object)new GH_Circle(new Circle(m_value.Plane, m_value.PitchRadius));
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_Curve)))
            {
                target = (T)(object)new GH_Curve(m_value.GearCurve);
                return true;
            }

            if (typeof(T).IsAssignableFrom(typeof(Gear)))
            {
                target = (T)(object)m_value;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_Mesh)))
            {
                target = (T)(object)new GH_Mesh(m_value.ToMesh());
                return true;
            }

            if (typeof(T).IsAssignableFrom(typeof(GH_Surface)))
            {
                target = (T)(object)new GH_Surface(m_value.ToBrep());
                return true;
            }
            return false;
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetPlane("Plane", new GH_IO.Types.GH_Plane(m_value.Plane.Origin.X, m_value.Plane.Origin.Y, m_value.Plane.Origin.Z, m_value.Plane.XAxis.X, m_value.Plane.XAxis.Y, m_value.Plane.XAxis.Z, m_value.Plane.YAxis.X, m_value.Plane.YAxis.Y, m_value.Plane.YAxis.Z));
            writer.SetDouble("Rotation", m_value.Rotation);
            writer.SetBoolean("IsExternal", m_value.IsExternal);
            writer.SetDouble("Hole", m_value.Hole);
            writer.SetInt32("N", m_value.N);
            writer.SetDouble("Width", m_value.Width);
            writer.SetDouble("PAngle", m_value.PAngle);
            writer.SetDouble("Addendum", m_value.Addendum);
            writer.SetDouble("Dedendum", m_value.Dedendum);
            writer.SetDouble("PitchRadius", m_value.PitchRadius);
            writer.SetDouble("BaseRadius", m_value.BaseRadius);
            var byteArray = GH_Convert.CommonObjectToByteArray(m_value.GearCurve);
            if (byteArray != null) writer.SetByteArray("GearCurve", byteArray);
            return true;
        }

        public override bool Read(GH_IReader reader)
        {
            var plane = reader.GetPlane("Plane");
            var rotation = reader.GetDouble("Rotation");
            var isExternal = reader.GetBoolean("IsExternal");
            var hole = reader.GetDouble("Hole");
            var n = reader.GetInt32("N");
            var width = reader.GetDouble("Width");
            var pAngle = reader.GetDouble("PAngle");
            var addendum = reader.GetDouble("Addendum");
            var dedendum = reader.GetDouble("Dedendum");
            var pithRadius = reader.GetDouble("PitchRadius");
            var baseRadius = reader.GetDouble("BaseRadius");
            Curve gearCurve = null;
            if (reader.ItemExists("GearCurve"))
                gearCurve = GH_Convert.ByteArrayToCommonObject<Curve>(reader.GetByteArray("GearCurve"));
            m_value = new Gear(new Plane(new Point3d(plane.Origin.x, plane.Origin.y, plane.Origin.z), new Vector3d(plane.XAxis.x, plane.XAxis.y, plane.XAxis.z), new Vector3d(plane.YAxis.x, plane.YAxis.y, plane.YAxis.z)), rotation, isExternal, hole, n, width, pAngle, addendum, dedendum, pithRadius, baseRadius, gearCurve);
            return true;
        }
    }
}
