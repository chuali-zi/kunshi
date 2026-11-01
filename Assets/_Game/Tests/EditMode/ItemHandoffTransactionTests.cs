using ConGame.Domain.Interaction;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{

    public sealed class ItemHandoffTransactionTests
    {
        [Test]
        public void PickUpBeginsAttachPendingAndAttachMarkerCompletes()
        {
            ItemHandoffTransaction transaction = new ItemHandoffTransaction();

            Assert.That(transaction.TryBegin(HandoffKind.PickUp, 0.35f, 1f), Is.True);
            Assert.That(transaction.Phase, Is.EqualTo(HandoffPhase.AttachPending));
            Assert.That(transaction.NotifyDetachMarker(), Is.EqualTo(HandoffCompletion.None));
            Assert.That(transaction.NotifyAttachMarker(), Is.EqualTo(HandoffCompletion.CompletedByMarker));
            Assert.That(transaction.IsPending, Is.False);
        }

        [Test]
        public void PutDownBeginsDetachPendingAndDetachMarkerCompletes()
        {
            ItemHandoffTransaction transaction = new ItemHandoffTransaction();

            Assert.That(transaction.TryBegin(HandoffKind.PutDown, 0.3f, 2f), Is.True);
            Assert.That(transaction.Phase, Is.EqualTo(HandoffPhase.DetachPending));
            Assert.That(transaction.NotifyAttachMarker(), Is.EqualTo(HandoffCompletion.None));
            Assert.That(transaction.NotifyDetachMarker(), Is.EqualTo(HandoffCompletion.CompletedByMarker));
            Assert.That(transaction.IsPending, Is.False);
        }

        [Test]
        public void ThrowUsesDetachPhase()
        {
            ItemHandoffTransaction transaction = new ItemHandoffTransaction();
            transaction.TryBegin(HandoffKind.Throw, 0.2f, 0f);

            Assert.That(transaction.Phase, Is.EqualTo(HandoffPhase.DetachPending));
        }

        [Test]
        public void FallbackCompletesOnlyAfterDeadline()
        {
            ItemHandoffTransaction transaction = new ItemHandoffTransaction();
            transaction.TryBegin(HandoffKind.PickUp, 0.5f, 10f);

            Assert.That(transaction.Advance(10.4f), Is.EqualTo(HandoffCompletion.None));
            Assert.That(transaction.IsPending, Is.True);
            Assert.That(transaction.Advance(10.5f), Is.EqualTo(HandoffCompletion.CompletedByFallback));
            Assert.That(transaction.IsPending, Is.False);
        }

        [Test]
        public void CannotBeginWhilePending()
        {
            ItemHandoffTransaction transaction = new ItemHandoffTransaction();
            transaction.TryBegin(HandoffKind.PickUp, 0.5f, 0f);

            Assert.That(transaction.TryBegin(HandoffKind.Throw, 0.5f, 0.1f), Is.False);
            Assert.That(transaction.Kind, Is.EqualTo(HandoffKind.PickUp));
        }

        [Test]
        public void CancelRestoresIdleWithoutCompletion()
        {
            ItemHandoffTransaction transaction = new ItemHandoffTransaction();
            transaction.TryBegin(HandoffKind.PutDown, 0.5f, 0f);

            Assert.That(transaction.Cancel(), Is.EqualTo(HandoffCompletion.Cancelled));
            Assert.That(transaction.IsPending, Is.False);
            Assert.That(transaction.NotifyDetachMarker(), Is.EqualTo(HandoffCompletion.None));
            Assert.That(transaction.Advance(float.PositiveInfinity), Is.EqualTo(HandoffCompletion.None));
            Assert.That(transaction.TryBegin(HandoffKind.PickUp, 0.5f, 1f), Is.True);
        }

        [Test]
        public void MarkerAfterCompletionIsIgnored()
        {
            ItemHandoffTransaction transaction = new ItemHandoffTransaction();
            transaction.TryBegin(HandoffKind.PickUp, 0.5f, 0f);
            transaction.NotifyAttachMarker();

            Assert.That(transaction.NotifyAttachMarker(), Is.EqualTo(HandoffCompletion.None));
        }
    }
}
