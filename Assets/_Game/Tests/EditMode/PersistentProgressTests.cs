using ConGame.Domain.Persistence;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{

    public sealed class PersistentProgressTests
    {
        [Test]
        public void NormalizeRepairsOutOfRangeFields()
        {
            PersistentProgressV1 progress = new PersistentProgressV1
            {
                schemaVersion = -1,
                loopIndex = -5,
                darkAdaptationXp = 99
            };

            progress.Normalize();

            Assert.That(progress.schemaVersion, Is.EqualTo(PersistentProgressV1.CurrentSchemaVersion));
            Assert.That(progress.loopIndex, Is.EqualTo(1));
            Assert.That(progress.darkAdaptationXp, Is.EqualTo(5));
        }
    }
}
