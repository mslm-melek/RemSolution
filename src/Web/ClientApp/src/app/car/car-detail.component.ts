import { Component, OnInit, inject } from '@angular/core';
import { Directionality } from '@angular/cdk/bidi';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { ActivatedRoute } from '@angular/router';
import {
  CarsClient, CarDto, CarImageDto, CarOverviewDto, CarBookingDto, FuelType,
  RentingsClient, RentingDto, RentingState
} from '../web-api-client';
import {
  CarAvailability, canRentNow, carAvailability, carAvailabilityClass, carAvailabilityLabelKey
} from '../shared/car-availability';
import { AuthService } from '../shared/auth.service';
import { ReturnDialogComponent } from '../shared/return-dialog.component';
import { CarQuickEditComponent } from './car-quick-edit.component';

// One car's page: what the vehicle is, how hard it has been working, who has it
// booked, what it has cost, and everywhere it has been. The form at
// /car/:id/edit owns the whole record (photos included); this page reads it, acts
// on it, and corrects the four fields that go stale between hires.
@Component({
  selector: 'app-car-detail',
  templateUrl: './car-detail.component.html',
  styleUrls: ['./car-detail.component.css']
})
export class CarDetailComponent implements OnInit {
  private readonly dialog = inject(MatDialog);
  // The quick-edit panel is pinned to the edge the page ends on, which is the
  // other edge in Arabic. A dialog is positioned in absolute terms (the CDK
  // overlay knows nothing about the page's direction), so the side is chosen here
  // rather than left to a logical property in the stylesheet.
  private readonly direction = inject(Directionality);

  carId!: number;
  car?: CarDto;

  // The figures and compact lists around the car — utilization, what it billed,
  // how it was rated, who has it next, what it has cost lately. One call (see
  // GetCarOverviewQuery): read separately, the tiles and the lists under them
  // would each describe a different moment.
  overview?: CarOverviewDto;

  // The car's gallery (CarImage), read from its own endpoint: the DTO carries one
  // picture for a list row, this page shows the lot. Managing them — adding,
  // reordering, choosing the primary — stays on the form.
  images: CarImageDto[] = [];
  selectedImageIndex = 0;

  rentings: RentingDto[] = [];
  rentingColumns: string[] = ['period', 'client', 'state', 'mileage', 'price', 'actions'];
  rentingsTotal = 0;
  rentingsPage = 1;
  rentingsPageSize = 10;

  canSeeRentings = false;
  canRent = false;
  canReturn = false;
  canEdit = false;
  // A car's money is what has been spent on it: the finance screen's payable tab
  // filters by car, and either module alone can answer it (see CreditComponent).
  canSeeExpenses = false;
  // How the car has done month by month — the statistics report, filtered to it.
  canSeeStatistics = false;

  private readonly stateLabelKeys: Record<number, string> = {
    [RentingState.NotYet]: 'enums.rentingState.notYet',
    [RentingState.InProgress]: 'enums.rentingState.inProgress',
    [RentingState.Done]: 'enums.rentingState.done',
    [RentingState.Cancelled]: 'enums.rentingState.cancelled'
  };

  private readonly fuelLabelKeys: Record<number, string> = {
    [FuelType.Gasoline]: 'enums.fuelType.gasoline',
    [FuelType.Diesel]: 'enums.fuelType.diesel'
  };

  constructor(
    private cars: CarsClient,
    private rentingsClient: RentingsClient,
    private auth: AuthService,
    private route: ActivatedRoute
  ) { }

  ngOnInit() {
    this.carId = +this.route.snapshot.paramMap.get('id')!;
    this.loadCar();
    this.loadOverview();
    this.loadImages();

    this.auth.currentUser$.subscribe(user => {
      this.canSeeRentings = AuthService.canAccessModule(user, 'Rentings', 'Renting.Read');
      this.canRent = AuthService.canAccessModule(user, 'Rentings', 'Renting.Create');
      this.canReturn = AuthService.canAccessModule(user, 'Rentings', 'Renting.Update');
      this.canEdit = AuthService.canAccessModule(user, 'Cars', 'Car.Update');
      this.canSeeExpenses = AuthService.canAccessModule(user, 'Expenses', 'Expense.Read')
        || AuthService.canAccessModule(user, 'Credits', 'Credit.Read');
      this.canSeeStatistics = AuthService.canAccessModule(user, 'Dashboard', 'Dashboard.View');

      if (this.canSeeRentings) this.loadRentings();
    });
  }

