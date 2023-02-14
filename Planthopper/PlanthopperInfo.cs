using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace Planthopper
{
    public class PlanthopperInfo : GH_AssemblyInfo
    {
        public override string Name => "Planthopper";
        public override string Version => "1.1.0";
        public override Bitmap Icon => null;
        public override string Description => "Planthopper has the only mechanical gears ever found in nature.";
        public override Guid Id => new Guid("1909f89e-222e-482a-8f37-fb8a5fccbac6");
        public override string AuthorName => "Mahdiyar";
        public override string AuthorContact => "Mahdiyar@outlook.com";
    }
}