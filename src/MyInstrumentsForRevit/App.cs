using Autodesk.Revit.UI;
using MyInstrumentsForRevit.Ribbon;

namespace MyInstrumentsForRevit
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            RibbonBuilder.Build(application);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}

