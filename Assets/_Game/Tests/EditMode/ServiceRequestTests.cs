using Aipocalypse.Core.GraphicsSystem;
using Aipocalypse.Core.TimeSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aipocalypse.Tests
{
    // Owner-keyed requests: several systems ask at once, the right one wins, and each release undoes only its own.
    public class ServiceRequestTests
    {
        [Test]
        public void TimeService_PauseBeatsSlowMotion_AndReleasesRestoreTheRest()
        {
            object slowMo = new();
            object menu = new();
            using var time = new TimeService();

            time.Request(slowMo, 0.5f);
            Assert.AreEqual(0.5f, time.Scale);
            time.RequestPause(menu);
            Assert.IsTrue(time.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);

            time.ReleasePause(menu);
            Assert.AreEqual(0.5f, Time.timeScale);
            time.Release(slowMo);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void TimeService_HitStops_KeepTheLowestScale_AndFreezeWhilePaused()
        {
            object menu = new();
            using var time = new TimeService();

            time.RequestHitStop(0.1f, 0.2f);
            time.RequestHitStop(0.05f, 0.5f);
            Assert.AreEqual(0.2f, time.Scale, 1e-5f);

            time.RequestPause(menu);
            time.AdvanceHitStop(1f);
            time.ReleasePause(menu);
            Assert.AreEqual(0.2f, time.Scale, 1e-5f, "the hit stop's real-time timer doesn't run during a pause");

            time.AdvanceHitStop(0.11f);
            Assert.AreEqual(1f, time.Scale);
        }

        [Test]
        public void TimeService_ReleasingAnOwnerThatNeverAsked_ChangesNothing()
        {
            object slowMo = new();
            using var time = new TimeService();

            time.Request(slowMo, 0.5f);
            time.Release(new object());
            time.ReleasePause(new object());

            Assert.AreEqual(0.5f, time.Scale);
            Assert.IsFalse(time.IsPaused);
        }

        [Test]
        public void TimeService_Dispose_RestoresTimeScaleAndFixedStep()
        {
            float scale = Time.timeScale;
            float fixedStep = Time.fixedDeltaTime;
            var time = new TimeService();

            time.Request(new object(), 0.3f);
            Assert.AreNotEqual(fixedStep, Time.fixedDeltaTime, "the physics step follows the time scale");
            time.Dispose();

            Assert.AreEqual(scale, Time.timeScale);
            Assert.AreEqual(fixedStep, Time.fixedDeltaTime);
        }

        [Test]
        public void GraphicsService_ShortestShadowRequestWins_AndDisposeRestoresTheAsset()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
                Assert.Ignore("no render pipeline asset");
            float ShadowDistance() => (float)pipeline.GetType().GetProperty("shadowDistance")!.GetValue(pipeline);

            float saved = ShadowDistance();
            object night = new();
            object cave = new();
            var graphics = new GraphicsService();
            try
            {
                graphics.RequestShadowDistance(night, saved * 0.5f);
                graphics.RequestShadowDistance(cave, saved * 0.25f);
                Assert.AreEqual(saved * 0.25f, ShadowDistance(), 1e-3f);

                graphics.ReleaseShadowDistance(cave);
                Assert.AreEqual(saved * 0.5f, ShadowDistance(), 1e-3f);
                graphics.ReleaseShadowDistance(night);
                Assert.AreEqual(saved, ShadowDistance(), 1e-3f);

                graphics.RequestShadowDistance(night, saved * 0.5f);
            }
            finally
            {
                graphics.Dispose();
            }
            Assert.AreEqual(saved, ShadowDistance(), 1e-3f);
        }
    }
}
