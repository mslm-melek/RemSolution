namespace RemSolution.Application.Features.Client.DTOs
{
    /// <summary>
    /// The outcome of re-cutting a client's portrait out of their CIN image. A
    /// record of what happened rather than a bare URL (like
    /// <see cref="ClientInvitationDto"/>): "there is no face on that image" is an
    /// answer the agency has to be told, and it is not an error — a PDF scan or a
    /// picture of the back of the card produces it perfectly legitimately.
    /// </summary>
    public class ClientPortraitDto
    {
        /// <summary>The new portrait, or null when none could be produced.</summary>
        public string? PortraitUrl { get; init; }

        /// <summary>
        /// Whether the client has a CIN image at all. Distinguishes "nothing to
        /// crop from" from "cropped, but no face was found there".
        /// </summary>
        public bool HasCinImage { get; init; }
    }
}
