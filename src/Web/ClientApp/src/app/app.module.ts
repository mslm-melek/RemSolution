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
import { HomeAgendaComponent } from './home/home-agenda.component';
import { AuthorizeInterceptor } from 'src/api-authorization/authorize.interceptor';
import { ImpersonationInterceptor } from './shared/impersonation.interceptor';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { BrandComponent } from './brand/brand.component';
import { ModelCarComponent } from './model-car/model-car.component';
import { ModelCarFormComponent } from './model-car/model-car-form.component';
import { CarComponent } from './car/car.component';
import { CarFormComponent } from './car/car-form.component';
import { CarDetailComponent } from './car/car-detail.component';
import { ClientComponent } from './client/client.component';
import { ClientFormComponent } from './client/client-form.component';
import { ClientDetailComponent } from './client/client-detail.component';
import { AgencyComponent } from './agency/agency.component';
import { AgencyFormComponent } from './agency/agency-form.component';
import { AgencyDetailComponent } from './agency/agency-detail.component';
import { UserFormComponent } from './user/user-form.component';
import { SubscriptionPlanComponent } from './subscription-plan/subscription-plan.component';
import { SubscriptionPlanFormComponent } from './subscription-plan/subscription-plan-form.component';
import { TeamComponent } from './team/team.component';
import { MyAgencyComponent } from './my-agency/my-agency.component';
import { RentingComponent } from './renting/renting.component';
import { RentingFormComponent } from './renting/renting-form.component';
import { ReservationComponent } from './reservation/reservation.component';
import { ReservationFormComponent } from './reservation/reservation-form.component';
import { ExtraServiceTypeComponent } from './extra-service-type/extra-service-type.component';
import { ExpenseTypeComponent } from './expense-type/expense-type.component';
import { ExpenseFormComponent } from './expense/expense-form.component';
import { CreditComponent } from './credit/credit.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { StatisticsComponent } from './statistics/statistics.component';
import { PlatformDashboardComponent } from './platform-dashboard/platform-dashboard.component';
import { ChatComponent } from './chat/chat.component';
import { NotificationComponent } from './notification/notification.component';
import { DocumentTemplateComponent } from './document-template/document-template.component';
import { DocumentTemplateFormComponent } from './document-template/document-template-form.component';
import { ProfileComponent } from './profile/profile.component';
import { MarketplaceSearchComponent } from './marketplace/marketplace-search.component';
import { MarketplaceCarComponent } from './marketplace/marketplace-car.component';
import { MarketplaceAgencyComponent } from './marketplace/marketplace-agency.component';
import { MarketplaceMapComponent } from './marketplace/marketplace-map.component';
import { MyReservationsComponent } from './marketplace/my-reservations.component';
import { MyRentingsComponent } from './marketplace/my-rentings.component';
import { MyChatsComponent } from './marketplace/my-chats.component';
import { RatingStarsComponent } from './shared/rating-stars.component';
import { QuickActionsComponent } from './shared/quick-actions.component';
import { BookingCalendarComponent } from './shared/booking-calendar.component';
import { MapPickerComponent } from './shared/map-picker.component';
import { BranchesEditorComponent } from './shared/branches-editor.component';
import { PaymentDialogComponent } from './shared/payment-dialog.component';
import { ReturnDialogComponent } from './shared/return-dialog.component';
import { CancelDialogComponent } from './shared/cancel-dialog.component';
import { DateFieldComponent } from './shared/date-field.component';
import { AppDateAdapter, APP_DATE_FORMATS } from './shared/date-adapter';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatTabsModule } from '@angular/material/tabs';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatRadioModule } from '@angular/material/radio';
import { MatProgressBarModule } from '@angular/material/progress-bar';
// The booking wizard (see RentingFormComponent) is the only stepper so far.
import { MatStepperModule } from '@angular/material/stepper';
import { MatDividerModule } from '@angular/material/divider';
import { MatMenuModule } from '@angular/material/menu';
// The unread count on the navigation bell is the only badge so far.
import { MatBadgeModule } from '@angular/material/badge';
// Recording money from a list row is the app's only modal flow so far (see
// PaymentDialogComponent); everything else edits on its own page.
import { MatDialogModule } from '@angular/material/dialog';
// Every date on every screen goes through DateFieldComponent, which is the only
// place this module is used from.
import { MatDatepickerModule } from '@angular/material/datepicker';
import { DateAdapter, MAT_DATE_FORMATS, MAT_DATE_LOCALE } from '@angular/material/core';
import { TranslocoHttpLoader } from './shared/transloco-loader';
import { LanguageInterceptor } from './shared/language.interceptor';
import { guardRoutes } from './shared/must-change-password.guard';
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
    HomeAgendaComponent,
    CarComponent,
    CarFormComponent,
    CarDetailComponent,
    ModelCarComponent,
    ModelCarFormComponent,
    BrandComponent,
    ClientComponent,
    ClientFormComponent,
    ClientDetailComponent,
    AgencyComponent,
    AgencyFormComponent,
    AgencyDetailComponent,
    UserFormComponent,
    SubscriptionPlanComponent,
    SubscriptionPlanFormComponent,
    TeamComponent,
    MyAgencyComponent,
    RentingComponent,
    RentingFormComponent,
    ReservationComponent,
    ReservationFormComponent,
    ExtraServiceTypeComponent,
    ExpenseTypeComponent,
    ExpenseFormComponent,
    CreditComponent,
    DashboardComponent,
    StatisticsComponent,
    PlatformDashboardComponent,
    ChatComponent,
    NotificationComponent,
    DocumentTemplateComponent,
    DocumentTemplateFormComponent,
    ProfileComponent,
    MarketplaceSearchComponent,
    MarketplaceCarComponent,
    MarketplaceAgencyComponent,
    MarketplaceMapComponent,
    MyReservationsComponent,
    MyRentingsComponent,
    MyChatsComponent,
    RatingStarsComponent,
    QuickActionsComponent,
    BookingCalendarComponent,
    MapPickerComponent,
    BranchesEditorComponent,
    PaymentDialogComponent,
    ReturnDialogComponent,
    CancelDialogComponent,
    DateFieldComponent
  ],
  bootstrap: [AppComponent],
  imports: [
    BrowserModule,
    FormsModule,
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatButtonToggleModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatToolbarModule,
    MatSelectModule,
    MatTooltipModule,
    MatTabsModule,
    MatCheckboxModule,
    MatBadgeModule,
    MatRadioModule,
    MatProgressBarModule,
    MatStepperModule,
    MatDividerModule,
    MatMenuModule,
    MatDialogModule,
    MatDatepickerModule,
    // guardRoutes wraps every route below (bar the profile page) in the
    // temporary-password guard, so a route added later cannot forget it.
    RouterModule.forRoot(guardRoutes([
      { path: '', component: HomeComponent, pathMatch: 'full' },
      { path: 'brand', component: BrandComponent },
      { path: 'model-car', component: ModelCarComponent },
      { path: 'model-car/new', component: ModelCarFormComponent },
      { path: 'model-car/:id', component: ModelCarFormComponent },
      // A car and a client each have a page of their own (history, money, the
      // actions the counter needs) with the form behind /edit — same shape the
      // agency console uses below.
      { path: 'car', component: CarComponent },
      { path: 'car/new', component: CarFormComponent },
      { path: 'car/:id', component: CarDetailComponent },
      { path: 'car/:id/edit', component: CarFormComponent },
      { path: 'client', component: ClientComponent },
      { path: 'client/new', component: ClientFormComponent },
      { path: 'client/:id', component: ClientDetailComponent },
      { path: 'client/:id/edit', component: ClientFormComponent },
      { path: 'renting', component: RentingComponent },
      { path: 'renting/new', component: RentingFormComponent },
      { path: 'renting/:id', component: RentingFormComponent },
      { path: 'reservation', component: ReservationComponent },
      { path: 'reservation/new', component: ReservationFormComponent },
      { path: 'reservation/:id', component: ReservationFormComponent },
      { path: 'extra-service-type', component: ExtraServiceTypeComponent },
      { path: 'expense-type', component: ExpenseTypeComponent },
      // Expenses are managed from the finance screen's payable tab now — the
      // standalone list duplicated it — so the list route only redirects, while
      // the form itself is still a page of its own.
      { path: 'expense', redirectTo: 'credit', pathMatch: 'full' },
      { path: 'expense/new', component: ExpenseFormComponent },
      { path: 'expense/:id', component: ExpenseFormComponent },
      { path: 'credit', component: CreditComponent },
      { path: 'dashboard', component: DashboardComponent },
      // The month-by-month / year-by-year report. Its car filter is a query
      // param (?car=) rather than a path segment: the fleet view is the screen's
      // own state, and the cars list and a car's page link in with it set.
      { path: 'statistics', component: StatisticsComponent },
      { path: 'chat', component: ChatComponent },
      // No route guard: the screen reads the caller's own inbox, and an agency
      // without the feature never gets the bell that leads here.
      { path: 'notifications', component: NotificationComponent },
      { path: 'document-template', component: DocumentTemplateComponent },
      { path: 'document-template/new', component: DocumentTemplateFormComponent },
      { path: 'document-template/:id', component: DocumentTemplateFormComponent },
      { path: 'profile', component: ProfileComponent },
      { path: 'browse', component: MarketplaceSearchComponent },
      { path: 'browse/car/:id', component: MarketplaceCarComponent },
      { path: 'browse/agency/:id', component: MarketplaceAgencyComponent },
      { path: 'my-reservations', component: MyReservationsComponent },
      { path: 'my-rentings', component: MyRentingsComponent },
      { path: 'my-chats', component: MyChatsComponent },

      // Platform-admin console. The dashboard is the admin's home screen (see
      // HomeComponent), so its old route only survives to keep bookmarks and the
      // links inside the console working.
      { path: 'platform-dashboard', redirectTo: '', pathMatch: 'full' },
      { path: 'agency', component: AgencyComponent },
      { path: 'agency/new', component: AgencyFormComponent },
      { path: 'agency/:id', component: AgencyDetailComponent },
      { path: 'agency/:id/edit', component: AgencyFormComponent },
      { path: 'agency/:id/user/new', component: UserFormComponent },
      { path: 'agency/:id/user/:userId', component: UserFormComponent },
      { path: 'subscription-plan', component: SubscriptionPlanComponent },
      { path: 'subscription-plan/new', component: SubscriptionPlanFormComponent },
      { path: 'subscription-plan/:id', component: SubscriptionPlanFormComponent },

      // Agency-admin self-service. The team screen is a tab of "My agency"
      // rather than a page of its own now; its old route survives to keep
      // bookmarks and any links to it working.
      { path: 'my-agency', component: MyAgencyComponent },
      { path: 'team', redirectTo: 'my-agency', pathMatch: 'full' }
    ])),
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
    // The calendars: month and weekday names in the active language, everything
    // else spelled out by AppDateAdapter (dd/MM/yyyy, Latin digits, Monday
    // first) rather than left to Intl.
    { provide: MAT_DATE_LOCALE, useFactory: resolveLanguage },
    { provide: DateAdapter, useClass: AppDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: APP_DATE_FORMATS },
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
