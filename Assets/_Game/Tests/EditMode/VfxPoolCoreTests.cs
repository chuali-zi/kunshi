using System.Collections.Generic;
using ConGame.Presentation.Vfx;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{
    public sealed class VfxPoolCoreTests
    {
        [Test]
        public void PrewarmCreatesUpToLimitAndAcquireReusesIdle()
        {
            int created = 0;
            VfxPoolCore<string> pool = new VfxPoolCore<string>(() => "instance" + created++, null, null, 3);

            pool.Prewarm(2);

            Assert.That(pool.TotalCreated, Is.EqualTo(2));
            Assert.That(pool.IdleCount, Is.EqualTo(2));

            string first = pool.Acquire();
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.IdleCount, Is.EqualTo(1));

            Assert.That(pool.Release(first), Is.True);
            Assert.That(pool.Acquire(), Is.SameAs(first));
            Assert.That(pool.TotalCreated, Is.EqualTo(2), "空闲实例必须复用,不得新建");
        }

        [Test]
        public void ExhaustedPoolRecyclesOldestActiveInsteadOfDestroying()
        {
            int created = 0;
            List<string> released = new List<string>();
            VfxPoolCore<string> pool = new VfxPoolCore<string>(
                () => "instance" + created++, null, released.Add, 2);

            string first = pool.Acquire();
            string second = pool.Acquire();
            string third = pool.Acquire();

            Assert.That(pool.TotalCreated, Is.EqualTo(2), "达到上限后不得扩容");
            Assert.That(third, Is.SameAs(first), "耗尽时回收最早活跃实例");
            Assert.That(released, Contains.Item(first));
            Assert.That(pool.ActiveCount, Is.EqualTo(2));
        }

        [Test]
        public void ReleaseAllReturnsEveryActiveInstance()
        {
            int created = 0;
            VfxPoolCore<string> pool = new VfxPoolCore<string>(() => "instance" + created++, null, null, 8);

            pool.Acquire();
            pool.Acquire();
            pool.ReleaseAll();

            Assert.That(pool.ActiveCount, Is.EqualTo(0));
            Assert.That(pool.IdleCount, Is.EqualTo(2));
        }

        [Test]
        public void ReleaseRejectsUnknownOrDoubleRelease()
        {
            int created = 0;
            VfxPoolCore<string> pool = new VfxPoolCore<string>(() => "instance" + created++, null, null, 4);

            string instance = pool.Acquire();
            Assert.That(pool.Release(null), Is.False);
            Assert.That(pool.Release("foreign"), Is.False);
            Assert.That(pool.Release(instance), Is.True);
            Assert.That(pool.Release(instance), Is.False, "重复归还不得破坏记账");
        }
    }
}
