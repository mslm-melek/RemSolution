import { Component, OnInit, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { ActivatedRoute } from '@angular/router';
import {
  CarsClient, CarDto, FuelType,
  RentingsClient, RentingDto, RentingState
} from '../web-api-client';
import {
  CarAvailability, canRentNow, carAvailability, carAvailabilityClass, carAvailabilityLabelKey
} from '../shared/car-availability';
import { AuthService } from '../shared/auth.service';
import { ReturnDialogComponent } from '../shared/return-dialog.component';

// One car's page: whether it can be hired out right now, who has it if not, and
// everywhere it has been. The form at /car/:id/edit changes the vehicle's own
// facts (and its photos); this page is for reading them and acting on them.
@Component({
  selector: 'app-car-detail',
  templateUrl: './car-detail.component.html',
  styleUrls: ['./car-detail.component.css']
})
export class CarDetailComponent implements OnInit {
  private readonly dialog = inject(MatDialog);

  carId!: number;
  car?: CarDto;

  rentings: RentingDto[] = [];
  rentingColumns: string[] = ['period', 'client', 'state', 'mileage', 'price', 'actions'];
  rentingsTotal = 0;
  rentingsPage = 1;
  rentingsPageSize = 10;

  canSeeRentings = false;
  canRent = false;
  canReturn = false;
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

    this.auth.currentUser$.subscribe(user => {
      this.canSeeRentings = AuthService.canAccessModule(user, 'Rentings', 'Renting.Read');
      this.canRent = AuthService.canAccessModule(user, 'Rentings', 'Renting.Create');
      this.canReturn = AuthService.canAccessModule(user, 'Rentings', 'Renting.Update');
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

  loadRentings() {
    this.rentingsClient.getRentings(
      this.rentingsPage, this.rentingsPageSize, this.carId, null, null,
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

  // Closes a hire on this car (the same dialog the cars list uses), then re-reads
  // both panels: the car's status and the hire's price have moved. The history
  // table passes its own row rather than relying on the car's current-hire field,
  // so the button can never be a silent no-op.
  returnCar(renting?: RentingDto) {
    const rentingId = renting?.id ?? this.car?.currentRenting?.id;
    if (!rentingId) return;

    this.dialog.open(ReturnDialogComponent, {
      data: {
        rentingId,
        carLabel: [this.car?.matricule, this.car?.modelName].filter(Boolean).join(' · '),
        clientName: renting?.clientName ?? this.car?.currentRenting?.clientName
      },
      autoFocus: 'first-tabbable'
    }).afterClosed().subscribe(returned => {
      if (returned) {
        this.loadCar();
        if (this.canSeeRentings) this.loadRentings();
      }
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

  // Distance covered on a finished hire — the pair of readings is what the
  // odometer columns are for.
  mileageDone(renting: RentingDto): number | null {
    if (renting.startMileage === undefined || renting.startMileage === null) return null;
    if (renting.endMileage === undefined || renting.endMileage === null) return null;
    return renting.endMileage - renting.startMileage;
  }
}
