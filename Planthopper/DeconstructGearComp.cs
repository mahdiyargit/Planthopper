using Grasshopper.Kernel;
using Planthopper.Parameter;
using Rhino.Geometry;
using System;

namespace Planthopper
{
    public class DeconstructGearComp : GH_Component
    {
        public DeconstructGearComp() : base("Deconstruct Gear", "DeGear", "Deconstruct a gear", "Planthopper", "Gear")
        {
        }
        protected override void RegisterInputParams(GH_InputParamManager pManager) =>
            pManager.AddParameter(new GH_GearParam(), "Gear", "G", "Gear to deconstruct", GH_ParamAccess.item);
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Curve", "C", "Gear curve", GH_ParamAccess.item);
            pManager.AddTextParameter("Type", "T", "Gear type", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Count", "N", "Number of teeth", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Plane", "P", "Gear base plane", GH_ParamAccess.item);
            pManager.AddCircleParameter("PitchCircle", "PC", "Pitch circle", GH_ParamAccess.item);
            pManager.AddCircleParameter("BaseCircle", "BC", "Base circle", GH_ParamAccess.item);
            pManager.AddCircleParameter("AddCircle", "AC", "Addendum circle", GH_ParamAccess.item);
            pManager.AddCircleParameter("DedCircle", "DC", "Dedendum circle", GH_ParamAccess.item);
        }
        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            var g = new Gear();
            dataAccess.GetData(0, ref g);
            dataAccess.SetData(0, g.GearCurve);
            dataAccess.SetData(1, g.IsExternal ? "External" : "Internal");
            dataAccess.SetData(2, g.N);
            dataAccess.SetData(3, g.Plane);
            dataAccess.SetData(4, new Circle(g.Plane, g.PitchRadius));
            dataAccess.SetData(5, new Circle(g.Plane, g.BaseRadius));
            dataAccess.SetData(6, new Circle(g.Plane, g.AddRadius));
            dataAccess.SetData(7, new Circle(g.Plane, g.DedRadius));
        }
        protected override System.Drawing.Bitmap Icon => Properties.Resources.deconstructGear;
        public override Guid ComponentGuid => new Guid("7A1DF9A6-933D-4464-AFD9-3A688A180176");
    }
}