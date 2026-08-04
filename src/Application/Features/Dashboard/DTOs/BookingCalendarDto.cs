using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Dashboard.DTOs
{
    // What a day of the agency's calendar is made of. A hire contributes two
    // independent entries — the day it is picked up and the day it comes back —
    // because those are two separate jobs for the desk, landing on two days.
    public enum BookingCalendarEventKind
    {
        // A hire starting: the car goes out.
        Pickup = 1,
        // A hire ending: the car is due back.
        Return = 2,
        // An active hold starting — still a request the desk has to turn into a
        // hire, so it is marked apart from the pickups it may become.
        ReservationStart = 3,
    }

    // One thing happening on one day. Deliberately flat and label-carrying rather
    // than a trimmed RentingDto: the calendar shows a car and a name per entry and
    // nothing else, and a month of full booking DTOs would be an order of
    // magnitude more payload for figures nobody reads from a grid.
    public class BookingCalendarEventDto
    {
        public BookingCalendarEventKind Kind { get; init; }
        // The moment it happens, UTC like every other domain date.
        public DateTime On { get; init; }

        // Exactly one of the two is set, following Kind: an entry links back to
        // the record it came from, so a click can open it.
        public int? RentingId { get; init; }
        public int? ReservationId { get; init; }

        public int? CarId { get; init; }
        public string? CarMatricule { get; init; }
        public string? CarModelName { get; init; }
        public int? ClientId { get; init; }
        public string? ClientName { get; init; }

        // The record's own state, so the calendar can tell an upcoming pickup from
        // one already collected, and a hold that is only requested from one the
        // agency has confirmed. Set to match Kind, like the ids above.
        public RentingState? RentingState { get; init; }
        public ReservationStatus? ReservationStatus { get; init; }

        /// <summary>
        /// A return that was due before now and whose hire is still out. The desk
        /// reads the calendar to find these, so the figure is computed here rather
        /// than left to a client whose clock may disagree.
        /// </summary>
        public bool IsLate { get; init; }
    }

    // A window of the calendar. The window is echoed back so the screen can tell
    // a clamped answer (see the query's day cap) from the one it asked for.
    public class BookingCalendarDto
    {
        // Half-open [From, To), the same convention as the dashboard's period.
        public DateTime From { get; init; }
        public DateTime To { get; init; }

        /// <summary>
        /// True when the window held more bookings than one calendar screen will
        /// carry and the tail was dropped. Said out loud rather than silently
        /// truncated: a day that quietly shows three of its five pickups is worse
        /// than one that admits the grid is not the whole story.
        /// </summary>
        public bool IsTruncated { get; init; }

        // Chronological, earliest first.
        public IList<BookingCalendarEventDto> Events { get; init; } = new List<BookingCalendarEventDto>();
    }
}
