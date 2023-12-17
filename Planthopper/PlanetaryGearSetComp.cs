using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Planthopper.Parameter;
using Planthopper.Properties;
using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Planthopper
{
    public class PlanetaryGearSetComp : GH_Component
    {
        private bool _useDegrees2;
        private bool _useDegrees9;
        private Interval _angleDomain = new Interval(RhinoMath.ToRadians(10), RhinoMath.ToRadians(35));
        private bool _previewMesh = true;
        private List<Mesh> _meshes;
        public PlanetaryGearSetComp() : base("Planetary Gear Set", "PGear", "Create a planetary gear set", "Planthopper", "Gear")
        { }
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPlaneParameter("Plane", "P", "Plane", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddNumberParameter("Width", "W", "Tooth Width", GH_ParamAccess.item, 1.0);
            pManager.AddAngleParameter("PressureAngle", "PA", "Pressure angle, between 10° to 35°\nStandard values are 14.5° and 20°.", GH_ParamAccess.item, Math.PI / 9);
            pManager.AddNumberParameter("Addendum", "A", "Optional addendum. if nothing supplied, \"width × 2 / π\".", GH_ParamAccess.item);
            pManager.AddNumberParameter("Dedendum", "D", "Optional dedendum. if nothing supplied, \"width × 2 / π\".", GH_ParamAccess.item);
            pManager.AddIntegerParameter("SunTeeth", "ZS", "Number of teeth of sun gear", GH_ParamAccess.item, 12);
            pManager.AddIntegerParameter("PlanetTeeth", "ZP", "Number of teeth of planet gears", GH_ParamAccess.item, 6);
            pManager.AddIntegerParameter("PlanetCount", "C", "Number of planet gears", GH_ParamAccess.item, 3);
            pManager.AddNumberParameter("Hole", "H", "Shaft hole for External gear.\nOffset distance for Internal gear.", GH_ParamAccess.item, 0.5);
            pManager.AddAngleParameter("Rotation", "R", "Rotation", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Setup", "S", "Planetary setup. Input → Output", GH_ParamAccess.item, 0);
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            var type = (Param_Integer)pManager[10];
            type.AddNamedValue("Sun → Ring", 0);
            type.AddNamedValue("Sun → Planet", 1);
            type.AddNamedValue("Ring → Sun", 2);
            type.AddNamedValue("Ring → Planet", 3);
            type.AddNamedValue("Planet → Sun", 4);
            type.AddNamedValue("Planet → Ring", 5);
        }
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_GearParam(), "SunGear", "SG", "Sun Gear", GH_ParamAccess.item);
            pManager.AddParameter(new GH_GearParam(), "PlanetGears", "PG", "Planet Gears", GH_ParamAccess.list);
            pManager.AddParameter(new GH_GearParam(), "RingGear", "RG", "Ring Gear", GH_ParamAccess.item);
            pManager.AddNumberParameter("Ratio", "i", "Transmission ratio", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Align", "A", "True if planet gears evenly spaced around the sun gear", GH_ParamAccess.item);
            pManager.AddBooleanParameter("EnoughSpace", "ES", "True if planet gears have enough space.\nif False planet gears may interfere.", GH_ParamAccess.item);
        }
        protected override void BeforeSolveInstance()
        {
            _meshes = new List<Mesh>();
            _useDegrees2 = false;
            _useDegrees9 = false;
            if (Params.Input[2] is Param_Number p2)
                _useDegrees2 = p2.UseDegrees;
            if (Params.Input[9] is Param_Number p9)
                _useDegrees9 = p9.UseDegrees;
        }
        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            var tolerance = Utility.DocumentTolerance();
            var plane = Plane.WorldXY;
            dataAccess.GetData(0, ref plane);
            double width = 0.0, pAngle = 0.0;
            dataAccess.GetData(1, ref width);
            width = Math.Abs(width);
            if (width < tolerance * 10)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Width is too small");
                return;
            }
            dataAccess.GetData(2, ref pAngle);
            pAngle = Math.Abs(pAngle);
            pAngle = _useDegrees2 ? RhinoMath.ToRadians(pAngle) : pAngle;
            if (!_angleDomain.IncludesParameter(pAngle))
            {
                pAngle = RhinoMath.Clamp(pAngle, _angleDomain.T0, _angleDomain.T1);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Pressure angle must be between 10° to 35°");
            }
            var initAdd = width * 2.0 / Math.PI;
            var initDed = initAdd;
            if (dataAccess.GetData(3, ref initAdd)) initAdd = Math.Abs(initAdd);
            if (dataAccess.GetData(4, ref initDed)) initDed = Math.Abs(initDed);
            var zs = 0;
            dataAccess.GetData(5, ref zs);
            zs = Math.Abs(zs);
            var zp = 0;
            dataAccess.GetData(6, ref zp);
            zp = Math.Abs(zp);
            if (zs < 3 || zp < 3)
            {
                zs = Math.Max(3, zs);
                zp = Math.Max(3, zp);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Number of teeth must be larger than or equal to 3");
            }
            var count = 0;
            dataAccess.GetData(7, ref count);
            count = Math.Abs(count);
            if (count < 1)
            {
                count = 1;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Need at least one planet gear");
            }
            var hole = 0.0;
            dataAccess.GetData(8, ref hole);
            var rotation = 0.0;
            dataAccess.GetData(9, ref rotation);
            rotation = _useDegrees9 ? RhinoMath.ToRadians(rotation) : rotation;
            var setup = 0;
            dataAccess.GetData(10, ref setup);
            setup = Math.Abs(setup);
            if (setup > 5)
            {
                setup = 5;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Setup must be between 0 and 5");
            }
            hole = Math.Abs(hole);
            var addendum = initAdd;
            var dedendum = initDed;

            var zr = zs + 2 * zp;

            var angleS = 0.0;
            var angleP = 0.0;
            var ratio = 0.0;
            switch (setup)
            {
                case 0:
                    angleS = rotation;
                    angleP = 0.0;
                    Message = "Sun → Ring";
                    ratio = -(double)zr / zs;
                    break;
                case 1:
                    angleS = rotation;
                    angleP = rotation * zs / (2 * (zs + zp));
                    Message = "Sun → Planet";
                    ratio = 1.0 + ((double)zr / zs);
                    break;
                case 2:
                    angleS = -rotation * zr / zs;
                    angleP = 0.0;
                    Message = "Ring → Sun";
                    ratio = -(double)zs / zr;
                    break;
                case 3:
                    angleS = 0.0;
                    angleP = rotation / (1.0 + (double)zs / zr);
                    Message = "Ring → Planet";
                    ratio = 1.0 + (double)zs / zr;
                    break;
                case 4:
                    angleS = rotation * 2 * (zs + zp) / zs;
                    angleP = rotation;
                    Message = "Planet → Sun";
                    ratio = 1.0 / (1.0 + (double)zr / zs);
                    break;
                case 5:
                    angleS = 0.0;
                    angleP = rotation;
                    Message = "Planet → Ring";
                    ratio = 1.0 / (1.0 + (double)zs / zr);
                    break;
            }
            var angleR = angleP + Math.PI;

            var sun = new Gear(plane, zs, width, pAngle, ref addendum, ref dedendum, true, hole, tolerance);
            sun.Rotate(angleS);

            var planets = new Gear[count];
            var planet = new Gear(sun.Plane, zp, width, pAngle, ref dedendum, ref addendum, true, hole, tolerance);
            Vector3d vector;
            var step = Math.PI / (zs + zp);
            var factor = 2.0 * (zs + (double)zp) / count;
            for (var i = 0; i < count; i++)
            {
                planets[i] = new Gear(planet);
                var a = Math.Round(i * factor) * step + angleP;
                vector = (sun.PitchRad + planets[i].PitchRad) * sun.Plane.XAxis;
                vector.Rotate(a, sun.Plane.ZAxis);
                planets[i].XFormAll(Transform.Translation(new Vector3d(vector)));
                a += (-sun.Rotation + a) * (sun.BaseRad / planets[i].BaseRad);
                a += zp % 2 == 0 ? Math.PI / zp : 0;
                planets[i].Rotate(a);
            }

            planet = planets[0];
            var ring = new Gear(planet.Plane, zr, width, pAngle, ref dedendum, ref addendum, false, hole, tolerance);
            vector = (ring.PitchRad - planet.PitchRad) * planet.Plane.XAxis;
            vector.Rotate(angleR, planet.Plane.ZAxis);
            ring.XFormAll(Transform.Translation(new Vector3d(vector)));
            angleR *= -1;
            angleR += (planet.Rotation + angleR) * (-planet.BaseRad / ring.BaseRad);
            angleR += (planet.N % 2) == 0 ? Math.PI / zr : 0;
            angleR += (zr % 2) == 0 ? Math.PI / zr : 0;
            ring.Rotate(-angleR);

            if (initAdd > addendum)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    $"Addendum must be smaller than or equal to {addendum}");
            if (initDed > dedendum)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    $"Dedendum must be smaller than or equal to {dedendum}");

            if (_previewMesh && !Hidden)
            {
                _meshes.Add(sun.ToMesh());
                _meshes.AddRange(planets.AsParallel().Select(p => p.ToMesh()));
                _meshes.Add(ring.ToMesh());
            }
            dataAccess.SetData(0, new GH_Gear(sun));
            dataAccess.SetDataList(1, planets.AsParallel().Select(p => new GH_Gear(p)));
            dataAccess.SetData(2, new GH_Gear(ring));
            dataAccess.SetData(3, ratio);
            dataAccess.SetData(4, (zs + zr) / (double)count % 1 == 0);
            dataAccess.SetData(5, count == 1 || zp + 2 < (zs + zp) * Math.Sin(Math.PI / count));
        }
        protected override Bitmap Icon => Resources.planetaryGear;
        public override GH_Exposure Exposure => GH_Exposure.primary;
        public override Guid ComponentGuid => new Guid("01999D14-C51A-45B3-9E0F-197E9CF2CFC6");
        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Preview Mesh", Click, null, true, _previewMesh);
        }

        private void Click(object sender, EventArgs e)
        {
            _previewMesh = !_previewMesh;
            ExpireSolution(true);
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            if (!_previewMesh) return;
            var mat = Attributes.Selected ? args.ShadeMaterial_Selected : args.ShadeMaterial;
            foreach (var mesh in _meshes)
                args.Display.DrawMeshShaded(mesh, mat);
        }
        public override bool Write(GH_IWriter writer)
        {
            writer.SetBoolean("PreviewMesh", _previewMesh);
            return base.Write(writer);
        }
        public override bool Read(GH_IReader reader)
        {
            var previewMesh = reader.GetBoolean("PreviewMesh");
            _previewMesh = previewMesh;
            return base.Read(reader);
        }
    }
}