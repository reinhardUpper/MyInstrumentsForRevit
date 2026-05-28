namespace MyInstrumentsForRevit.Filters
{
    public sealed class FilterSearchItem
    {
        public FilterSearchItem(FilterItem filter, FilterViewState state)
        {
            Filter = filter;
            State = state;
        }

        public FilterItem Filter { get; }

        public FilterViewState State { get; set; }

        public string Name => Filter.Name;

        public bool IsVisible => State == FilterViewState.Visible;

        public bool IsOnView => State != FilterViewState.NotApplied;

        public string StateText
        {
            get
            {
                switch (State)
                {
                    case FilterViewState.Visible:
                        return "ВКЛ";
                    case FilterViewState.Hidden:
                        return "ВЫКЛ";
                    default:
                        return "НЕТ";
                }
            }
        }

        public string StateBackground
        {
            get
            {
                switch (State)
                {
                    case FilterViewState.Visible:
                        return "#D7F0DF";
                    case FilterViewState.Hidden:
                        return "#F8D8D8";
                    default:
                        return "#E9E9E9";
                }
            }
        }

        public string StateForeground
        {
            get
            {
                switch (State)
                {
                    case FilterViewState.Visible:
                        return "#176B35";
                    case FilterViewState.Hidden:
                        return "#8A1F1F";
                    default:
                        return "#555555";
                }
            }
        }
    }
}
