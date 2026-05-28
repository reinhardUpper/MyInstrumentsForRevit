using Autodesk.Revit.DB;

namespace MyInstrumentsForRevit.Filters
{
    internal static class ViewFilterApplicator
    {
        public static bool CanUseFilters(View view)
        {
            try
            {
                view.GetFilters();
                return true;
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                return false;
            }
        }

        public static void ApplyVisibility(View view, ElementId filterId, bool visible)
        {
            if (!view.GetFilters().Contains(filterId))
            {
                view.AddFilter(filterId);
            }

            view.SetFilterVisibility(filterId, visible);
        }

        public static FilterViewState GetState(View view, ElementId filterId)
        {
            if (!view.GetFilters().Contains(filterId))
            {
                return FilterViewState.NotApplied;
            }

            return view.GetFilterVisibility(filterId)
                ? FilterViewState.Visible
                : FilterViewState.Hidden;
        }
    }
}
