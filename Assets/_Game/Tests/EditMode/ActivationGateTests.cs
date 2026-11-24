using MB13.Aipocalypse.Core.Entities;
using NUnit.Framework;

namespace MB13.Aipocalypse.Tests
{
    public class ActivationGateTests
    {
        [Test]
        public void InjectedBeforeStart_ActivatesOnStart()
        {
            var gate = new ActivationGate();
            Assert.That(gate.OnEnabled(), Is.False, "the first OnEnable comes before Start");
            Assert.That(gate.OnConstructed(true), Is.False, "Start hasn't run");
            Assert.That(gate.OnStarted(), Is.True);
        }

        [Test]
        public void InjectedAfterStart_ActivatesOnConstructOnlyWhileEnabled()
        {
            var gate = new ActivationGate();
            Assert.That(gate.OnStarted(), Is.False, "not injected yet");
            Assert.That(gate.OnConstructed(true), Is.True);

            var disabled = new ActivationGate();
            disabled.OnStarted();
            Assert.That(disabled.OnConstructed(false), Is.False, "disabled: waits for OnEnable");
            Assert.That(disabled.OnEnabled(), Is.True);
        }

        [Test]
        public void DisableEnableCycles_ActivateAgainOnlyOnceStartedAndInjected()
        {
            var gate = new ActivationGate();
            gate.OnConstructed(true);
            Assert.That(gate.OnEnabled(), Is.False, "re-enabled before Start");
            gate.OnStarted();
            for (int i = 0; i < 3; i++)
                Assert.That(gate.OnEnabled(), Is.True);

            var never = new ActivationGate();
            never.OnStarted();
            Assert.That(never.OnEnabled(), Is.False, "never injected");
        }
    }
}