  private loadCar() {
    this.cars.getCarById(this.carId).subscribe({
      next: car => this.car = car,
      error: err => console.error(err)
    });
  }

  // The overview's own sections are gated server-side, so this is not guarded by
  // the permission flags above: whatever the caller may not see comes back null
  // and the template draws nothing for it.
  private loadOverview() {
    this.cars.getCarOverview(this.carId).subscribe({
      next: overview => this.overview = overview,
      error: err => console.error(err)
    });
  }

  // Everything a write on this car (a return, a quick edit) can have moved.
  private reload() {
    this.loadCar();
    this.loadOverview();
    if (this.canSeeRentings) this.loadRentings();
  }

  /**
   * What the car is called: make and model together ("Renault Clio"). A fleet
   * with a Clio and a 208 in it is not helped by a bare model name, and either
   * half can be missing on a car whose model was never filled in.
   */
  get carName(): string {
    return [this.car?.brandName, this.car?.modelName].filter(Boolean).join(' ');
  }

  // --- Photos -----------------------------------------------------------------

  private loadImages() {
    this.cars.getCarImages(this.carId).subscribe({
      next: images => {
        this.images = images || [];
        // Open on the primary image — the one the fleet chose as the car's face,
        // and the one every list row is already showing.
        const primary = this.images.findIndex(image => image.isPrimary);
        this.selectedImageIndex = primary >= 0 ? primary : 0;
      },
      error: err => console.error(err)
    });
  }

  get hasPhotos(): boolean {
    return this.images.length > 0 || !!this.car?.imageUrl;
  }

  /**
   * The big picture. Prefers the medium derivative: it is generated for exactly
   * this, and the original can be a several-megabyte phone photo. Falls back
   * through what exists, because the derivatives are produced out of band and for
   * a few seconds after an upload the original is all there is.
   */
  get heroUrl(): string | undefined {
    const image = this.images[this.selectedImageIndex];

    return image
      ? image.mediumUrl || image.originalUrl || image.thumbnailUrl
      : this.car?.imageUrl;
  }

  /** Full size, for opening in a tab: the untouched upload where there is one. */
  get heroHref(): string | undefined {
    const image = this.images[this.selectedImageIndex];

    return (image ? image.originalUrl : undefined) || this.heroUrl;
  }

  thumbFor(image: CarImageDto): string | undefined {
    return image.thumbnailUrl || image.mediumUrl || image.originalUrl;
  }

  selectImage(index: number) {
    if (index >= 0 && index < this.images.length) this.selectedImageIndex = index;
  }

  // --- Overview ----------------------------------------------------------------

  /**
   * The third tile. A rating is the best thing to put there — it is the only
   * figure on the page that comes from outside the agency — but reviews arrive
   * from marketplace customers, so most cars have none. Those show how much work
   * the car took on instead, which is the same window as the two tiles beside it.
   */
  get showRating(): boolean {
    return (this.overview?.rating?.count ?? 0) > 0;
  }

  /** Active and upcoming hires, as the compact list shows them. */
  get bookings(): CarBookingDto[] {
    return this.overview?.bookings ?? [];
  }

  bookingStateClass(booking: CarBookingDto): string {
    if (booking.isLate) return 'danger';
    return this.stateClass(booking.state);
  }

  // --- History table -----------------------------------------------------------

  loadRentings() {
    this.rentingsClient.getRentings(
      this.rentingsPage, this.rentingsPageSize, null, this.carId, null, null,
      null, null, undefined, false, 'period', true
    ).subscribe({
      next: result => {
        this.rentings = result.items || [];
        this.rentingsTotal = result.totalCount || 0;
      },
      error: err => console.error(err)
    });
  }

  onRentingsPage(event: PageEvent) {
    this.rentingsPage = event.pageIndex + 1;
    this.rentingsPageSize = event.pageSize;
    this.loadRentings();
  }

