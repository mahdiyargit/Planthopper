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
using System.Windows.Forms;

namespace Planthopper
{
    public class ConstructGearComp : GH_Component
    {
        private bool _useDegrees;
        private Interval _angleDomain = new Interval(RhinoMath.ToRadians(10), RhinoMath.ToRadians(35));
        private bool _previewMesh = true;
        private List<Mesh> _meshes;
        public ConstructGearComp() : base("Construct Gear", "Gear", "Create an involute gear", "Planthopper", "Gear")
        { }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPlaneParameter("Plane", "P", "Plane", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddNumberParameter("Width", "W", "Tooth Width", GH_ParamAccess.item, 1.0);
            pManager.AddAngleParameter("PressureAngle", "PA", "Pressure angle, between 10° to 35°\nStandard values are 14.5° and 20°.", GH_ParamAccess.item, Math.PI / 9);
            pManager.AddNumberParameter("Addendum", "A", "Optional addendum. if nothing supplied, \"width × 2 / π\".", GH_ParamAccess.item);
            pManager.AddNumberParameter("Dedendum", "D", "Optional dedendum. if nothing supplied, \"width × 2 / π\".", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Count", "N", "Number of teeth", GH_ParamAccess.item, 10);
            pManager.AddNumberParameter("Hole", "H", "Shaft hole for External gear.\nOffset distance for Internal gear.", GH_ParamAccess.item, 0.5);
            pManager.AddAngleParameter("Rotation", "R", "Rotation", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Type", "T", "Gear type", GH_ParamAccess.item, 0);
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            var type = (Param_Integer)pManager[8];
            type.AddNamedValue("External", 0);
            type.AddNamedValue("Axle", 1);
            type.AddNamedValue("Internal", 2);
        }
        protected override void RegisterOutputParams(GH_OutputParamManager pManager) =>
            pManager.AddParameter(new GH_GearParam(), "Gear", "G", "Gear", GH_ParamAccess.item);
        protected override void BeforeSolveInstance()
        {
            _meshes = new List<Mesh>();
            _useDegrees = false;
            if (Params.Input[2] is Param_Number pAParameter)
                _useDegrees = pAParameter.UseDegrees;
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
            pAngle = _useDegrees ? RhinoMath.ToRadians(pAngle) : pAngle;
            if (!_angleDomain.IncludesParameter(pAngle))
            {
                pAngle = RhinoMath.Clamp(pAngle, _angleDomain.T0, _angleDomain.T1);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Pressure angle must be between 10° to 35°");
            }
            var initAdd = width * 2.0 / Math.PI;
            var initDed = initAdd;
            if (dataAccess.GetData(3, ref initAdd)) initAdd = Math.Abs(initAdd);
            if (dataAccess.GetData(4, ref initDed)) initDed = Math.Abs(initDed);
            var n = 0;
            dataAccess.GetData(5, ref n);
            n = Math.Abs(n);
            if (n < 3)
            {
                n = 3;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Number of teeth must be larger than or equal to 3");
            }
            var hole = 0.0;
            dataAccess.GetData(6, ref hole);
            var rotation = 0.0;
            dataAccess.GetData(7, ref rotation);
            var type = 0;
            dataAccess.GetData(8, ref type);
            hole = Math.Abs(hole);
            var addendum = type == 2 ? initDed : initAdd;
            var dedendum = type == 2 ? initAdd : initDed;
            var gear = new Gear(plane, n, width, pAngle, ref addendum, ref dedendum, type != 2, hole, tolerance);
            if ((type == 2 ? initDed : initAdd) > addendum)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    $"Addendum must be smaller than or equal to {addendum}");
            if ((type == 2 ? initAdd : initDed) > dedendum)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    $"Dedendum must be smaller than or equal to {dedendum}");
            gear.Rotate(rotation);
            if (_previewMesh)
                _meshes.Add(gear.ToMesh());
            dataAccess.SetData(0, new GH_Gear(gear));
        }
        protected override Bitmap Icon => Resources.constructGear;
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

        public override Guid ComponentGuid => new Guid("cbb5c2c2-c181-465c-a0f6-8090e211888a");
    }
}