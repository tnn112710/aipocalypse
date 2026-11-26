namespace MB13.Aipocalypse.Core.Events
{
    // The night forecast changed (ForecastService): read ForecastService.Fight for the new one.
    public readonly struct ForecastChanged
    {
    }

    // Today's forecast was revealed, or hidden again for a new day (ForecastReveal). `Live`: it happened in play (the board
    // spins); false after a load or a clock jump (the board just shows it).
    public readonly struct ForecastRevealChanged
    {
        public readonly bool Revealed;
        public readonly bool Live;

        public ForecastRevealChanged(bool revealed, bool live)
        {
            Revealed = revealed;
            Live = live;
        }
    }
}
