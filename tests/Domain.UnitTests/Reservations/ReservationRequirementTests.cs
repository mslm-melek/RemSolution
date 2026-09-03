using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.Exceptions;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Domain.UnitTests.Reservations;

/// <summary>
/// One thing the agency asks for before the keys change hands. The states matter
/// because they are a conversation, not a checkbox: a refusal has to say why, and
/// a second attempt has to be possible.
/// </summary>
public class ReservationRequirementTests
{
    private static readonly DateTime Now = new(2030, 4, 2, 9, 0, 0, DateTimeKind.Utc);

    private static ReservationRequirement AnAsk(
        ReservationRequirementKind kind = ReservationRequirementKind.Document) =>
        ReservationRequirement.Create(reservationId: 1, kind, "Copy of the driving licence");

    [Test]
    public void Create_ShouldOpenAnUnansweredAsk()
    {
        var ask = ReservationRequirement.Create(
            1, ReservationRequirementKind.Payment, " Bank transfer ",
            Money.Of(300m, "TND"), PaymentMethod.Transfer);

        ask.Status.Should().Be(ReservationRequirementStatus.Requested);
        ask.Label.Should().Be("Bank transfer");
        ask.Amount!.Amount.Should().Be(300m);
        ask.ExpectedMethod.Should().Be(PaymentMethod.Transfer);
        ask.IsSettled.Should().BeFalse();
    }

    [Test]
    public void Create_ShouldRefuseAnEmptyLabel()
    {
        FluentActions.Invoking(() => ReservationRequirement.Create(
                1, ReservationRequirementKind.Document, "   "))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Create_ShouldRefuseANegativeAmount()
    {
        FluentActions.Invoking(() => ReservationRequirement.Create(
                1, ReservationRequirementKind.Payment, "Transfer", Money.Of(-1m, "TND")))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Submit_ShouldRecordTheAnswer()
    {
        var ask = AnAsk();

        ask.Submit(fileId: 7, note: "Scanned this morning", at: Now);

        ask.Status.Should().Be(ReservationRequirementStatus.Submitted);
        ask.SubmittedFileId.Should().Be(7);
        ask.SubmittedNote.Should().Be("Scanned this morning");
        ask.SubmittedAt.Should().Be(Now);
    }

    /// <summary>Accepting terms carries no file; everything else says something.</summary>
    [Test]
    public void Submit_ShouldRefuseAnAnswerWithNothingInIt()
    {
        FluentActions.Invoking(() => AnAsk().Submit(null, null, Now))
            .Should().Throw<DomainRuleException>();

        var conditions = AnAsk(ReservationRequirementKind.Conditions);
        conditions.Submit(null, null, Now);
        conditions.Status.Should().Be(ReservationRequirementStatus.Submitted);
    }

    [Test]
    public void Reject_ShouldRequireAReason()
    {
        var ask = AnAsk();
        ask.Submit(1, null, Now);

        FluentActions.Invoking(() => ask.Reject(Now, "staff-1", "  "))
            .Should().Throw<DomainRuleException>();

        ask.Status.Should().Be(ReservationRequirementStatus.Submitted);
    }

    /// <summary>A refusal is a request to try again, not an end.</summary>
    [Test]
    public void ARejectedAsk_ShouldBeAnswerableAgain()
    {
        var ask = AnAsk();
        ask.Submit(1, null, Now);
        ask.Reject(Now, "staff-1", "The scan is unreadable.");

        ask.Status.Should().Be(ReservationRequirementStatus.Rejected);
        ask.ReviewNote.Should().Be("The scan is unreadable.");

        ask.Submit(2, "Better scan", Now.AddHours(1));

        ask.Status.Should().Be(ReservationRequirementStatus.Submitted);
        // The second attempt starts a clean review.
        ask.ReviewNote.Should().BeNull();
        ask.ReviewedAt.Should().BeNull();
    }

    /// <summary>
    /// Most of these are settled at the counter, so an agent ticking off a
    /// deposit paid in cash must not have to fake a submission first.
    /// </summary>
    [Test]
    public void Accept_ShouldWorkStraightFromTheAsk()
    {
        var ask = AnAsk(ReservationRequirementKind.Deposit);

        ask.Accept(Now, "staff-1", "Cash at the desk");

        ask.Status.Should().Be(ReservationRequirementStatus.Accepted);
        ask.IsSettled.Should().BeTrue();
        ask.ReviewedBy.Should().Be("staff-1");
    }

    [Test]
    public void Waive_ShouldSettleItWithoutAnAnswer()
    {
        var ask = AnAsk();

        ask.Waive(Now, "staff-1", "No longer required.");

        ask.Status.Should().Be(ReservationRequirementStatus.Waived);
        ask.IsSettled.Should().BeTrue();
    }

    [Test]
    public void Amend_ShouldOnlyTouchAnAskNobodyHasAnsweredYet()
    {
        var ask = AnAsk();
        ask.Amend("Copy of the passport", Money.Of(10m, "TND"), PaymentMethod.Cash);
        ask.Label.Should().Be("Copy of the passport");

        ask.Submit(1, null, Now);

        FluentActions.Invoking(() => ask.Amend("Something else", null, null))
            .Should().Throw<DomainRuleException>();
    }
}

