import { Component, OnInit, inject } from '@angular/core';
import { Directionality } from '@angular/cdk/bidi';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { ActivatedRoute } from '@angular/router';
import {
  CarsClient, CarDto, CarImageDto, CarOverviewDto, CarBookingDto, FuelType,
  CarExpenseScheduleDto, CarUnavailabilityDto, RentingsClient, RentingDto, RentingState
} from '../web-api-client';
import { unavailabilityReasonLabelKey } from '../shared/car-unavailability';
import {
  CarUnavailabilityDialogComponent, CarUnavailabilityDialogData
} from './car-unavailability-dialog.component';
import {
  CarAvailability, canRentNow, carAvailability, carAvailabilityClass, carAvailabilityLabelKey
} from '../shared/car-availability';
import { AuthService } from '../shared/auth.service';
import { ReturnDialogComponent } from '../shared/return-dialog.component';
import { CarQuickEditComponent } from './car-quick-edit.component';

/** One servicing line: the resolved schedule plus the distance left on it. */
interface CarScheduleRow {
  schedule: CarExpenseScheduleDto;
  kilometersLeft?: number;
}

// One car's page: what it is, how it is working, who has it booked and what it
// has cost. The form at /car/:id/edit owns the record; this page reads and acts.
@Component({
  selector: 'app-car-detail',
  templateUrl: './car-detail.component.html',
  styleUrls: ['./car-detail.component.css']
})
export class CarDetailComponent implements OnInit {
  private readonly dialog = inject(MatDialog);
  // The CDK overlay positions in absolute terms, so the panel's side is chosen
  // here rather than by a logical property in the stylesheet.
  private readonly direction = inject(Directionality);

  carId!: number;
  car?: CarDto;

  // The tiles and compact lists around the car, in one call (GetCarOverviewQuery)
  // so they all describe the same moment.
  overview?: CarOverviewDto;

  // The car's gallery (CarImage), from its own endpoint; the form manages it.
  images: CarImageDto[] = [];
  selectedImageIndex = 0;

  // When each recurring cost falls due on this car, resolved server-side with the
  // rule the notification sweep uses.
  private schedules: CarExpenseScheduleDto[] = [];

  // Built rather than derived in a getter: the template reads it twice, and a
  // getter would rebuild the panel on every change-detection pass.
  scheduleRows: CarScheduleRow[] = [];

  // Dates this car is declared off the road. Upcoming only: the panel exists to
  // plan around, and a car's servicing history belongs to its expenses.
  unavailabilities: CarUnavailabilityDto[] = [];
  readonly unavailabilityReasonLabelKey = unavailabilityReasonLabelKey;

  rentings: RentingDto[] = [];
  rentingColumns: string[] = ['period', 'client', 'state', 'mileage', 'price', 'actions'];
  rentingsTotal = 0;
  rentingsPage = 1;
  rentingsPageSize = 10;

  canSeeRentings = false;
  canRent = false;
  canReturn = false;
  canEdit = false;
  // Either finance module can answer what the car has cost (see CreditComponent).
  canSeeExpenses = false;
  // The statistics report, filtered to this car.
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
    this.loadSchedules();
    this.loadUnavailabilities();

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
      next: car => {
        this.car = car;
        // The odometer feeds the distance schedules, and the calls land in either order.
        this.buildScheduleRows();
      },
      error: err => console.error(err)
    });
  }

  // Gated server-side per section: what the caller may not see comes back null.
  private loadOverview() {
    this.cars.getCarOverview(this.carId).subscribe({
      next: overview => this.overview = overview,
      error: err => console.error(err)
    });
  }

  // Not guarded by canSeeExpenses: the query carries its own permission, so a
  // caller who may not read costs gets a 403 and no panel.
  private loadSchedules() {
    this.cars.getCarExpenseSchedules(this.carId).subscribe({
      next: rows => {
        this.schedules = rows || [];
        this.buildScheduleRows();
      },
      error: () => {
        this.schedules = [];
        this.buildScheduleRows();
      }
    });
  }

  private loadUnavailabilities() {
    this.cars.getCarUnavailabilities(this.carId, false).subscribe({
      next: rows => this.unavailabilities = rows || [],
      // A car whose blocks cannot be read is still a car worth showing, so this
      // panel degrades to empty rather than taking the page down.
      error: () => this.unavailabilities = []
    });
  }

  declareUnavailability(block?: CarUnavailabilityDto) {
    this.dialog
      .open<CarUnavailabilityDialogComponent, CarUnavailabilityDialogData, boolean>(
        CarUnavailabilityDialogComponent,
        { data: { carId: this.carId, carLabel: this.car?.matricule ?? undefined, block } })
      .afterClosed()
      .subscribe(saved => {
        if (saved) {
          this.loadUnavailabilities();
          // A block changes what the car is available for, which the overview
          // tiles report.
          this.loadOverview();
        }
      });
  }

  removeUnavailability(block: CarUnavailabilityDto) {
    this.cars.deleteCarUnavailability(block.id!).subscribe({
      next: () => {
        this.loadUnavailabilities();
        this.loadOverview();
      },
      error: err => console.error(err)
    });
  }

  /** Only types with a next due date or reading; the rest have nothing to show. */
  private buildScheduleRows() {
    const mileage = this.car?.mileage;

    this.scheduleRows = this.schedules
      .filter(s => s.nextDueOn || s.nextDueAtKilometers)
      .map(s => ({
        schedule: s,
        // "1 200 km to go" reads better than "due at 128 000"; needs the odometer.
        kilometersLeft: s.nextDueAtKilometers != null && mileage != null
          ? s.nextDueAtKilometers - mileage
          : undefined
      }));
  }

  // Everything a write on this car (a return, a quick edit) can have moved.
  private reload() {
    this.loadCar();
    this.loadOverview();
    // A return moves the odometer, and with it the distance schedules.
    this.loadSchedules();
    if (this.canSeeRentings) this.loadRentings();
  }

  /** Make and model together ("Renault Clio"); either half can be missing. */
  get carName(): string {
    return [this.car?.brandName, this.car?.modelName].filter(Boolean).join(' ');
  }

  // --- Photos -----------------------------------------------------------------

  private loadImages() {
    this.cars.getCarImages(this.carId).subscribe({
      next: images => {
        this.images = images || [];
        // Open on the primary image, the one every list row shows.
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
   * The big picture. Prefers the medium derivative (the original can be a huge
   * phone photo) and falls back, since derivatives are produced out of band.
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

  /** The third tile shows the rating when there is one, and workload otherwise. */
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

  // Closes a hire on this car (the dialog the cars list uses), then re-reads the
  // page. The caller passes its own row, so the button is never a silent no-op.
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

  /** The four fields that go stale between hires: branch, rate, status, odometer. */
  openQuickEdit() {
    if (!this.canEdit || !this.car) return;

    this.dialog.open(CarQuickEditComponent, {
      data: { carId: this.carId },
      // A slide-over, so the page behind stays readable as context.
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

  // Distance covered on a finished hire, from the pair of readings.
  mileageDone(renting: RentingDto): number | null {
    if (renting.startMileage === undefined || renting.startMileage === null) return null;
    if (renting.endMileage === undefined || renting.endMileage === null) return null;
    return renting.endMileage - renting.startMileage;
  }

  // A booking row names the hire `rentingId`; a history row IS the hire.
  private rentingIdOf(renting?: RentingDto | CarBookingDto): number | undefined {
    if (!renting) return undefined;
    return (renting as CarBookingDto).rentingId ?? (renting as RentingDto).id;
  }
}
