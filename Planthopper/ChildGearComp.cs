using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Planthopper.Parameter;
using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Planthopper
{
    public class ChildGearComp : GH_Component
    {
        private bool _useDegrees;
        private bool _previewMesh = true;
        private List<Mesh> _meshes;
        public ChildGearComp() : base("Child Gear", "CGear", "Create a gear that is connected to another gear", "Planthopper", "Gear")
        {
        }
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new GH_GearParam(), "Parent", "P", "Parent gear", GH_ParamAccess.item);
            pManager.AddAngleParameter("Angle", "A", "Connection angle", GH_ParamAccess.item, 0.0);
            pManager.AddIntegerParameter("Count", "N", "Number of teeth", GH_ParamAccess.item, 10);
            pManager.AddNumberParameter("Hole", "H", "Shaft hole for External gear.\nOffset distance for Internal gear.", GH_ParamAccess.item, .5);
            pManager.AddIntegerParameter("Type", "T", "Gear type", GH_ParamAccess.item, 0);
            var type = (Param_Integer)pManager[4];
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
            if (Params.Input[1] is Param_Number angleParameter)
                _useDegrees = angleParameter.UseDegrees;
        }
        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            var parentGear = new Gear();
            dataAccess.GetData(0, ref parentGear);
            var angle = 0.0;
            dataAccess.GetData(1, ref angle);
            angle = _useDegrees ? RhinoMath.ToRadians(angle) : angle;
            var n = 10;
            dataAccess.GetData(2, ref n);
            n = Math.Abs(n);
            if (n < 3)
            {
                n = 3;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Number of teeth must be larger than or equal to 3");
            }
            var hole = 0.0;
            dataAccess.GetData(3, ref hole);
            var type = 0;
            dataAccess.GetData(4, ref type);

            var dedendum = (type == 2 || parentGear.IsExternal) ? parentGear.Dedendum : parentGear.Addendum;
            var addendum = (type == 2 || parentGear.IsExternal) ? parentGear.Addendum : parentGear.Dedendum;
            var parentPlane = parentGear.Plane;
            var gear = new Gear(parentPlane, n, parentGear.Width, parentGear.PAngle, ref dedendum, ref addendum, type != 2, hole, Utility.DocumentTolerance());
            Message = type != 2 ? "External" : "Internal";
            if (parentGear.Addendum > addendum)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Parent addendum must be smaller than or equal to {addendum}");
            if (parentGear.Dedendum > dedendum)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Parent dedendum must be smaller than or equal to {dedendum}");
            if (type == 1)
                gear.Rotate(angle + parentGear.Rotation);

            else if (type == 0 && parentGear.IsExternal || type == 2 && !parentGear.IsExternal)
            {
                var vector = (parentGear.PitchRad + gear.PitchRad) * parentPlane.XAxis;
                vector.Rotate(angle, parentPlane.ZAxis);
                gear.XFormAll(Transform.Translation(new Vector3d(vector)));

                angle += (-parentGear.Rotation + angle) * (parentGear.BaseRad / gear.BaseRad);
                angle += n % 2 == 0 ? Math.PI / n : 0;
                gear.Rotate(angle);
            }
            else
            {
                var vector = (gear.PitchRad - parentGear.PitchRad) * parentPlane.XAxis;
                vector.Rotate(angle, parentPlane.ZAxis);
                gear.XFormAll(Transform.Translation(new Vector3d(vector)));
                angle *= -1;
                angle += (parentGear.Rotation + angle) * (-parentGear.BaseRad / gear.BaseRad);
                angle += (parentGear.N % 2) == 0 ? Math.PI / n : 0;
                angle += (n % 2) == 0 ? Math.PI / n : 0;
                gear.Rotate(-angle);
            }
            if (_previewMesh && !Hidden)
                _meshes.Add(gear.ToMesh());
            dataAccess.SetData(0, new GH_Gear(gear));
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.childGear;
        public override Guid ComponentGuid => new Guid("545B35AE-6006-41DC-8422-DFB7731A8741");
        public override GH_Exposure Exposure => GH_Exposure.primary;
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