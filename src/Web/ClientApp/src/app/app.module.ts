import { BrowserModule } from '@angular/platform-browser';
import { APP_ID, APP_INITIALIZER, LOCALE_ID, NgModule } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeFr from '@angular/common/locales/fr';
import localeAr from '@angular/common/locales/ar';
import { provideTransloco, TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

import { AppComponent } from './app.component';
import { NavMenuComponent } from './nav-menu/nav-menu.component';
import { HomeComponent } from './home/home.component';
import { AuthorizeInterceptor } from 'src/api-authorization/authorize.interceptor';
import { ImpersonationInterceptor } from './shared/impersonation.interceptor';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { BrandComponent } from './brand/brand.component';
import { ModelCarComponent } from './model-car/model-car.component';
import { ModelCarFormComponent } from './model-car/model-car-form.component';
import { CarComponent } from './car/car.component';
import { CarFormComponent } from './car/car-form.component';
import { ClientComponent } from './client/client.component';
import { ClientFormComponent } from './client/client-form.component';
import { AgencyComponent } from './agency/agency.component';
import { AgencyFormComponent } from './agency/agency-form.component';
import { AgencyDetailComponent } from './agency/agency-detail.component';
import { AgencyCarsComponent } from './agency/agency-cars.component';
import { AgencyClientsComponent } from './agency/agency-clients.component';
import { UserFormComponent } from './user/user-form.component';
import { SubscriptionPlanComponent } from './subscription-plan/subscription-plan.component';
import { SubscriptionPlanFormComponent } from './subscription-plan/subscription-plan-form.component';
import { TeamComponent } from './team/team.component';
import { RentingComponent } from './renting/renting.component';
import { RentingFormComponent } from './renting/renting-form.component';
import { ReservationComponent } from './reservation/reservation.component';
import { ReservationFormComponent } from './reservation/reservation-form.component';
import { ExtraServiceTypeComponent } from './extra-service-type/extra-service-type.component';
import { ExpenseTypeComponent } from './expense-type/expense-type.component';
import { DocumentTemplateComponent } from './document-template/document-template.component';
import { DocumentTemplateFormComponent } from './document-template/document-template-form.component';
import { ProfileComponent } from './profile/profile.component';
import { MarketplaceSearchComponent } from './marketplace/marketplace-search.component';
import { MarketplaceCarComponent } from './marketplace/marketplace-car.component';
import { MyReservationsComponent } from './marketplace/my-reservations.component';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatTabsModule } from '@angular/material/tabs';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatRadioModule } from '@angular/material/radio';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatDividerModule } from '@angular/material/divider';
import { MatMenuModule } from '@angular/material/menu';
import { TranslocoHttpLoader } from './shared/transloco-loader';
import { LanguageInterceptor } from './shared/language.interceptor';
import { DEFAULT_LANGUAGE, SUPPORTED_LANGUAGES, resolveLanguage } from './shared/language';
import { environment } from 'src/environments/environment';

// Drives the date / number pipes used throughout the tables. Angular's Arabic
// data already formats with Latin digits (350, not ٣٥٠); it groups US-style
// (1,234.56) — swap the import for 'ar-TN' or 'ar-DZ' to get the Maghrebi
// 1 234,56 instead.
registerLocaleData(localeFr);
registerLocaleData(localeAr);

