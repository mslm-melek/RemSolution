import { Component, Input } from '@angular/core';

// The two fields the avatar needs — a ClientDto satisfies it.
export interface ClientAvatarFacts {
  firstName?: string;
  lastName?: string;
  cinPortraitUrl?: string;
}

// The one way a client's face is drawn anywhere in the app.
//
// The face itself is cut out of their CIN image on the server (see
// IPortraitCropper) and arrives as ClientDto.cinPortraitUrl. Plenty of clients
// have no CIN image, or one with no readable photo on it — a PDF scan, a picture
// of the back of the card — so every screen needs the same stand-in, and a
// component is what keeps it the SAME stand-in: a list, a detail header and a
// form that each invented their own would make the same client look like three
// different records.
//
// The stand-in is a generic silhouette, not the client's initials. Initials read
// as data — as if the app knew something about this person that it drew for you —
// where a silhouette reads as exactly what it is: no photograph on file. It is
// drawn inline rather than loaded as an image file so it takes its colour from the
// theme, which a bitmap could not.
@Component({
  selector: 'app-client-avatar',
  templateUrl: './client-avatar.component.html',
  styleUrls: ['./client-avatar.component.css']
})
export class ClientAvatarComponent {
  @Input() client: ClientAvatarFacts | null | undefined;

  // Row size by default; 'lg' is the one on a detail header.
  @Input() size: 'sm' | 'lg' = 'sm';

  get portraitUrl(): string | undefined {
    return this.client?.cinPortraitUrl;
  }

  // Named in the accessibility tree, so a screen reader hears whose face this is
  // rather than "image". The silhouette gets no name at all (see the template):
  // it carries no information, and announcing it would only interrupt the name
  // sitting right beside it.
  get name(): string {
    return [this.client?.firstName, this.client?.lastName].filter(Boolean).join(' ');
  }
}
