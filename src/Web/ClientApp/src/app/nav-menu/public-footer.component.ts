import { Component } from '@angular/core';
import { PUBLIC_CONTACT } from '../shared/public-site';

/** The marketplace's footer, drawn by the shell under every page a visitor or customer sees. */
@Component({
  selector: 'app-public-footer',
  templateUrl: './public-footer.component.html',
  styleUrls: ['./public-footer.component.css']
})
export class PublicFooterComponent {
  readonly contact = PUBLIC_CONTACT;
  readonly year = new Date().getFullYear();

  readonly telHref = 'tel:' + PUBLIC_CONTACT.phone.replace(/\s/g, '');
}