@NgModule({
  declarations: [
    AppComponent,
    NavMenuComponent,
    HomeComponent,
    CarComponent,
    CarFormComponent,
    ModelCarComponent,
    ModelCarFormComponent,
    BrandComponent,
    ClientComponent,
    ClientFormComponent,
    AgencyComponent,
    AgencyFormComponent,
    AgencyDetailComponent,
    AgencyCarsComponent,
    AgencyClientsComponent,
    UserFormComponent,
    SubscriptionPlanComponent,
    SubscriptionPlanFormComponent,
    TeamComponent,
    RentingComponent,
    RentingFormComponent,
    ReservationComponent,
    ReservationFormComponent,
    ExtraServiceTypeComponent,
    ExpenseTypeComponent,
    DocumentTemplateComponent,
    DocumentTemplateFormComponent,
    ProfileComponent,
    MarketplaceSearchComponent,
    MarketplaceCarComponent,
    MyReservationsComponent
  ],
  bootstrap: [AppComponent],
  imports: [
    BrowserModule,
    FormsModule,
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatToolbarModule,
    MatSelectModule,
    MatTooltipModule,
    MatTabsModule,
    MatCheckboxModule,
    MatRadioModule,
    MatProgressBarModule,
    MatDividerModule,
    MatMenuModule,
    RouterModule.forRoot([
      { path: '', component: HomeComponent, pathMatch: 'full' },
      { path: 'brand', component: BrandComponent },
      { path: 'model-car', component: ModelCarComponent },
      { path: 'model-car/new', component: ModelCarFormComponent },
      { path: 'model-car/:id', component: ModelCarFormComponent },
      { path: 'car', component: CarComponent },
      { path: 'car/new', component: CarFormComponent },
      { path: 'car/:id', component: CarFormComponent },
      { path: 'client', component: ClientComponent },
      { path: 'client/new', component: ClientFormComponent },
      { path: 'client/:id', component: ClientFormComponent },
      { path: 'renting', component: RentingComponent },
      { path: 'renting/new', component: RentingFormComponent },
      { path: 'renting/:id', component: RentingFormComponent },
      { path: 'reservation', component: ReservationComponent },
      { path: 'reservation/new', component: ReservationFormComponent },
      { path: 'reservation/:id', component: ReservationFormComponent },
      { path: 'extra-service-type', component: ExtraServiceTypeComponent },
      { path: 'expense-type', component: ExpenseTypeComponent },
      { path: 'document-template', component: DocumentTemplateComponent },
      { path: 'document-template/new', component: DocumentTemplateFormComponent },
      { path: 'document-template/:id', component: DocumentTemplateFormComponent },
      { path: 'profile', component: ProfileComponent },
      { path: 'browse', component: MarketplaceSearchComponent },
      { path: 'browse/car/:id', component: MarketplaceCarComponent },
      { path: 'my-reservations', component: MyReservationsComponent },

      // Platform-admin console.
      { path: 'agency', component: AgencyComponent },
      { path: 'agency/new', component: AgencyFormComponent },
      { path: 'agency/:id', component: AgencyDetailComponent },
      { path: 'agency/:id/edit', component: AgencyFormComponent },
      { path: 'agency/:id/user/new', component: UserFormComponent },
      { path: 'agency/:id/user/:userId', component: UserFormComponent },
      { path: 'agency/:id/cars', component: AgencyCarsComponent },
      { path: 'agency/:id/clients', component: AgencyClientsComponent },
      { path: 'subscription-plan', component: SubscriptionPlanComponent },
      { path: 'subscription-plan/new', component: SubscriptionPlanFormComponent },
      { path: 'subscription-plan/:id', component: SubscriptionPlanFormComponent },

      // Agency-admin self-service.
      { path: 'team', component: TeamComponent }
    ]),
    TranslocoModule,
    BrowserAnimationsModule],
  providers: [
    { provide: APP_ID, useValue: 'ng-cli-universal' },
    { provide: HTTP_INTERCEPTORS, useClass: AuthorizeInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: ImpersonationInterceptor, multi: true },
    // Tells the API which language to answer validation and error text in.
    { provide: HTTP_INTERCEPTORS, useClass: LanguageInterceptor, multi: true },
    provideHttpClient(withInterceptorsFromDi()),
    provideAnimationsAsync(),
    provideTransloco({
      config: {
        availableLangs: SUPPORTED_LANGUAGES,
        // Same resolution main.ts used for `<html lang/dir>` — a pure read of
        // cookie / storage / navigator, so both agree.
        defaultLang: resolveLanguage(),
        fallbackLang: DEFAULT_LANGUAGE,
        // A key missing from a translation file falls back to the default
        // language instead of rendering the raw key at the user.
        missingHandler: { useFallbackTranslation: true },
        reRenderOnLangChange: true,
        prodMode: environment.production
      },
      loader: TranslocoHttpLoader
    }),
    // LOCALE_ID is fixed at bootstrap, which is why switching language reloads
    // the page (see LanguageService.use).
    { provide: LOCALE_ID, useFactory: resolveLanguage },
    // Have the translation file in memory before the first render. Components
    // that translate imperatively (confirm dialogs, error banners) call
    // TranslocoService.translate() synchronously and would otherwise show the
    // raw key if they ran before the initial fetch resolved.
    {
      provide: APP_INITIALIZER,
      multi: true,
      deps: [TranslocoService],
      useFactory: (transloco: TranslocoService) => () =>
        firstValueFrom(transloco.load(transloco.getActiveLang()))
    }
  ]
})
export class AppModule { }
