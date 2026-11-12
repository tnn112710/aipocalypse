namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // What the herd was last told: opening the pen gate releases it to the pasture, the bell calls it home. Plain rules.
    public sealed class HerdOrders
    {
        public bool GateOpen { get; private set; }
        // The bell rang since the gate was last opened.
        public bool Called { get; private set; }
        // Free to walk out and graze on the pasture.
        public bool Released => GateOpen && !Called;
        // Goes up whenever the orders change, so an animal busy eating looks again at once.
        public int Version { get; private set; }

        public void OpenGate()
        {
            if (GateOpen && !Called)
                return;
            GateOpen = true;
            Called = false;
            Version++;
        }

        public void CloseGate()
        {
            if (!GateOpen)
                return;
            GateOpen = false;
            Version++;
        }

        // False when nothing is out to call (the gate is shut, or the bell already rang).
        public bool Call()
        {
            if (!GateOpen || Called)
                return false;
            Called = true;
            Version++;
            return true;
        }

        public void Restore(bool gateOpen, bool called)
        {
            GateOpen = gateOpen;
            Called = gateOpen && called;
            Version++;
        }
    }
}
