using Grasshopper.Kernel;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Planthopper.Parameter
{
    public class GH_GearParam : GH_PersistentParam<GH_Gear>, IGH_PreviewObject, IGH_BakeAwareObject
    {
        public GH_GearParam() : base(new GH_InstanceDescription("Gear", "Gear", "Contains a collection of gears",
            "Params", "Geometry"))
        {
        }
        protected override GH_Gear InstantiateT() => new GH_Gear();

        protected override GH_Gear PreferredCast(object data) => !(data is Gear gear) ? null : new GH_Gear(gear);

        public override GH_Exposure Exposure => GH_Exposure.obscure;
        protected override GH_GetterResult Prompt_Singular(ref GH_Gear value) => GH_GetterResult.cancel;
        protected override GH_GetterResult Prompt_Plural(ref List<GH_Gear> values) => GH_GetterResult.cancel;
        public void DrawViewportWires(IGH_PreviewArgs args) => Preview_DrawWires(args);

        public void DrawViewportMeshes(IGH_PreviewArgs args)
        {
        }

        public bool Hidden { get; set; }
        public bool IsPreviewCapable => true;
        public BoundingBox ClippingBox => Preview_ComputeClippingBox();

        public bool IsBakeCapable => !m_data.IsEmpty;
        public void BakeGeometry(RhinoDoc doc, List<Guid> objIds) => BakeGeometry(doc, null, objIds);
        public void BakeGeometry(RhinoDoc doc, ObjectAttributes att, List<Guid> objIds)
        {
            var ghBakeUtility = new GH_BakeUtility(OnPingDocument());
            ghBakeUtility.BakeObjects(m_data, att, doc);
            objIds.AddRange(ghBakeUtility.BakedIds);
        }
        protected override Bitmap Icon => Properties.Resources.gearParam;
        public override Guid ComponentGuid => new Guid("7FA741DB-9B5F-4D26-987F-531BD88AA815");
    }
}