  // --- Availability ----------------------------------------------------------

  get availability(): CarAvailability | null {
    return this.car ? carAvailability(this.car) : null;
  }

  get availabilityLabelKey(): string {
    return this.car ? carAvailabilityLabelKey(carAvailability(this.car)) : '';
  }

  get availabilityClass(): string {
    return this.car ? carAvailabilityClass(carAvailability(this.car)) : 'neutral';
  }

  get canRentOut(): boolean {
    return this.canRent && !!this.car && canRentNow(this.car);
  }

  get canTakeBack(): boolean {
    return this.canReturn && !!this.car?.currentRenting?.id;
  }

  // --- Actions -----------------------------------------------------------------

  // Closes a hire on this car (the same dialog the cars list uses), then re-reads
  // the page: the car's status, its figures and the hire's price have all moved.
  // The history table passes its own row rather than relying on the car's
  // current-hire field, so the button can never be a silent no-op.
  returnCar(renting?: RentingDto | CarBookingDto) {
    const rentingId = this.rentingIdOf(renting) ?? this.car?.currentRenting?.id;
    if (!rentingId) return;

    this.dialog.open(ReturnDialogComponent, {
      data: {
        rentingId,
        carLabel: [this.car?.matricule, this.carName].filter(Boolean).join(' · '),
        clientName: renting?.clientName ?? this.car?.currentRenting?.clientName
      },
      autoFocus: 'first-tabbable'
    }).afterClosed().subscribe(returned => {
      if (returned) this.reload();
    });
  }

  /**
   * The four fields that go stale between hires — where the car is based, what it
   * costs, whether it is on the road, what the odometer reads — without leaving
   * the page for the whole record. Everything else is still the form's.
   */
  openQuickEdit() {
    if (!this.canEdit || !this.car) return;

    this.dialog.open(CarQuickEditComponent, {
      data: { carId: this.carId },
      // A slide-over rather than a centred box: the page behind it is the context
      // for what is being changed, and a dialog over the middle of it hides
      // exactly the panel the fields are read off.
      panelClass: 'side-panel',
      position: this.direction.value === 'rtl' ? { top: '0', left: '0' } : { top: '0', right: '0' },
      height: '100vh',
      width: '380px',
      maxWidth: '100vw',
      autoFocus: 'first-tabbable'
    }).afterClosed().subscribe(saved => {
      // The rate and the odometer feed the figures, not just the spec box.
      if (saved) this.reload();
    });
  }

  // --- Labels (transloco keys; the template pipes them) ----------------------

  stateLabelKey(state?: RentingState): string {
    return state === undefined ? '' : this.stateLabelKeys[state] ?? '';
  }

  stateClass(state?: RentingState): string {
    switch (state) {
      case RentingState.InProgress: return 'ok';
      case RentingState.NotYet: return 'info';
      case RentingState.Cancelled: return 'danger';
      default: return 'neutral';
    }
  }

  get fuelLabelKey(): string {
    const fuel = this.car?.fuelType;
    return fuel === undefined || fuel === null ? '' : this.fuelLabelKeys[fuel] ?? '';
  }

  canTakeBackRow(renting: RentingDto): boolean {
    return this.canReturn && renting.rentingState === RentingState.InProgress && !!renting.id;
  }

  /** The return action reaches it from a history row and from a booking row alike. */
  canTakeBackBooking(booking: CarBookingDto): boolean {
    return this.canReturn && booking.state === RentingState.InProgress && !!booking.rentingId;
  }

  // Distance covered on a finished hire — the pair of readings is what the
  // odometer columns are for.
  mileageDone(renting: RentingDto): number | null {
    if (renting.startMileage === undefined || renting.startMileage === null) return null;
    if (renting.endMileage === undefined || renting.endMileage === null) return null;
    return renting.endMileage - renting.startMileage;
  }

  // A booking row names the hire `rentingId`; a history row IS the hire. Both
  // reach the same return dialog, so the two spellings are resolved here rather
  // than at each call site.
  private rentingIdOf(renting?: RentingDto | CarBookingDto): number | undefined {
    if (!renting) return undefined;
    return (renting as CarBookingDto).rentingId ?? (renting as RentingDto).id;
  }
}
