using NUnit.Framework;
using Piglings.Events;
using Piglings.Rules;
using Piglings.Runtime;

namespace Piglings.Tests
{
    public sealed class ChainTrackerTests
    {
        private EventBus _bus; private NightState _state; private ChainTracker _tracker; private IdAllocator _ids;
        private ChainClosed? _closed;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus(); _state = new NightState(); _ids = new IdAllocator();
            _tracker = new ChainTracker(_bus, _state);
            _closed = null;
            _bus.Subscribe<ChainClosed>(e => _closed = e);
        }

        [Test]
        public void ThrowThatHitsNothing_ClosesEmptyChain()
        {
            var chain = new ChainId(_ids.Next()); var stone = _ids.Next();
            _bus.Publish(new ThrowReleased(chain, stone));
            _bus.Publish(new ThrowableRemoved(stone, chain));
            Assert.That(_closed.HasValue);
            Assert.That(_closed.Value.RobotsDropped, Is.EqualTo(0));
        }

        [Test]
        public void BallKnocksSecondRobot_ChainOfTwo_Depth1_ClosesOnlyWhenAllGone()
        {
            var chain = new ChainId(_ids.Next()); var stone = _ids.Next();
            var a = _ids.Next(); var b = _ids.Next();
            _bus.Publish(new ThrowReleased(chain, stone));
            _bus.Publish(new RobotLostGrip(a, chain, Attribution.FromThrowable(stone)));
            _bus.Publish(new RobotLostGrip(b, chain, Attribution.FromRobotBall(a, 0)));

            _bus.Publish(new ThrowableRemoved(stone, chain));
            _bus.Publish(new RobotRemoved(a, chain, RemovalReason.HitGround));
            Assert.That(_closed.HasValue, Is.False, "b is still falling");

            _bus.Publish(new RobotRemoved(b, chain, RemovalReason.HitGround));
            Assert.That(_closed.Value.RobotsDropped, Is.EqualTo(2));
            Assert.That(_closed.Value.MaxDepth, Is.EqualTo(1));
            Assert.That(_state.LongestChain, Is.EqualTo(2));
            // a: 1st in the chain, depth 0 → 10 ×1.  b: 2nd, depth 1 → 20 ×2.
            var curve = _tracker.Curve;
            Assert.That(_state.Score, Is.EqualTo(curve.RobotTotal(1, 0) + curve.RobotTotal(2, 1)));
        }

        [Test]
        public void RobotReachingTop_CountsAndDoesNotBreakChains()
        {
            var r = _ids.Next();
            _bus.Publish(new RobotRemoved(r, ChainId.None, RemovalReason.EnteredBarn));
            Assert.That(_state.RobotsReachedTop, Is.EqualTo(1));
            Assert.That(_tracker.OpenChainCount, Is.EqualTo(0));
        }
    }
}
