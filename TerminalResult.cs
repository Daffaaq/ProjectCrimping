namespace ProjectCrimping
{
    public class TerminalResult
    {
        public string Sisi;             // "A" / "B"
        public string KodeMesin;
        public decimal? CH, IH, CW, IW;
        public decimal? Tension;
        public string Display => $"TERMINAL {Sisi}  ({KodeMesin})";
    }
}